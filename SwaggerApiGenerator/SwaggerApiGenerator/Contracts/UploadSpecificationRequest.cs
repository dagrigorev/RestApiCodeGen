using System.ComponentModel.DataAnnotations;

namespace SwaggerApiGenerator.Contracts;

public sealed class UploadSpecificationRequest
{
    [Required]
    public IFormFile File { get; set; } = default!;
}