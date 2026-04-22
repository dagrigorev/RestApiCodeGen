using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NJsonSchema;
using NSwag;
using SwaggerApiGenerator.Models;

namespace SwaggerApiGenerator.Services;

public sealed class OpenApiRegistry
{
    private readonly ConcurrentDictionary<string, GeneratedApiDefinition> _definitions = new();
    private readonly OpenApiExampleFactory _exampleFactory;
    private readonly ILogger<OpenApiRegistry> _logger;
    private readonly string _storageRoot;

    public OpenApiRegistry(OpenApiExampleFactory exampleFactory, ILogger<OpenApiRegistry> logger, IWebHostEnvironment environment)
    {
        _exampleFactory = exampleFactory;
        _logger = logger;
        _storageRoot = Path.Combine(environment.ContentRootPath, "GeneratedApis");
        Directory.CreateDirectory(_storageRoot);
    }

    public IReadOnlyCollection<GeneratedApiDefinition> List()
        => _definitions.Values.OrderByDescending(x => x.CreatedAtUtc).ToArray();

    public bool TryGet(string apiId, out GeneratedApiDefinition? definition)
        => _definitions.TryGetValue(apiId, out definition);

    public bool Delete(string apiId)
    {
        if (!_definitions.TryRemove(apiId, out var definition) || definition is null)
        {
            return false;
        }

        var apiFolder = Path.GetDirectoryName(definition.StoredSpecificationPath);
        if (!string.IsNullOrWhiteSpace(apiFolder) && Directory.Exists(apiFolder))
        {
            Directory.Delete(apiFolder, recursive: true);
        }

        return true;
    }

    public async Task LoadPersistedApisAsync()
    {
        if (!Directory.Exists(_storageRoot))
        {
            return;
        }

        foreach (var filePath in Directory.EnumerateFiles(_storageRoot, "spec.*", SearchOption.AllDirectories))
        {
            try
            {
                var folder = Path.GetDirectoryName(filePath);
                var apiId = Path.GetFileName(folder);
                if (string.IsNullOrWhiteSpace(apiId) || _definitions.ContainsKey(apiId))
                {
                    continue;
                }

                var document = await OpenApiDocument.FromFileAsync(filePath);
                var createdAtUtc = new DateTimeOffset(Directory.GetCreationTimeUtc(folder!));
                var definition = BuildDefinition(apiId, Path.GetFileName(filePath), filePath, document, createdAtUtc);
                _definitions[apiId] = definition;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load persisted API specification from {Path}", filePath);
            }
        }
    }

    public async Task<GeneratedApiDefinition> RegisterAsync(IFormFile file)
    {
        if (file.Length <= 0)
        {
            throw new InvalidOperationException("The uploaded file is empty.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !new[] { ".json", ".yaml", ".yml" }.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only .json, .yaml and .yml OpenAPI files are supported.");
        }

        var apiId = $"api-{Guid.NewGuid():N}";
        var apiFolder = Path.Combine(_storageRoot, apiId);
        Directory.CreateDirectory(apiFolder);

        var storedFilePath = Path.Combine(apiFolder, $"spec{extension.ToLowerInvariant()}");
        await using (var stream = File.Create(storedFilePath))
        {
            await file.CopyToAsync(stream);
        }

        try
        {
            var document = await OpenApiDocument.FromFileAsync(storedFilePath);
            if ((document.Paths?.Count ?? 0) == 0)
            {
                throw new InvalidOperationException("The uploaded file was parsed, but it does not contain any API paths.");
            }

            var definition = BuildDefinition(apiId, file.FileName, storedFilePath, document, DateTimeOffset.UtcNow);
            _definitions[apiId] = definition;
            await PersistMetadataAsync(definition);
            return definition;
        }
        catch
        {
            if (Directory.Exists(apiFolder))
            {
                Directory.Delete(apiFolder, recursive: true);
            }

            throw;
        }
    }

    public GeneratedOperation? MatchOperation(GeneratedApiDefinition definition, string httpMethod, string remainingPath)
    {
        var requestPath = NormalizePath(remainingPath);

        return definition.Operations.FirstOrDefault(operation =>
            string.Equals(operation.HttpMethod, httpMethod, StringComparison.OrdinalIgnoreCase)
            && RouteMatches(operation.OriginalPath, requestPath));
    }

    public IDictionary<string, string> ExtractRouteValues(string routeTemplate, string requestPath)
    {
        var templateSegments = GetSegments(routeTemplate);
        var requestSegments = GetSegments(requestPath);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < templateSegments.Length && i < requestSegments.Length; i++)
        {
            var segment = templateSegments[i];
            if (segment.StartsWith('{') && segment.EndsWith('}'))
            {
                values[segment.Trim('{', '}')] = requestSegments[i];
            }
        }

