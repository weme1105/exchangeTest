namespace ExchangeTest.Api.Models;

public enum OrderSide
{
    Buy = 1,
    Sell = 2
}

public enum OrderType
{
    Market = 1,
    Limit = 2
}

public enum OrderStatus
{
    Pending = 1,
    PartiallyFilled = 2,
    Filled = 3,
    Cancelled = 4,
    Rejected = 5
}

public enum PositionSide
{
    Long = 1,
    Short = 2
}

public sealed class PlaceOrderRequest
{
    public required string Symbol { get; init; }
    public required OrderSide Side { get; init; }
    public required OrderType OrderType { get; init; }
    public decimal? Price { get; init; }
    public required int Quantity { get; init; }
}

public sealed class Order
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public required string Symbol { get; init; }
    public required OrderSide Side { get; init; }
    public required OrderType OrderType { get; init; }
    public decimal? LimitPrice { get; init; }
    public required int Quantity { get; init; }
    public int FilledQuantity { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class Trade
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid BuyOrderId { get; init; }
    public required Guid SellOrderId { get; init; }
    public required Guid BuyerUserId { get; init; }
    public required Guid SellerUserId { get; init; }
    public required string Symbol { get; init; }
    public required decimal Price { get; init; }
    public required int Quantity { get; init; }
    public DateTimeOffset ExecutedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class Position
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid UserId { get; init; }
    public required string Symbol { get; init; }
    public required PositionSide Side { get; set; }
    public required int Quantity { get; set; }
    public required decimal AveragePrice { get; set; }
    public decimal RealizedProfitLoss { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PositionLot
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid UserId { get; init; }
    public required string Symbol { get; init; }
    public required PositionSide Side { get; init; }
    public required decimal OpenPrice { get; init; }
    public required int OriginalQuantity { get; init; }
    public int RemainingQuantity { get; set; }
    public DateTimeOffset OpenedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class FuturesContract
{
    public required string Symbol { get; init; }
    public required string Name { get; init; }
    public required decimal PointValue { get; init; }
    public required decimal TickSize { get; init; }
    public required DateOnly ExpirationDate { get; init; }
    public bool IsTradable { get; init; } = true;
}

public sealed class UpdateMockPriceRequest
{
    public required decimal Price { get; init; }
}
