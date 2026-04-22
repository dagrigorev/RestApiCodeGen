# Design — OpenAPI Runtime Server Generator

## Goal

Build an ASP.NET Core service that:

1. accepts an uploaded OpenAPI specification
2. extracts routes, methods, request contracts and response contracts
3. exposes the uploaded API at a generated runtime prefix
4. returns a URI that can immediately be used to call the generated API

## Why the original approach was incomplete

The original project attempted to generate ASP.NET controllers from Swagger, compile them dynamically with Roslyn, and inject them into MVC after startup.

That is not a reliable way to make new routes callable in ASP.NET Core, because controller discovery and endpoint routing are built during application startup. Loading a new assembly later does not automatically produce stable runtime endpoints.

## Chosen architecture

This version uses a runtime-dispatch model.

### Management plane

Responsible for upload and inspection:

- `POST /api/specifications/upload`
- `GET /api/specifications`
- `GET /api/specifications/{apiId}`
- `GET /api/specifications/{apiId}/contracts`
- `GET /api/specifications/{apiId}/openapi`
- `DELETE /api/specifications/{apiId}`

### Runtime plane

Responsible for handling generated API traffic:

- `ANY /generated/{apiId}/...`

This is implemented by a catch-all dispatcher that matches by `apiId`, HTTP method, and original OpenAPI path template.

## Components

### OpenApiRegistry

Responsibilities:
- persist uploaded specifications to `GeneratedApis/{apiId}`
- parse JSON or YAML OpenAPI files
- extract operations, parameters, request bodies, and responses
- generate runtime path templates
- reload persisted APIs at startup
- delete registered APIs

### OpenApiExampleFactory

Builds example response payloads from JSON schema definitions.

### SpecificationsController

Management API for upload, listing, inspection, downloading the original spec, and deleting generated APIs.

### GeneratedApiController

Catch-all runtime dispatcher.

Responsibilities:
- resolve uploaded API by `apiId`
- match request to an OpenAPI operation
- extract route values
- validate required path/query/header parameters and required request body presence
- return a response envelope with metadata and generated example payload

## URL model

If uploaded spec contains:
- `GET /items`
- `POST /items/{id}`

and generated `apiId = api-abc123`

then runtime endpoints become:
- `GET /generated/api-abc123/items`
- `POST /generated/api-abc123/items/{id}`

## Current behavior

This project is a runtime API simulator and contract host.

It does not emit a brand-new compiled ASP.NET project per upload. Instead, it hosts generated APIs inside the running process and returns stable callable URLs immediately.

That makes it useful for:
- frontend mocking
- integration prototyping
- contract-first backend simulation
- API review and testing

## Future extensions

Possible next steps:
1. strict schema validation for request body and query values
2. stateful fake persistence for CRUD operations
3. configurable custom examples per operation
4. auth/security scheme emulation
5. exporting a standalone ASP.NET project from the uploaded spec
