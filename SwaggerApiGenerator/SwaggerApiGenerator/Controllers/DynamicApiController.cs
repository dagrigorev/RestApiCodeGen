using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

// ... other namespaces
[Route("api/[controller]")]
[ApiController]
public class DynamicApiController : ControllerBase
{
    private readonly ApplicationPartManager _partManager;
    private readonly ILogger<DynamicApiController> _logger;

    public DynamicApiController(ApplicationPartManager partManager, ILogger<DynamicApiController> logger)
    {
        _partManager = partManager;
        _logger = logger;
    }

    // ... other methods
    [HttpGet("{controllerName}/{actionName}")]
    [HttpPost("{controllerName}/{actionName}")]
    [HttpPut("{controllerName}/{actionName}")]
    [HttpDelete("{controllerName}/{actionName}")]
    [HttpPatch("{controllerName}/{actionName}")]
    [HttpOptions("{controllerName}/{actionName}")]
    [HttpHead("{controllerName}/{actionName}")]
    public IActionResult ExecuteMethod([FromRoute] string controllerName, [FromRoute] string actionName)
    {
        // 1. Find the Controller
        var controllerType = FindControllerType(controllerName);
        if (controllerType == null)
        {
            return NotFound($"Controller '{controllerName}' not found.");
        }

        // 2. Create an instance of the controller
        var controller = Activator.CreateInstance(controllerType); //ActivatorUtilities.CreateInstance(HttpContext.RequestServices, controllerType) as ControllerBase;
        if (controller == null)
        {
            return StatusCode(500, $"Failed to create instance of controller '{controllerName}'.");
        }

        // 3. Find the action method
        var methodInfo = controllerType.GetMethod(actionName);
        if (methodInfo == null)
        {
            return NotFound($"Action '{actionName}' not found on controller '{controllerName}'.");
        }

        // 4. Invoke the action method
        try
        {
            var result = methodInfo.Invoke(controller, null); // Assuming no parameters for now
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error executing method '{actionName}' on controller '{controllerName}'.");
            return StatusCode(500, "An error occurred while processing your request.");
        }
    }

    private Type? FindControllerType(string controllerName)
    {
        // Logic to find the controller type from loaded application parts
        foreach (var part in _partManager.ApplicationParts)
        {
            if (part is AssemblyPart assemblyPart)
            {
                var controllerType = assemblyPart.Types.FirstOrDefault(t =>
                    t.Name.Equals(controllerName, StringComparison.OrdinalIgnoreCase) &&
                    typeof(ControllerBase).IsAssignableFrom(t));

                if (controllerType != null)
                {
                    return controllerType;
                }
            }
        }

        return null;
    }
}