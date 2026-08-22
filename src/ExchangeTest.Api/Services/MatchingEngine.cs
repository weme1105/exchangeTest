using ExchangeTest.Api.Models;

namespace ExchangeTest.Api.Services;

public sealed class MatchingEngine
{
    private readonly InMemoryTradingStore _store;
    private readonly MockMarketPriceService _market;
    private readonly PositionService _positionService;
    private readonly object _syncRoot = new();

    public MatchingEngine(
        InMemoryTradingStore store,
        MockMarketPriceService market,
        PositionService positionService)
    {
        _store = store;
        _market = market;
        _positionService = positionService;
    }

    public Order PlaceOrder(Guid userId, PlaceOrderRequest request)
    {
        ValidateRequest(request);

        var contract = _store.Contracts.SingleOrDefault(x =>
            x.Symbol.Equals(request.Symbol, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Unknown contract: {request.Symbol}");

        if (!contract.IsTradable)
            throw new InvalidOperationException("Contract is not tradable.");

        if (request.OrderType == OrderType.Limit && request.Price % contract.TickSize != 0)
            throw new ArgumentException($"Price must follow tick size {contract.TickSize}.");

        var order = new Order
        {
            UserId = userId,
            Symbol = contract.Symbol,
            Side = request.Side,
            OrderType = request.OrderType,
            LimitPrice = request.Price,
            Quantity = request.Quantity
        };

        lock (_syncRoot)
        {
            _store.Orders.Add(order);
            Match(order, contract);

            if (order.OrderType == OrderType.Market && IsOpen(order))
                CancelMarketRemainder(order);
        }

        return order;
    }

    public void OnMarketPriceChanged(string symbol)
    {
        lock (_syncRoot)
        {
            var contract = _store.Contracts.SingleOrDefault(x =>
                x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException($"Unknown contract: {symbol}");

            var pending = _store.Orders
                .Where(x => x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))
                .Where(IsOpen)
                .Where(x => x.OrderType == OrderType.Limit)
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .ToList();

            foreach (var order in pending)
            {
                if (IsOpen(order))
                    Match(order, contract);
            }
        }
    }

    public void Cancel(Guid userId, Guid orderId)
    {
        lock (_syncRoot)
        {
            var order = _store.Orders.SingleOrDefault(x => x.Id == orderId)
                ?? throw new KeyNotFoundException("Order not found.");

            if (order.UserId != userId)
                throw new UnauthorizedAccessException();

            if (!IsOpen(order))
                throw new InvalidOperationException("Only pending or partially filled orders can be cancelled.");

            order.CancelledQuantity += order.RemainingQuantity;
            order.Status = order.FilledQuantity > 0
                ? OrderStatus.PartiallyFilledCancelled
                : OrderStatus.Cancelled;
            order.CompletedAt = DateTimeOffset.UtcNow;
        }
    }

    private void Match(Order incoming, FuturesContract contract)
    {
        if (!IsOpen(incoming))
            return;

        var marketPrice = _market.GetPrice(incoming.Symbol);

        if (!AcceptsExecutionPrice(incoming, marketPrice))
            return;

        var candidates = _store.Orders
            .Where(x => x.Id != incoming.Id)
            .Where(x => x.Symbol.Equals(incoming.Symbol, StringComparison.OrdinalIgnoreCase))
            .Where(x => x.Side != incoming.Side)
            .Where(IsOpen)
            .Where(x => AcceptsExecutionPrice(x, marketPrice))
            .ToList();

        candidates = incoming.Side == OrderSide.Buy
            ? candidates
                .OrderBy(x => EffectivePriorityPrice(x, decimal.MaxValue))
                .ThenBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .ToList()
            : candidates
                .OrderByDescending(x => EffectivePriorityPrice(x, decimal.MinValue))
                .ThenBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .ToList();

        foreach (var resting in candidates)
        {
            if (incoming.RemainingQuantity == 0)
                break;

            if (!IsOpen(resting))
                continue;

            var fillQuantity = Math.Min(incoming.RemainingQuantity, resting.RemainingQuantity);
            if (fillQuantity <= 0)
                continue;

            CreateTrade(incoming, resting, marketPrice, fillQuantity, contract);
            ApplyFill(incoming, fillQuantity);
            ApplyFill(resting, fillQuantity);
        }
    }

    private void CreateTrade(
        Order incoming,
        Order resting,
        decimal executionPrice,
        int quantity,
        FuturesContract contract)
    {
        var buyOrder = incoming.Side == OrderSide.Buy ? incoming : resting;
        var sellOrder = incoming.Side == OrderSide.Sell ? incoming : resting;

        var trade = new Trade
        {
            BuyOrderId = buyOrder.Id,
            SellOrderId = sellOrder.Id,
            BuyerUserId = buyOrder.UserId,
            SellerUserId = sellOrder.UserId,
            Symbol = incoming.Symbol,
            Price = executionPrice,
            Quantity = quantity,
            PointValueAtTrade = contract.PointValue
        };

        _store.Trades.Add(trade);

        _positionService.ApplyTrade(
            buyOrder.UserId,
            trade.Symbol,
            OrderSide.Buy,
            trade.Price,
            trade.Quantity,
            trade.Id,
            trade.PointValueAtTrade);

        _positionService.ApplyTrade(
            sellOrder.UserId,
            trade.Symbol,
            OrderSide.Sell,
            trade.Price,
            trade.Quantity,
            trade.Id,
            trade.PointValueAtTrade);
    }

    private static void ApplyFill(Order order, int quantity)
    {
        order.FilledQuantity += quantity;

        if (order.RemainingQuantity > 0)
        {
            order.Status = OrderStatus.PartiallyFilled;
            return;
        }

        order.Status = OrderStatus.Filled;
        order.CompletedAt = DateTimeOffset.UtcNow;
    }

    private static void CancelMarketRemainder(Order order)
    {
        var remaining = order.RemainingQuantity;
        if (remaining <= 0)
            return;

        order.CancelledQuantity += remaining;
        order.Status = order.FilledQuantity > 0
            ? OrderStatus.PartiallyFilledCancelled
            : OrderStatus.Cancelled;
        order.CompletedAt = DateTimeOffset.UtcNow;
    }

    private static bool AcceptsExecutionPrice(Order order, decimal marketPrice)
    {
        if (order.OrderType == OrderType.Market)
            return true;

        if (order.LimitPrice is null)
            return false;

        return order.Side == OrderSide.Buy
            ? marketPrice <= order.LimitPrice.Value
            : marketPrice >= order.LimitPrice.Value;
    }

    private static decimal EffectivePriorityPrice(Order order, decimal marketOrderPrice)
        => order.OrderType == OrderType.Market
            ? marketOrderPrice
            : order.LimitPrice!.Value;

    private static bool IsOpen(Order order)
        => order.Status is OrderStatus.Pending or OrderStatus.PartiallyFilled;

    private static void ValidateRequest(PlaceOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol))
            throw new ArgumentException("Symbol is required.");

        if (request.Quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.Quantity), "Quantity must be greater than zero.");

        if (request.OrderType == OrderType.Limit)
        {
            if (request.Price is null)
                throw new ArgumentException("Limit order requires a price.");

            if (request.Price <= 0)
                throw new ArgumentOutOfRangeException(nameof(request.Price), "Price must be greater than zero.");
        }

        if (request.OrderType == OrderType.Market && request.Price is not null)
            throw new ArgumentException("Market order must not include a price.");
    }
}
