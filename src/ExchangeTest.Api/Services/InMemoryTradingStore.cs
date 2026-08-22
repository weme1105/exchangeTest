using ExchangeTest.Api.Models;

namespace ExchangeTest.Api.Services;

public sealed class InMemoryTradingStore
{
    public List<Order> Orders { get; } = [];
    public List<Trade> Trades { get; } = [];
    public List<Position> Positions { get; } = [];
    public List<PositionLot> PositionLots { get; } = [];

    public List<FuturesContract> Contracts { get; } =
    [
        new()
        {
            Symbol = "TXF202609",
            Name = "TAIEX Futures 2026/09",
            PointValue = 200m,
            TickSize = 1m,
            ExpirationDate = new DateOnly(2026, 9, 16)
        },
        new()
        {
            Symbol = "MTX202609",
            Name = "Mini-TAIEX Futures 2026/09",
            PointValue = 50m,
            TickSize = 1m,
            ExpirationDate = new DateOnly(2026, 9, 16)
        }
    ];
}
