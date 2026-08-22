using ExchangeTest.Api.Models;
using ExchangeTest.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExchangeTest.Api.Controllers;

[ApiController]
[Route("api/demo")]
[AllowAnonymous]
public sealed class DemoController : ControllerBase
{
    private static readonly Guid UserB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserC = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid UserD = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly InMemoryTradingStore _store;
    private readonly MockMarketPriceService _market;
    private readonly MatchingEngine _matchingEngine;
    private readonly IWebHostEnvironment _environment;

    public DemoController(
        InMemoryTradingStore store,
        MockMarketPriceService market,
        MatchingEngine matchingEngine,
        IWebHostEnvironment environment)
    {
        _store = store;
        _market = market;
        _matchingEngine = matchingEngine;
        _environment = environment;
    }

    [HttpPost("reset")]
    public IActionResult Reset()
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        ResetState();

        return Ok(new
        {
            message = "Demo state reset.",
            marketPrices = new
            {
                TXF202609 = 23800m,
                MTX202609 = 23800m
            }
        });
    }

    [HttpPost("seed/sell-book")]
    public IActionResult SeedSellBook()
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        ResetState();

        var orders = new[]
        {
            _matchingEngine.PlaceOrder(UserB, Limit("TXF202609", OrderSide.Sell, 23798m, 2)),
            _matchingEngine.PlaceOrder(UserC, Limit("TXF202609", OrderSide.Sell, 23799m, 3)),
            _matchingEngine.PlaceOrder(UserD, Limit("TXF202609", OrderSide.Sell, 23800m, 5))
        };

        return Ok(new
        {
            message = "Sell book seeded. Login as A and submit a Buy order to test price-time priority and partial fills.",
            marketPrice = _market.GetPrice("TXF202609"),
            orders
        });
    }

    [HttpPost("seed/buy-book")]
    public IActionResult SeedBuyBook()
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        ResetState();

        var orders = new[]
        {
            _matchingEngine.PlaceOrder(UserB, Limit("TXF202609", OrderSide.Buy, 23802m, 2)),
            _matchingEngine.PlaceOrder(UserC, Limit("TXF202609", OrderSide.Buy, 23801m, 3)),
            _matchingEngine.PlaceOrder(UserD, Limit("TXF202609", OrderSide.Buy, 23800m, 5))
        };

        return Ok(new
        {
            message = "Buy book seeded. Login as A and submit a Sell order to test price-time priority and partial fills.",
            marketPrice = _market.GetPrice("TXF202609"),
            orders
        });
    }

    private void ResetState()
    {
        _store.Orders.Clear();
        _store.Trades.Clear();
        _store.Positions.Clear();
        _store.PositionLots.Clear();

        _market.SetPrice("TXF202609", 23800m);
        _market.SetPrice("MTX202609", 23800m);
    }

    private static PlaceOrderRequest Limit(string symbol, OrderSide side, decimal price, int quantity)
        => new()
        {
            Symbol = symbol,
            Side = side,
            OrderType = OrderType.Limit,
            Price = price,
            Quantity = quantity
        };
}
