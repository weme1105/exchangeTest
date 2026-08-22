using ExchangeTest.Api.Models;
using ExchangeTest.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExchangeTest.Api.Controllers;

[ApiController]
[Route("api/order-book")]
[Authorize]
public sealed class OrderBookController : ControllerBase
{
    private readonly InMemoryTradingStore _store;

    public OrderBookController(InMemoryTradingStore store)
    {
        _store = store;
    }

    [HttpGet("{symbol}")]
    public IActionResult Get(string symbol)
    {
        var openOrders = _store.Orders
            .Where(x => x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))
            .Where(x => x.Status is OrderStatus.Pending or OrderStatus.PartiallyFilled)
            .ToList();

        var buys = openOrders
            .Where(x => x.Side == OrderSide.Buy)
            .OrderByDescending(x => x.OrderType == OrderType.Market)
            .ThenByDescending(x => x.LimitPrice)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Select(ToBookOrder)
            .ToList();

        var sells = openOrders
            .Where(x => x.Side == OrderSide.Sell)
            .OrderByDescending(x => x.OrderType == OrderType.Market)
            .ThenBy(x => x.LimitPrice)
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Select(ToBookOrder)
            .ToList();

        return Ok(new
        {
            symbol = symbol.ToUpperInvariant(),
            buys,
            sells
        });
    }

    private static object ToBookOrder(Order order) => new
    {
        order.Id,
        order.UserId,
        order.Side,
        order.OrderType,
        order.LimitPrice,
        order.Quantity,
        order.FilledQuantity,
        order.RemainingQuantity,
        order.Status,
        order.CreatedAt
    };
}
