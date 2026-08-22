using ExchangeTest.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExchangeTest.Api.Controllers;

[ApiController]
[Route("api/demo")]
[AllowAnonymous]
public sealed class DemoController : ControllerBase
{
    private readonly InMemoryTradingStore _store;
    private readonly MockMarketPriceService _market;
    private readonly IWebHostEnvironment _environment;

    public DemoController(
        InMemoryTradingStore store,
        MockMarketPriceService market,
        IWebHostEnvironment environment)
    {
        _store = store;
        _market = market;
        _environment = environment;
    }

    [HttpPost("reset")]
    public IActionResult Reset()
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        _store.Orders.Clear();
        _store.Trades.Clear();
        _store.Positions.Clear();
        _store.PositionLots.Clear();

        _market.SetPrice("TXF202609", 23800m);
        _market.SetPrice("MTX202609", 23800m);

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
}
