using Microsoft.AspNetCore.Mvc;
using SwaggerApiGenerator.Contracts;
using SwaggerApiGenerator.Models;
using SwaggerApiGenerator.Services;

namespace SwaggerApiGenerator.Controllers;

[ApiController]
[Route("api/specifications")]
public sealed class SpecificationsController : ControllerBase
{
    private readonly OpenApiRegistry _registry;

    public SpecificationsController(OpenApiRegistry registry)
    {
        _registry = registry;
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] UploadSpecificationRequest request)
    {
        var file = request.File;

        if (file is null)
        {
            return BadRequest(new { error = "File is required." });
        }

        try
        {
            var definition = await _registry.RegisterAsync(file);
            return CreatedAtAction(nameof(GetById), new { apiId = definition.ApiId }, BuildPayload(definition));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult List()
    {
        return Ok(_registry.List().Select(BuildPayload));
    }

    [HttpGet("{apiId}")]
    public IActionResult GetById(string apiId)
    {
        if (!_registry.TryGet(apiId, out var definition) || definition is null)
        {
            return NotFound(new { error = $"API '{apiId}' was not found." });
        }

        return Ok(BuildPayload(definition));
    }

    [HttpDelete("{apiId}")]
    public IActionResult Delete(string apiId)
    {
        if (!_registry.Delete(apiId))
        {
            return NotFound(new { error = $"API '{apiId}' was not found." });
        }

        return NoContent();
    }

    [HttpGet("{apiId}/contracts")]
    public IActionResult GetContracts(string apiId)
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
            definition.CreatedAtUtc,
            baseUrl = GetBaseUrl(definition.ApiId),
            operations = definition.Operations
        });
    }

    [HttpGet("{apiId}/openapi")]
    public IActionResult GetSpecification(string apiId)
    {
        if (!_registry.TryGet(apiId, out var definition) || definition is null)
        {
            return NotFound(new { error = $"API '{apiId}' was not found." });
        }

        var fileName = Path.GetFileName(definition.StoredSpecificationPath);
        var contentType = fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
            ? "application/yaml"
            : "application/json";

        return PhysicalFile(definition.StoredSpecificationPath, contentType, fileName);
    }

    private object BuildPayload(GeneratedApiDefinition definition)
    {
        return new
        {
            definition.ApiId,
            definition.Title,
            definition.Version,
            definition.Description,
            definition.CreatedAtUtc,
            definition.OriginalFileName,
            baseUrl = GetBaseUrl(definition.ApiId),
            contractsUrl = $"{Request.Scheme}://{Request.Host}/api/specifications/{definition.ApiId}/contracts",
            specificationUrl = $"{Request.Scheme}://{Request.Host}/api/specifications/{definition.ApiId}/openapi",
            operations = definition.Operations.Select(o => new
            {
                o.OperationId,
                o.HttpMethod,
                o.OriginalPath,
                url = $"{Request.Scheme}://{Request.Host}{o.RuntimePathTemplate}",
                o.Summary
            })
        };
    }

    private string GetBaseUrl(string apiId) => $"{Request.Scheme}://{Request.Host}/generated/{apiId}";
}
