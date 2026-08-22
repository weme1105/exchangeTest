using ExchangeTest.Api.Extensions;
using ExchangeTest.Api.Models;
using ExchangeTest.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExchangeTest.Api.Controllers;

[ApiController]
[Route("api/trades")]
[Authorize]
public sealed class TradesController : ControllerBase
{
    private readonly InMemoryTradingStore _store;

    public TradesController(InMemoryTradingStore store)
    {
        _store = store;
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<Trade>> GetTrades([FromQuery] string? symbol = null)
    {
        var userId = User.GetUserId();

        var query = _store.Trades.Where(x =>
            x.BuyerUserId == userId || x.SellerUserId == userId);

        if (!string.IsNullOrWhiteSpace(symbol))
            query = query.Where(x => x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));

        return Ok(query
            .OrderByDescending(x => x.ExecutedAt)
            .ThenByDescending(x => x.Id)
            .ToList());
    }

    [HttpGet("{tradeId:guid}")]
    public ActionResult<Trade> GetTrade(Guid tradeId)
    {
        var userId = User.GetUserId();
        var trade = _store.Trades.SingleOrDefault(x =>
            x.Id == tradeId &&
            (x.BuyerUserId == userId || x.SellerUserId == userId));

        return trade is null ? NotFound() : Ok(trade);
    }
}
