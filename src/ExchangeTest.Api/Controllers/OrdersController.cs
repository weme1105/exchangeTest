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

    public OrdersController(InMemoryTradingStore store, MatchingEngine matchingEngine)
    {
        _store = store;
        _matchingEngine = matchingEngine;
    }

    [HttpPost]
    public ActionResult<Order> PlaceOrder([FromBody] PlaceOrderRequest request)
    {
        var userId = User.GetUserId();
        var order = _matchingEngine.PlaceOrder(userId, request);
        return Ok(order);
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
    public IActionResult Cancel(Guid orderId)
    {
        _matchingEngine.Cancel(User.GetUserId(), orderId);
        return NoContent();
    }
}
