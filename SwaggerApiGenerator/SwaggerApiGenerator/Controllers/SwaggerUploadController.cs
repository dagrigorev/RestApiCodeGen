using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NSwag;
using NSwag.CodeGeneration.CSharp;
using NSwag.CodeGeneration.CSharp.Models;
using System.Reflection;

namespace SwaggerApiGenerator.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SwaggerUploadController : ControllerBase
    {
        private static Assembly GeneratedAssembly;
        private readonly ILogger<SwaggerUploadController> _logger;
        private readonly ApplicationPartManager _partManager;

        public SwaggerUploadController(ILogger<SwaggerUploadController> logger, ApplicationPartManager partManager)
        {
            _logger = logger;
            _partManager = partManager;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadSwaggerFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "UploadedFiles", file.FileName);

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Generate API code from Swagger
            GeneratedAssembly = await GenerateApiFromSwagger(filePath);

            return Ok(new { Message = "File uploaded and API generated successfully." });
        }

        private async Task<Assembly> GenerateApiFromSwagger(string swaggerFilePath)
        {
            var document = await OpenApiDocument.FromFileAsync(swaggerFilePath);
            var settings = new CSharpControllerGeneratorSettings
            {
                ControllerBaseClass = "Microsoft.AspNetCore.Mvc.ControllerBase",
                ControllerStyle = CSharpControllerStyle.Partial,
                ControllerTarget = CSharpControllerTarget.AspNetCore,
                UseCancellationToken = true,
                RouteNamingStrategy = CSharpControllerRouteNamingStrategy.OperationId,
                GenerateModelValidationAttributes = true,
                UseActionResultType = true,
                BasePath = "api"
            };
            settings.CodeGeneratorSettings.GenerateDefaultValues = true;

            var generator = new CSharpControllerGenerator(document, settings);
            var code = generator.GenerateFile();

            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create("GeneratedApi")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(ControllerBase).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(IActionResult).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(HttpPostAttribute).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Newtonsoft.Json.JsonConvert).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Net.Http.HttpClient).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(System.Net.HttpStatusCode).Assembly.Location),
                    MetadataReference.CreateFromFile(AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "System.Runtime").Location)
                )
                .AddSyntaxTrees(syntaxTree);

            using (var ms = new MemoryStream())
            {
                var result = compilation.Emit(ms);

                if (!result.Success)
                {
                    var failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);
                    foreach (var diagnostic in failures)
                    {
                        System.Console.Error.WriteLine(diagnostic.ToString());
                    }

                    throw new InvalidOperationException("Compilation failed.");
                }

                ms.Seek(0, SeekOrigin.Begin);
                return Assembly.Load(ms.ToArray());
            }
        }

        [HttpGet("serve")]
        public IActionResult ServeGeneratedApi()
        {
            if (GeneratedAssembly == null)
            {
                return NotFound("No API has been generated.");
            }

            // Load generated controllers into the application
            LoadGeneratedControllers(GeneratedAssembly, _partManager, _logger);

            return Ok(new { Message = "Generated API is ready.", CallerControllerName = GeneratedAssembly.DefinedTypes.Where(a => !a.Name.EndsWith("ControllerBase") && a.Name.EndsWith("Controller")).First().Name });
        }

        private void LoadGeneratedControllers(Assembly generatedAssembly, ApplicationPartManager partManager, ILogger logger)
        {
            var controllers = generatedAssembly.GetTypes()
                .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
                .ToList();

            foreach (var controller in controllers)
            {
                // Log the controller being loaded
                logger.LogInformation($"Loading controller: {controller.FullName}");

                // Create an assembly part from the generated assembly
                var part = new AssemblyPart(generatedAssembly);

                // Add the assembly part to the application part manager
                partManager.ApplicationParts.Add(part);
            }
        }
    }
}
