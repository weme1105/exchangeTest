using ExchangeTest.Api.Models;
using ExchangeTest.Api.Services;

namespace ExchangeTest.Api.Tests;

public sealed class MatchingEngineTests
{
    private static readonly Guid UserA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void MarketOrder_WhenLiquidityIsInsufficient_CancelsRemainder()
    {
        var store = new InMemoryTradingStore();
        var market = new MockMarketPriceService();
        var positions = new PositionService(store);
        var engine = new MatchingEngine(store, market, positions);

        engine.PlaceOrder(UserB, new PlaceOrderRequest
        {
            Symbol = "TXF202609",
            Side = OrderSide.Sell,
            OrderType = OrderType.Limit,
            Price = 23800m,
            Quantity = 5
        });

        var marketBuy = engine.PlaceOrder(UserA, new PlaceOrderRequest
        {
            Symbol = "TXF202609",
            Side = OrderSide.Buy,
            OrderType = OrderType.Market,
            Quantity = 10
        });

        Assert.Equal(5, marketBuy.FilledQuantity);
        Assert.Equal(5, marketBuy.CancelledQuantity);
        Assert.Equal(0, marketBuy.RemainingQuantity);
        Assert.Equal(OrderStatus.PartiallyFilledCancelled, marketBuy.Status);
        Assert.Single(store.Trades);
        Assert.Equal(5, store.Trades[0].Quantity);
    }

    [Fact]
    public void LimitOrder_WhenLiquidityIsInsufficient_RemainsPartiallyFilled()
    {
        var store = new InMemoryTradingStore();
        var market = new MockMarketPriceService();
        var positions = new PositionService(store);
        var engine = new MatchingEngine(store, market, positions);

        engine.PlaceOrder(UserB, new PlaceOrderRequest
        {
            Symbol = "TXF202609",
            Side = OrderSide.Sell,
            OrderType = OrderType.Limit,
            Price = 23800m,
            Quantity = 5
        });

        var limitBuy = engine.PlaceOrder(UserA, new PlaceOrderRequest
        {
            Symbol = "TXF202609",
            Side = OrderSide.Buy,
            OrderType = OrderType.Limit,
            Price = 23800m,
            Quantity = 10
        });

        Assert.Equal(5, limitBuy.FilledQuantity);
        Assert.Equal(0, limitBuy.CancelledQuantity);
        Assert.Equal(5, limitBuy.RemainingQuantity);
        Assert.Equal(OrderStatus.PartiallyFilled, limitBuy.Status);
    }
}
