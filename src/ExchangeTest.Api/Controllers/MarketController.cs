using ExchangeTest.Api.Models;
using ExchangeTest.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExchangeTest.Api.Controllers;

[ApiController]
[Route("api/mock-market")]
public sealed class MarketController : ControllerBase
{
    private readonly MockMarketPriceService _market;
    private readonly MatchingEngine _matchingEngine;

    public MarketController(MockMarketPriceService market, MatchingEngine matchingEngine)
    {
        _market = market;
        _matchingEngine = matchingEngine;
    }

    [HttpGet("{symbol}/price")]
    public ActionResult<object> GetPrice(string symbol)
    {
        return Ok(new { symbol, price = _market.GetPrice(symbol) });
    }

    [HttpPut("{symbol}/price")]
    public ActionResult<object> SetPrice(string symbol, [FromBody] UpdateMockPriceRequest request)
    {
        _market.SetPrice(symbol, request.Price);
        _matchingEngine.OnMarketPriceChanged(symbol);
        return Ok(new { symbol, price = request.Price });
    }
}
