using NJsonSchema;

namespace SwaggerApiGenerator.Services;

public sealed class OpenApiExampleFactory
{
    public object? CreateExample(JsonSchema? schema)
    {
        if (schema is null)
        {
            return null;
        }

        var actual = schema.ActualSchema;

        if (actual.Example is not null)
        {
            return actual.Example;
        }

        if (actual.Default is not null)
        {
            return actual.Default;
        }

        if (actual.IsEnumeration && actual.Enumeration.Count > 0)
        {
            return actual.Enumeration.ElementAt(0);
        }

        if (actual.Type.HasFlag(JsonObjectType.Array) && actual.Item is not null)
        {
            return new[] { CreateExample(actual.Item) };
        }

        if (actual.Type.HasFlag(JsonObjectType.Object) || actual.Properties.Count > 0 || actual.AllOf.Count > 0)
        {
            var result = new Dictionary<string, object?>();

            foreach (var property in actual.ActualProperties)
            {
                result[property.Key] = CreateExample(property.Value);
            }

            foreach (var inheritedSchema in actual.AllOf)
            {
                if (CreateExample(inheritedSchema) is IDictionary<string, object?> inherited)
                {
                    foreach (var pair in inherited)
                    {
                        result[pair.Key] = pair.Value;
                    }
                }
            }

            return result;
        }

        if (actual.Type.HasFlag(JsonObjectType.Integer))
        {
            return 1;
        }

        if (actual.Type.HasFlag(JsonObjectType.Number))
        {
            return 1.0;
        }

        if (actual.Type.HasFlag(JsonObjectType.Boolean))
        {
            return true;
        }

        if (actual.Format == JsonFormatStrings.Date)
        {
            return DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        }

        if (actual.Format == JsonFormatStrings.DateTime)
        {
            return DateTimeOffset.UtcNow.ToString("O");
        }

        if (actual.Format == JsonFormatStrings.Uuid)
        {
            return Guid.NewGuid().ToString();
        }

        return actual.Type.HasFlag(JsonObjectType.Null)
            ? null
            : $"sample-{actual.Title?.ToLowerInvariant().Replace(' ', '-') ?? "value"}";
    }
}
