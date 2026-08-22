using ExchangeTest.Api.Extensions;
using ExchangeTest.Api.Models;
using ExchangeTest.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExchangeTest.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public sealed class OrdersController : ControllerBase
{
    private readonly InMemoryTradingStore _store;
    private readonly MatchingEngine _matchingEngine;
    private readonly MockMarketPriceService _market;

    public OrdersController(
        InMemoryTradingStore store,
        MatchingEngine matchingEngine,
        MockMarketPriceService market)
    {
        _store = store;
        _matchingEngine = matchingEngine;
        _market = market;
    }

    [HttpPost]
    public ActionResult<PlaceOrderResponse> PlaceOrder([FromBody] PlaceOrderRequest request)
    {
        var userId = User.GetUserId();
        var positionWarning = BuildPositionWarning(userId, request.Symbol);
        var order = _matchingEngine.PlaceOrder(userId, request);

        return Ok(new PlaceOrderResponse
        {
            OrderId = order.Id,
            Symbol = order.Symbol,
            Side = order.Side,
            OrderType = order.OrderType,
            RequestedQuantity = order.Quantity,
            FilledQuantity = order.FilledQuantity,
            CancelledQuantity = order.CancelledQuantity,
            RemainingQuantity = order.RemainingQuantity,
            Status = order.Status,
            Message = BuildMessage(order),
            PositionWarning = positionWarning
        });
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<Order>> GetOrders([FromQuery] OrderStatus? status = null)
    {
        var userId = User.GetUserId();

        var query = _store.Orders
            .Where(x => x.UserId == userId);

        if (status is not null)
            query = query.Where(x => x.Status == status);

        return Ok(query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToList());
    }

    [HttpGet("{orderId:guid}")]
    public ActionResult<Order> GetOrder(Guid orderId)
    {
        var userId = User.GetUserId();
        var order = _store.Orders.SingleOrDefault(x => x.Id == orderId && x.UserId == userId);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("{orderId:guid}/cancel")]
    public ActionResult<Order> Cancel(Guid orderId)
    {
        var userId = User.GetUserId();
        _matchingEngine.Cancel(userId, orderId);

        var order = _store.Orders.Single(x => x.Id == orderId && x.UserId == userId);
        return Ok(order);
    }

    private PositionRiskWarning? BuildPositionWarning(Guid userId, string symbol)
    {
        var position = _store.Positions.SingleOrDefault(x =>
            x.UserId == userId &&
            x.Quantity > 0 &&
            x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));

        if (position is null)
            return null;

        var contract = _store.Contracts.Single(x =>
            x.Symbol.Equals(position.Symbol, StringComparison.OrdinalIgnoreCase));
        var currentPrice = _market.GetPrice(position.Symbol);
        var pointDifference = position.Side == PositionSide.Long
            ? currentPrice - position.AveragePrice
            : position.AveragePrice - currentPrice;
        var unrealizedPnL = pointDifference * contract.PointValue * position.Quantity;

        return new PositionRiskWarning
        {
            Side = position.Side,
            Quantity = position.Quantity,
            AveragePrice = position.AveragePrice,
            CurrentPrice = currentPrice,
            UnrealizedProfitLoss = unrealizedPnL,
            Message = $"目前持有 {position.Side} {position.Quantity} 口，未實現損益 {unrealizedPnL:N0}。反向委託成交時可能形成平倉。"
        };
    }

    private static string BuildMessage(Order order)
    {
        return order.Status switch
        {
            OrderStatus.Filled => $"委託 {order.Quantity} 口，已全部成交。",
            OrderStatus.PartiallyFilled => $"委託 {order.Quantity} 口，目前已成交 {order.FilledQuantity} 口，剩餘 {order.RemainingQuantity} 口持續掛單。",
            OrderStatus.PartiallyFilledCancelled => $"委託 {order.Quantity} 口，僅成交 {order.FilledQuantity} 口，其餘 {order.CancelledQuantity} 口已取消。",
            OrderStatus.Cancelled when order.OrderType == OrderType.Market => $"委託 {order.Quantity} 口，目前無可成交數量，本次市價單已取消。",
            OrderStatus.Cancelled => $"委託已取消，取消數量 {order.CancelledQuantity} 口。",
            OrderStatus.Rejected => "委託遭拒絕。",
            _ => $"委託已建立，尚未成交，剩餘 {order.RemainingQuantity} 口。"
        };
    }
}
