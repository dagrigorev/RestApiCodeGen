using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SwaggerApiGenerator.Models;
using SwaggerApiGenerator.Services;

namespace SwaggerApiGenerator.Controllers;

[ApiController]
[Route("generated/{apiId}")]
public sealed class GeneratedApiController : ControllerBase
{
    private readonly OpenApiRegistry _registry;

    public GeneratedApiController(OpenApiRegistry registry)
    {
        _registry = registry;
    }

    [HttpGet]
    public IActionResult DescribeRoot(string apiId)
    {
        if (!_registry.TryGet(apiId, out var definition) || definition is null)
        {
            return NotFound(new { error = $"API '{apiId}' was not found." });
        }

        return Ok(new
        {
            definition.ApiId,
            definition.Title,
            definition.Version,
            definition.Description,
            operations = definition.Operations.Select(o => new
            {
                o.OperationId,
                o.HttpMethod,
                o.OriginalPath,
                runtimePath = o.RuntimePathTemplate,
                o.Summary
            })
        });
    }

    [Route("{**path}")]
    [AcceptVerbs("GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS")]
    public async Task<IActionResult> Execute(string apiId, string? path)
    {
        if (!_registry.TryGet(apiId, out var definition) || definition is null)
        {
            return NotFound(new { error = $"API '{apiId}' was not found." });
        }

        var remainingPath = "/" + (path ?? string.Empty).Trim('/');
        var operation = _registry.MatchOperation(definition, Request.Method, remainingPath);
        if (operation is null)
        {
            return NotFound(new
            {
                error = $"No generated operation matched {Request.Method} {remainingPath}.",
                available = definition.Operations.Select(o => new { o.HttpMethod, o.RuntimePathTemplate })
            });
        }

        var routeValues = _registry.ExtractRouteValues(operation.OriginalPath, remainingPath);
        var missingParameters = FindMissingRequiredParameters(operation, routeValues, Request.Query, Request.Headers);
        if (missingParameters.Count > 0)
        {
            return BadRequest(new
            {
                error = "The request is missing required parameters.",
                missing = missingParameters
            });
        }

        object? requestBody = null;
        if (operation.RequestBody is not null && Request.ContentLength.GetValueOrDefault() > 0)
        {
            using var reader = new StreamReader(Request.Body);
            var raw = await reader.ReadToEndAsync();
            requestBody = TryParseJson(raw);
        }
        else if (operation.RequestBody is not null && operation.RequestBody.IsRequired)
        {
            return BadRequest(new { error = "A request body is required by the specification." });
        }

        var response = SelectResponse(operation);
        var payload = response.Content.FirstOrDefault()?.Example ?? new { message = "Operation executed." };

        Response.Headers["X-Generated-Api-Id"] = definition.ApiId;
        Response.Headers["X-Generated-Operation-Id"] = operation.OperationId;

        return StatusCode(ParseStatusCode(response.StatusCode), new
        {
            meta = new
            {
                definition.ApiId,
                definition.Title,
                operation.OperationId,
                operation.HttpMethod,
                operation.OriginalPath,
                requestedPath = remainingPath,
                responseStatus = response.StatusCode
            },
            request = new
            {
                path = routeValues,
                query = Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString()),
                headers = Request.Headers
                    .Where(h => h.Key.StartsWith("x-", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(x => x.Key, x => x.Value.ToString()),
                body = requestBody
            },
            response = payload
        });
    }

    private static List<string> FindMissingRequiredParameters(
        GeneratedOperation operation,
        IDictionary<string, string> routeValues,
        IQueryCollection query,
        IHeaderDictionary headers)
    {
        var missing = new List<string>();

        foreach (var parameter in operation.Parameters.Where(p => p.IsRequired))
        {
            switch (parameter.Kind.ToLowerInvariant())
            {
                case "path" when !routeValues.ContainsKey(parameter.Name):
                    missing.Add($"path:{parameter.Name}");
                    break;
                case "query" when !query.ContainsKey(parameter.Name):
                    missing.Add($"query:{parameter.Name}");
                    break;
                case "header" when !headers.ContainsKey(parameter.Name):
                    missing.Add($"header:{parameter.Name}");
                    break;
            }
        }

        return missing;
    }

    private static GeneratedResponseContract SelectResponse(GeneratedOperation operation)
    {
        return operation.Responses.FirstOrDefault(r => r.StatusCode is "200" or "201" or "202")
               ?? operation.Responses.FirstOrDefault(r => r.StatusCode.Equals("default", StringComparison.OrdinalIgnoreCase))
               ?? operation.Responses.FirstOrDefault()
               ?? new GeneratedResponseContract
               {
                   StatusCode = "200",
                   Description = "Generated default response.",
                   Content = Array.Empty<GeneratedMediaTypeContract>()
               };
    }

    private static int ParseStatusCode(string statusCode)
    {
        return int.TryParse(statusCode, out var parsed)
            ? parsed
            : StatusCodes.Status200OK;
    }

    private static object? TryParseJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            return JsonSerializer.Deserialize<object>(document.RootElement.GetRawText());
        }
        catch
        {
            return raw;
        }
    }
}
