using ExchangeTest.Api.Models;

namespace ExchangeTest.Api.Services;

public sealed class MatchingEngine
{
    private readonly InMemoryTradingStore _store;
    private readonly MockMarketPriceService _market;

    public MatchingEngine(InMemoryTradingStore store, MockMarketPriceService market)
    {
        _store = store;
        _market = market;
    }

    public Order PlaceOrder(Guid userId, PlaceOrderRequest request)
    {
        if (request.Quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.Quantity));

        if (request.OrderType == OrderType.Limit && request.Price is null)
            throw new ArgumentException("Limit order requires a price.");

        var contract = _store.Contracts.SingleOrDefault(x => x.Symbol.Equals(request.Symbol, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Unknown contract: {request.Symbol}");

        if (!contract.IsTradable)
            throw new InvalidOperationException("Contract is not tradable.");

        var order = new Order
        {
            UserId = userId,
            Symbol = contract.Symbol,
            Side = request.Side,
            OrderType = request.OrderType,
            LimitPrice = request.Price,
            Quantity = request.Quantity
        };

        _store.Orders.Add(order);
        Match(order);
        return order;
    }

    public void OnMarketPriceChanged(string symbol)
    {
        var pending = _store.Orders
            .Where(x => x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))
            .Where(x => x.Status is OrderStatus.Pending or OrderStatus.PartiallyFilled)
            .OrderBy(x => x.CreatedAt)
            .ToList();

        foreach (var order in pending)
            Match(order);
    }

    public void Cancel(Guid userId, Guid orderId)
    {
        var order = _store.Orders.SingleOrDefault(x => x.Id == orderId)
            ?? throw new KeyNotFoundException("Order not found.");

        if (order.UserId != userId)
            throw new UnauthorizedAccessException();

        if (order.Status is not (OrderStatus.Pending or OrderStatus.PartiallyFilled))
            throw new InvalidOperationException("Only pending or partially filled orders can be cancelled.");

        order.Status = OrderStatus.Cancelled;
        order.CompletedAt = DateTimeOffset.UtcNow;
    }

    private void Match(Order incoming)
    {
        var marketPrice = _market.GetPrice(incoming.Symbol);

        if (incoming.OrderType == OrderType.Market)
        {
            FillAgainstSyntheticLiquidity(incoming, marketPrice, incoming.Quantity - incoming.FilledQuantity);
            return;
        }

        var canExecute = incoming.Side == OrderSide.Buy
            ? marketPrice <= incoming.LimitPrice
            : marketPrice >= incoming.LimitPrice;

        if (!canExecute)
            return;

        var remaining = incoming.Quantity - incoming.FilledQuantity;
        FillAgainstSyntheticLiquidity(incoming, marketPrice, remaining);
    }

    private void FillAgainstSyntheticLiquidity(Order order, decimal executionPrice, int quantity)
    {
        // Prototype rule: current market price provides enough mock liquidity.
        // Real partial fills are produced when opposite user orders are added in the next iteration.
        if (quantity <= 0)
            return;

        order.FilledQuantity += quantity;
        order.Status = order.FilledQuantity == order.Quantity
            ? OrderStatus.Filled
            : OrderStatus.PartiallyFilled;

        if (order.Status == OrderStatus.Filled)
            order.CompletedAt = DateTimeOffset.UtcNow;
    }
}