        return values;
    }

    private async Task PersistMetadataAsync(GeneratedApiDefinition definition)
    {
        var metadataPath = Path.Combine(Path.GetDirectoryName(definition.StoredSpecificationPath)!, "metadata.json");
        var metadata = new
        {
            definition.ApiId,
            definition.Title,
            definition.Version,
            definition.Description,
            definition.CreatedAtUtc,
            definition.OriginalFileName,
            definition.StoredSpecificationPath,
            operations = definition.Operations.Select(o => new
            {
                o.OperationId,
                o.HttpMethod,
                o.OriginalPath,
                o.RuntimePathTemplate,
                o.Summary,
                o.Description
            })
        };

        await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    private GeneratedApiDefinition BuildDefinition(
        string apiId,
        string originalFileName,
        string storedFilePath,
        OpenApiDocument document,
        DateTimeOffset createdAtUtc)
    {
        var operations = new List<GeneratedOperation>();

        foreach (var pathItemPair in document.Paths)
        {
            var normalizedPath = NormalizePath(pathItemPair.Key);
            foreach (var operationPair in pathItemPair.Value)
            {
                var method = operationPair.Key.ToString().ToUpperInvariant();
                var operation = operationPair.Value;

                var parameters = operation.Parameters.Select(parameter => new GeneratedParameterContract
                {
                    Name = parameter.Name,
                    Kind = parameter.Kind.ToString(),
                    IsRequired = parameter.IsRequired,
                    Type = parameter.Schema?.ActualSchema?.Type.ToString(),
                    Format = parameter.Schema?.ActualSchema?.Format,
                    Description = parameter.Description
                }).ToArray();

                GeneratedRequestBodyContract? requestBody = null;
                if (operation.RequestBody is not null)
                {
                    requestBody = new GeneratedRequestBodyContract
                    {
                        IsRequired = operation.RequestBody.IsRequired,
                        Content = operation.RequestBody.Content.Select(content => new GeneratedMediaTypeContract
                        {
                            MediaType = content.Key,
                            SchemaType = content.Value.Schema?.ActualSchema?.Type.ToString(),
                            Example = CreateMediaTypeExample(content.Value.Schema)
                        }).ToArray()
                    };
                }

                var responses = operation.Responses.Select(response => new GeneratedResponseContract
                {
                    StatusCode = response.Key,
                    Description = response.Value.Description,
                    Content = response.Value.Content.Select(content => new GeneratedMediaTypeContract
                    {
                        MediaType = content.Key,
                        SchemaType = content.Value.Schema?.ActualSchema?.Type.ToString(),
                        Example = CreateMediaTypeExample(content.Value.Schema)
                    }).ToArray()
                }).OrderBy(r => r.StatusCode).ToArray();

                operations.Add(new GeneratedOperation
                {
                    OperationId = string.IsNullOrWhiteSpace(operation.OperationId)
                        ? BuildOperationId(method, normalizedPath)
                        : operation.OperationId,
                    HttpMethod = method,
                    OriginalPath = normalizedPath,
                    RuntimePathTemplate = $"/generated/{apiId}{normalizedPath}",
                    Summary = operation.Summary,
                    Description = operation.Description,
                    Parameters = parameters,
                    RequestBody = requestBody,
                    Responses = responses
                });
            }
        }

        return new GeneratedApiDefinition
        {
            ApiId = apiId,
            Title = string.IsNullOrWhiteSpace(document.Info?.Title) ? apiId : document.Info.Title,
            Version = string.IsNullOrWhiteSpace(document.Info?.Version) ? "1.0.0" : document.Info.Version,
            Description = document.Info?.Description,
            CreatedAtUtc = createdAtUtc,
            OriginalFileName = originalFileName,
            StoredSpecificationPath = storedFilePath,
            Document = document,
            Operations = operations.OrderBy(x => x.RuntimePathTemplate).ThenBy(x => x.HttpMethod).ToArray()
        };
    }

    private object? CreateMediaTypeExample(JsonSchema? schema)
        => _exampleFactory.CreateExample(schema);

    private static bool RouteMatches(string routeTemplate, string requestPath)
    {
        var templateSegments = GetSegments(routeTemplate);
        var requestSegments = GetSegments(requestPath);

        if (templateSegments.Length != requestSegments.Length)
        {
            return false;
        }

        for (var i = 0; i < templateSegments.Length; i++)
        {
            var templateSegment = templateSegments[i];
            if (templateSegment.StartsWith('{') && templateSegment.EndsWith('}'))
            {
                continue;
            }

            if (!string.Equals(templateSegment, requestSegments[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string[] GetSegments(string path)
        => NormalizePath(path)
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        path = path.Replace("//", "/", StringComparison.Ordinal);
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        return path == "/" ? path : path.TrimEnd('/');
    }

    private static string BuildOperationId(string method, string path)
    {
        var cleaned = path.Trim('/').Replace("/", "_").Replace("{", string.Empty).Replace("}", string.Empty);
        return string.IsNullOrWhiteSpace(cleaned) ? method.ToLowerInvariant() : $"{method.ToLowerInvariant()}_{cleaned}";
    }
}
