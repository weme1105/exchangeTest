using ExchangeTest.Api.Models;
using ExchangeTest.Api.Services;
using Xunit;

namespace ExchangeTest.Api.Tests;

public sealed class MatchingEngineTests
{
    private static readonly Guid UserA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static (InMemoryTradingStore Store, MockMarketPriceService Market, MatchingEngine Engine) CreateEngine()
    {
        var store = new InMemoryTradingStore();
        var market = new MockMarketPriceService();
        var positions = new PositionService(store);
        return (store, market, new MatchingEngine(store, market, positions));
    }

    [Fact]
    public void MarketOrder_WhenLiquidityIsInsufficient_CancelsRemainder()
    {
        var (store, _, engine) = CreateEngine();
        engine.PlaceOrder(UserB, new PlaceOrderRequest { Symbol = "TXF202609", Side = OrderSide.Sell, OrderType = OrderType.Limit, Price = 23800m, Quantity = 5 });

        var marketBuy = engine.PlaceOrder(UserA, new PlaceOrderRequest { Symbol = "TXF202609", Side = OrderSide.Buy, OrderType = OrderType.Market, Quantity = 10 });

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
        var (_, _, engine) = CreateEngine();
        engine.PlaceOrder(UserB, new PlaceOrderRequest { Symbol = "TXF202609", Side = OrderSide.Sell, OrderType = OrderType.Limit, Price = 23800m, Quantity = 5 });

        var limitBuy = engine.PlaceOrder(UserA, new PlaceOrderRequest { Symbol = "TXF202609", Side = OrderSide.Buy, OrderType = OrderType.Limit, Price = 23800m, Quantity = 10 });

        Assert.Equal(5, limitBuy.FilledQuantity);
        Assert.Equal(0, limitBuy.CancelledQuantity);
        Assert.Equal(5, limitBuy.RemainingQuantity);
        Assert.Equal(OrderStatus.PartiallyFilled, limitBuy.Status);
    }

    [Fact]
    public void OppositeOrders_FromSameUser_DoNotSelfTrade()
    {
        var (store, _, engine) = CreateEngine();
        var sell = engine.PlaceOrder(UserA, new PlaceOrderRequest { Symbol = "TXF202609", Side = OrderSide.Sell, OrderType = OrderType.Limit, Price = 23800m, Quantity = 5 });
        var buy = engine.PlaceOrder(UserA, new PlaceOrderRequest { Symbol = "TXF202609", Side = OrderSide.Buy, OrderType = OrderType.Limit, Price = 23800m, Quantity = 5 });

        Assert.Empty(store.Trades);
        Assert.Equal(OrderStatus.Pending, sell.Status);
        Assert.Equal(OrderStatus.Pending, buy.Status);
        Assert.Equal(5, sell.RemainingQuantity);
        Assert.Equal(5, buy.RemainingQuantity);
    }

    [Fact]
    public void Position_WhenOppositeFillExceedsHolding_ClosesFifoThenReverses()
    {
        var (store, market, engine) = CreateEngine();

        // A opens Long 2 @ 23800.
        engine.PlaceOrder(UserB, new PlaceOrderRequest { Symbol = "TXF202609", Side = OrderSide.Sell, OrderType = OrderType.Limit, Price = 23800m, Quantity = 2 });
        engine.PlaceOrder(UserA, new PlaceOrderRequest { Symbol = "TXF202609", Side = OrderSide.Buy, OrderType = OrderType.Market, Quantity = 2 });

        // A adds Long 1 @ 23900, creating a second FIFO lot.
        market.SetPrice("TXF202609", 23900m);
        engine.PlaceOrder(UserB, new PlaceOrderRequest { Symbol = "TXF202609", Side = OrderSide.Sell, OrderType = OrderType.Limit, Price = 23900m, Quantity = 1 });
        engine.PlaceOrder(UserA, new PlaceOrderRequest { Symbol = "TXF202609", Side = OrderSide.Buy, OrderType = OrderType.Market, Quantity = 1 });

        // A sells 5 @ 24000: close Long 2 @ 23800, then Long 1 @ 23900, then open Short 2 @ 24000.
        market.SetPrice("TXF202609", 24000m);
        engine.PlaceOrder(UserB, new PlaceOrderRequest { Symbol = "TXF202609", Side = OrderSide.Buy, OrderType = OrderType.Limit, Price = 24000m, Quantity = 5 });
        var reverse = engine.PlaceOrder(UserA, new PlaceOrderRequest { Symbol = "TXF202609", Side = OrderSide.Sell, OrderType = OrderType.Market, Quantity = 5 });

        Assert.Equal(OrderStatus.Filled, reverse.Status);

        var position = Assert.Single(store.Positions.Where(x => x.UserId == UserA && x.Symbol == "TXF202609"));
        Assert.Equal(PositionSide.Short, position.Side);
        Assert.Equal(2, position.Quantity);
        Assert.Equal(24000m, position.AveragePrice);
        Assert.Equal(100000m, position.RealizedProfitLoss); // (200*2 + 100*1) * 200

        var activeLots = store.PositionLots.Where(x => x.UserId == UserA && x.RemainingQuantity > 0).ToList();
        var shortLot = Assert.Single(activeLots);
        Assert.Equal(PositionSide.Short, shortLot.Side);
        Assert.Equal(2, shortLot.RemainingQuantity);
        Assert.Equal(24000m, shortLot.OpenPrice);
    }
}
