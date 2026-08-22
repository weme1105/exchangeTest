using ExchangeTest.Api.Models;
using ExchangeTest.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExchangeTest.Api.Controllers;

[ApiController]
[Route("api/contracts")]
[Authorize]
public sealed class ContractsController : ControllerBase
{
    private readonly InMemoryTradingStore _store;

    public ContractsController(InMemoryTradingStore store)
    {
        _store = store;
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<FuturesContract>> GetContracts()
    {
        return Ok(_store.Contracts
            .OrderBy(x => x.Symbol)
            .ToList());
    }

    [HttpGet("{symbol}")]
    public ActionResult<FuturesContract> GetContract(string symbol)
    {
        var contract = _store.Contracts.SingleOrDefault(x =>
            x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));

        return contract is null ? NotFound() : Ok(contract);
    }
}
