using NSwag;

namespace SwaggerApiGenerator.Models;

public sealed class GeneratedApiDefinition
{
    public required string ApiId { get; init; }
    public required string Title { get; init; }
    public required string Version { get; init; }
    public string? Description { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required string OriginalFileName { get; init; }
    public required string StoredSpecificationPath { get; init; }
    public required OpenApiDocument Document { get; init; }
    public required IReadOnlyList<GeneratedOperation> Operations { get; init; }
}

public sealed class GeneratedOperation
{
    public required string OperationId { get; init; }
    public required string HttpMethod { get; init; }
    public required string OriginalPath { get; init; }
    public required string RuntimePathTemplate { get; init; }
    public string? Summary { get; init; }
    public string? Description { get; init; }
    public required IReadOnlyList<GeneratedParameterContract> Parameters { get; init; }
    public GeneratedRequestBodyContract? RequestBody { get; init; }
    public required IReadOnlyList<GeneratedResponseContract> Responses { get; init; }
}

public sealed class GeneratedParameterContract
{
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public required bool IsRequired { get; init; }
    public string? Type { get; init; }
    public string? Format { get; init; }
    public string? Description { get; init; }
}

public sealed class GeneratedRequestBodyContract
{
    public required bool IsRequired { get; init; }
    public required IReadOnlyList<GeneratedMediaTypeContract> Content { get; init; }
}

public sealed class GeneratedMediaTypeContract
{
    public required string MediaType { get; init; }
    public string? SchemaType { get; init; }
    public object? Example { get; init; }
}

public sealed class GeneratedResponseContract
{
    public required string StatusCode { get; init; }
    public string? Description { get; init; }
    public required IReadOnlyList<GeneratedMediaTypeContract> Content { get; init; }
}
