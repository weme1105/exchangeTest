using ExchangeTest.Api.Models;

namespace ExchangeTest.Api.Services;

public sealed class PositionService
{
    private readonly InMemoryTradingStore _store;

    public PositionService(InMemoryTradingStore store)
    {
        _store = store;
    }

    public void ApplyTrade(
        Guid userId,
        string symbol,
        OrderSide side,
        decimal executionPrice,
        int quantity,
        Guid tradeId,
        decimal pointValue)
    {
        if (quantity <= 0)
            return;

        var openingSide = side == OrderSide.Buy
            ? PositionSide.Long
            : PositionSide.Short;

        var closingSide = openingSide == PositionSide.Long
            ? PositionSide.Short
            : PositionSide.Long;

        var remaining = quantity;
        decimal realizedDelta = 0m;

        var oppositeLots = _store.PositionLots
            .Where(x => x.UserId == userId)
            .Where(x => x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))
            .Where(x => x.Side == closingSide)
            .Where(x => x.RemainingQuantity > 0)
            .OrderBy(x => x.OpenedAt)
            .ThenBy(x => x.Id)
            .ToList();

        foreach (var lot in oppositeLots)
        {
            if (remaining == 0)
                break;

            var closingQuantity = Math.Min(remaining, lot.RemainingQuantity);

            var pointDifference = lot.Side == PositionSide.Long
                ? executionPrice - lot.OpenPrice
                : lot.OpenPrice - executionPrice;

            realizedDelta += pointDifference * lot.PointValueAtOpen * closingQuantity;
            lot.RemainingQuantity -= closingQuantity;
            remaining -= closingQuantity;
        }

        if (remaining > 0)
        {
            _store.PositionLots.Add(new PositionLot
            {
                OpenTradeId = tradeId,
                UserId = userId,
                Symbol = symbol,
                Side = openingSide,
                OpenPrice = executionPrice,
                PointValueAtOpen = pointValue,
                OriginalQuantity = remaining,
                RemainingQuantity = remaining
            });
        }

        RebuildPosition(userId, symbol, realizedDelta);
    }

    private void RebuildPosition(Guid userId, string symbol, decimal realizedDelta)
    {
        var activeLots = _store.PositionLots
            .Where(x => x.UserId == userId)
            .Where(x => x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))
            .Where(x => x.RemainingQuantity > 0)
            .OrderBy(x => x.OpenedAt)
            .ThenBy(x => x.Id)
            .ToList();

        var position = _store.Positions.SingleOrDefault(x =>
            x.UserId == userId &&
            x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));

        if (position is null)
        {
            position = new Position
            {
                UserId = userId,
                Symbol = symbol,
                Side = activeLots.FirstOrDefault()?.Side ?? PositionSide.Long,
                Quantity = 0,
                AveragePrice = 0m
            };
            _store.Positions.Add(position);
        }

        position.RealizedProfitLoss += realizedDelta;
        position.UpdatedAt = DateTimeOffset.UtcNow;

        if (activeLots.Count == 0)
        {
            position.Quantity = 0;
            position.AveragePrice = 0m;
            return;
        }

        // Net-position mode guarantees that all remaining lots have the same side.
        position.Side = activeLots[0].Side;
        position.Quantity = activeLots.Sum(x => x.RemainingQuantity);
        position.AveragePrice = activeLots.Sum(x => x.OpenPrice * x.RemainingQuantity) / position.Quantity;
    }
}
