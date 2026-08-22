# exchangeTest

Futures trading API prototype for testing order placement, partial fills, FIFO position closing, trade history, and mock market prices through Swagger.

## Current rules

- User identity comes from JWT `NameIdentifier`.
- Development mode provides mock JWTs for users `A`, `B`, `C`, and `D`.
- Order types: `Market`, `Limit`.
- Order statuses: `Pending`, `PartiallyFilled`, `Filled`, `Cancelled`, `Rejected`, `PartiallyFilledCancelled`.
- Limit orders use current mock market price as the execution price when the limit condition is satisfied.
- Available fill quantity is determined by opposite-side open orders, not synthetic unlimited liquidity.
- Matching priority is price first, then creation time.
  - Incoming Buy: eligible Sell orders are considered from lower limit price to higher limit price.
  - Incoming Sell: eligible Buy orders are considered from higher limit price to lower limit price.
- Self-trade is prohibited: an order never matches another open order from the same user.
- Market orders are immediate-only for this demo. If available opposite liquidity is insufficient, the filled quantity is kept and the unfilled remainder is cancelled immediately.
- Limit orders may remain `PartiallyFilled` with their remaining quantity still open.
- Every fill creates a `Trade`.
- Every trade updates both users' positions immediately.
- Position closing uses FIFO lots.
- `Position` is a current aggregate snapshot; `PositionLot` preserves the open lots used by FIFO.
- When a user already has an open position in the same contract, the order response includes current unrealized PnL as a risk warning.

## Run

```bash
dotnet run --project src/ExchangeTest.Api/ExchangeTest.Api.csproj
```

Open Swagger:

```text
http://localhost:5098/swagger
```

## Swagger test flow

1. `POST /api/demo/reset` if you want a clean state.
2. `POST /api/dev-auth/token/A` and copy `accessToken`.
3. Click Swagger `Authorize` and paste the token.
4. Create an order for user A.
5. Generate a token for `B`, `C`, or `D`, authorize with it, then create opposite orders to test matching.
6. Inspect:
   - `GET /api/order-book/{symbol}`
   - `GET /api/orders`
   - `GET /api/trades`
   - `GET /api/positions`
   - `GET /api/positions/{symbol}/lots`
7. Change market price with `PUT /api/mock-market/{symbol}/price` to trigger re-evaluation of pending limit orders.

## Core endpoints

```text
POST /api/dev-auth/token/{userKey}
POST /api/demo/reset

POST /api/orders
GET  /api/orders
GET  /api/orders/{orderId}
POST /api/orders/{orderId}/cancel

GET  /api/order-book/{symbol}
GET  /api/trades
GET  /api/positions
GET  /api/positions/{symbol}
GET  /api/positions/{symbol}/lots

GET  /api/contracts
GET  /api/contracts/{symbol}
GET  /api/mock-market/{symbol}/price
PUT  /api/mock-market/{symbol}/price
```
