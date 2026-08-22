namespace ExchangeTest.Api.Services;

public sealed class MockMarketPriceService
{
    private readonly Dictionary<string, decimal> _prices = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TXF202609"] = 23800m,
        ["MTX202609"] = 23800m
    };

    public decimal GetPrice(string symbol)
    {
        if (!_prices.TryGetValue(symbol, out var price))
            throw new KeyNotFoundException($"Unknown symbol: {symbol}");

        return price;
    }

    public void SetPrice(string symbol, decimal price)
    {
        if (price <= 0)
            throw new ArgumentOutOfRangeException(nameof(price));

        _prices[symbol] = price;
    }
}
