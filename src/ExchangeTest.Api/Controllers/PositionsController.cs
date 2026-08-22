using ExchangeTest.Api.Extensions;
using ExchangeTest.Api.Models;
using ExchangeTest.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExchangeTest.Api.Controllers;

[ApiController]
[Route("api/positions")]
[Authorize]
public sealed class PositionsController : ControllerBase
{
    private readonly InMemoryTradingStore _store;
    private readonly MockMarketPriceService _market;

    public PositionsController(InMemoryTradingStore store, MockMarketPriceService market)
    {
        _store = store;
        _market = market;
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<PositionResponse>> GetOpenPositions()
    {
        var userId = User.GetUserId();

        var result = _store.Positions
            .Where(x => x.UserId == userId && x.Quantity > 0)
            .OrderBy(x => x.Symbol)
            .Select(ToResponse)
            .ToList();

        return Ok(result);
    }

    [HttpGet("{symbol}")]
    public ActionResult<PositionResponse> GetOpenPosition(string symbol)
    {
        var userId = User.GetUserId();
        var position = _store.Positions.SingleOrDefault(x =>
            x.UserId == userId &&
            x.Quantity > 0 &&
            x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));

        return position is null ? NotFound() : Ok(ToResponse(position));
    }

    [HttpGet("{symbol}/lots")]
    public ActionResult<IReadOnlyCollection<PositionLot>> GetOpenLots(string symbol)
    {
        var userId = User.GetUserId();

        var lots = _store.PositionLots
            .Where(x => x.UserId == userId)
            .Where(x => x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))
            .Where(x => x.RemainingQuantity > 0)
            .OrderBy(x => x.OpenedAt)
            .ThenBy(x => x.Id)
            .ToList();

        return Ok(lots);
    }

    private PositionResponse ToResponse(Position position)
    {
        var currentPrice = _market.GetPrice(position.Symbol);
        var contract = _store.Contracts.Single(x =>
            x.Symbol.Equals(position.Symbol, StringComparison.OrdinalIgnoreCase));

        var pointDifference = position.Side == PositionSide.Long
            ? currentPrice - position.AveragePrice
            : position.AveragePrice - currentPrice;

        return new PositionResponse
        {
            Id = position.Id,
            Symbol = position.Symbol,
            Side = position.Side,
            Quantity = position.Quantity,
            AveragePrice = position.AveragePrice,
            CurrentPrice = currentPrice,
            UnrealizedProfitLoss = pointDifference * contract.PointValue * position.Quantity,
            RealizedProfitLoss = position.RealizedProfitLoss
        };
    }
}
