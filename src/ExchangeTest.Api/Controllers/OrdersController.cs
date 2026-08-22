using ExchangeTest.Api.Models;
using ExchangeTest.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExchangeTest.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private static readonly Guid MockUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
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
        // TODO: replace MockUserId with JWT claim once authentication is added.
        var order = _matchingEngine.PlaceOrder(MockUserId, request);
        return Ok(order);
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<Order>> GetOrders()
    {
        return Ok(_store.Orders
            .Where(x => x.UserId == MockUserId)
            .OrderByDescending(x => x.CreatedAt));
    }

    [HttpPost("{orderId:guid}/cancel")]
    public IActionResult Cancel(Guid orderId)
    {
        _matchingEngine.Cancel(MockUserId, orderId);
        return NoContent();
    }
}
