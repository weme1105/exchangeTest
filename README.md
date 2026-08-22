# exchangeTest

Futures trading API prototype for testing order placement, partial fills, FIFO position closing, trade history, and mock market prices through Swagger.

## Current rules

- User identity comes from JWT `NameIdentifier`.
- Development mode provides mock JWTs for `user-a` and `user-b`.
- Order types: `Market`, `Limit`.
- Order statuses: `Pending`, `PartiallyFilled`, `Filled`, `Cancelled`, `Rejected`.
- Limit orders use current mock market price as the execution price when the limit condition is satisfied.
- Available fill quantity is determined by opposite-side open orders, not synthetic unlimited liquidity.
- Matching priority is price first, then creation time.
  - Incoming Buy: eligible Sell orders are considered from lower limit price to higher limit price.
  - Incoming Sell: eligible Buy orders are considered from higher limit price to lower limit price.
- Every fill creates a `Trade`.
- Every trade updates both users' positions immediately.
- Position closing uses FIFO lots.
- `Position` is a current aggregate snapshot; `PositionLot` preserves the open lots used by FIFO.

## Run

```bash
dotnet run --project src/ExchangeTest.Api/ExchangeTest.Api.csproj
```

Open:

```text
http://localhost:5098/swagger
```

## Swagger test flow

1. `POST /api/dev-auth/token/user-a` and copy `accessToken`.
2. Click Swagger `Authorize` and paste the token.
3. Create a Sell order for user A if desired.
4. Generate a token for `user-b`, authorize with it, then create the opposite order.
5. Inspect:
   - `GET /api/orders`
   - `GET /api/trades`
   - `GET /api/positions`
   - `GET /api/positions/{symbol}/lots`
6. Change market price with `PUT /api/mock-market/{symbol}/price` to trigger re-evaluation of pending orders.

## Core endpoints

```text
POST /api/orders
GET  /api/orders
GET  /api/orders/{orderId}
POST /api/orders/{orderId}/cancel
GET  /api/trades
GET  /api/positions
GET  /api/positions/{symbol}
GET  /api/positions/{symbol}/lots
GET  /api/contracts
GET  /api/contracts/{symbol}
GET  /api/mock-market/{symbol}/price
PUT  /api/mock-market/{symbol}/price
POST /api/dev-auth/token/{userKey}
```

## Pending market-order policy

A business rule still needs to be selected for the unfilled remainder of a Market order when opposite-side liquidity is insufficient.
