# Swagger API Generator

Swagger API Generator is an ASP.NET Core 8 service that accepts an OpenAPI or Swagger file, registers it at runtime, and exposes a callable generated API under a stable URI prefix.

## What you get

After uploading a spec, the service returns:
- `apiId`
- generated API base URL
- contracts URL with methods, parameters, request body and response details
- original uploaded specification URL
- concrete callable URLs for every operation

Example:
- original path in spec: `/items`
- generated base URL: `/generated/api-123...`
- callable runtime URL: `/generated/api-123.../items`

## Endpoints

### Management

- `POST /api/specifications/upload`
- `GET /api/specifications`
- `GET /api/specifications/{apiId}`
- `GET /api/specifications/{apiId}/contracts`
- `GET /api/specifications/{apiId}/openapi`
- `DELETE /api/specifications/{apiId}`

### Generated runtime API

- `ANY /generated/{apiId}/...`

## Run locally

From the solution folder:

```bash
dotnet restore
dotnet build
dotnet run --project SwaggerApiGenerator/SwaggerApiGenerator.csproj
```

Then open:
- Swagger UI: `https://localhost:5001/swagger` or the port printed by ASP.NET
- Root info: `https://localhost:5001/`

## Upload an OpenAPI file

```bash
curl -X POST "https://localhost:5001/api/specifications/upload" \
  -F "file=@../../simple-api.json" \
  -k
```

The response will include:
- `apiId`
- `baseUrl`
- `contractsUrl`
- `specificationUrl`
- operation URLs

## Call the generated API

Example generated call:

```bash
curl "https://localhost:5001/generated/{apiId}/items" -k
```

If the operation requires route values, query parameters, headers, or request body, the runtime checks required values and returns a helpful `400` when required pieces are missing.

## Notes

This version is designed to be reliable at runtime first.

It is a runtime mock server generated from OpenAPI contracts. It does not create fully implemented business logic from the specification. Instead, it returns deterministic example responses derived from the response schemas.
