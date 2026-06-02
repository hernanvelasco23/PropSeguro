# PropSeguro

PropSeguro - Segunda opinion inteligente para compradores inmobiliarios en Argentina.

## MVP vertical slice

This repository currently implements the first backend slice: create and retrieve a property analysis.

The API accepts:

- Property title.
- Listing URL.
- Address.
- Neighborhood.
- Price in USD.
- Monthly expenses.
- Optional notes.

The API returns:

- Analysis id.
- Initial deterministic risk score.
- Basic risk findings.
- Missing documentation checklist.

No authentication, payments, document uploads, AI agents, or frontend are implemented yet.

## Architecture

- `src/PropSeguro.Domain`: domain entities and deterministic analysis rules.
- `src/PropSeguro.Application`: CQRS commands, queries, DTOs, and application abstractions.
- `src/PropSeguro.Infrastructure`: EF Core DbContext, SQL Server configuration, and migrations.
- `src/PropSeguro.Api`: ASP.NET Core Web API endpoints and Swagger.

## Requirements

- .NET SDK.
- SQL Server or LocalDB.

The planning docs requested .NET 9. This workspace currently has .NET 8 installed, so the projects target `net8.0` for local buildability. Upgrade the target frameworks to `net9.0` after installing the .NET 9 SDK.

## Run locally

The development connection string uses SQL Server LocalDB with Windows Integrated Security by default:

```text
Server=(localdb)\MSSQLLocalDB;Database=PropSeguroDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
```

You can change `DefaultConnection` in `src/PropSeguro.Api/appsettings.Development.json` if you use a different local SQL Server instance. Avoid using `sa` credentials for local development.

Create or update the local database by applying the EF Core migration:

```powershell
dotnet ef database update --project src/PropSeguro.Infrastructure --startup-project src/PropSeguro.Api
```

If `dotnet ef` is not installed, install it with:

```powershell
dotnet tool install --global dotnet-ef
```

Run the API:

```powershell
dotnet run --project src/PropSeguro.Api --launch-profile http
```

Open Swagger at the URL shown in the console, usually:

```text
http://localhost:5288/swagger
```

## Example request

```http
POST /api/property-analyses
Content-Type: application/json

{
  "title": "Departamento 3 ambientes en Palermo",
  "listingUrl": "https://example.com/listing/123",
  "address": "Av. Santa Fe 3200",
  "neighborhood": "Palermo",
  "priceUsd": 180000,
  "monthlyExpenses": 95000,
  "notes": "Visita realizada el sabado."
}
```
