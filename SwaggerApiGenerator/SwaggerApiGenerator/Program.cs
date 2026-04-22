using Microsoft.AspNetCore.Http.Features;
using Microsoft.OpenApi.Models;
using SwaggerApiGenerator.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Swagger API Generator",
        Version = "v1",
        Description = "Upload an OpenAPI specification and instantly host a callable runtime mock API under /generated/{apiId}."
    });
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 50 * 1024 * 1024;
});

builder.Services.AddSingleton<OpenApiExampleFactory>();
builder.Services.AddSingleton<OpenApiRegistry>();

var app = builder.Build();

var registry = app.Services.GetRequiredService<OpenApiRegistry>();
await registry.LoadPersistedApisAsync();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/", (HttpRequest request) => Results.Ok(new
{
    name = "Swagger API Generator",
    version = "1.0.0",
    description = "Upload an OpenAPI specification and get a callable generated runtime API.",
    management = new
    {
        upload = $"{request.Scheme}://{request.Host}/api/specifications/upload",
        list = $"{request.Scheme}://{request.Host}/api/specifications",
        swagger = $"{request.Scheme}://{request.Host}/swagger"
    }
}));

app.Run();
