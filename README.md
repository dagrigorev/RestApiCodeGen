# RestApiCodeGen

Ready-to-use ASP.NET Core 8 OpenAPI runtime server generator.

What it does:
- accepts an uploaded OpenAPI/Swagger file
- parses operations, request contracts, and response contracts
- exposes the uploaded API immediately under `/generated/{apiId}`
- returns callable URLs and contract metadata

Main project:
- `SwaggerApiGenerator/SwaggerApiGenerator`

Read first:
- `SwaggerApiGenerator/DESIGN.md`
- `SwaggerApiGenerator/SwaggerApiGenerator/README.md`
