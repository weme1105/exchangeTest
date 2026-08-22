FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/ExchangeTest.Api/ExchangeTest.Api.csproj src/ExchangeTest.Api/
RUN dotnet restore src/ExchangeTest.Api/ExchangeTest.Api.csproj

COPY . .
RUN dotnet publish src/ExchangeTest.Api/ExchangeTest.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Development
EXPOSE 8080

ENTRYPOINT ["dotnet", "ExchangeTest.Api.dll"]
