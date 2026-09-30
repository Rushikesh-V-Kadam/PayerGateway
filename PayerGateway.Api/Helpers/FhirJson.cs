using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PayerGateway.Api.Helpers;

/// <summary>
/// JSON settings and small helpers for FHIR and CDS Hooks bodies:
///  - camelCase names; null values and empty lists are not written (FHIR does not allow empty arrays)
///  - property names are matched without regard to case when reading
/// </summary>
public static class FhirJson
{
    /// <summary>Serializer options used for every FHIR / CDS Hooks body.</summary>
    public static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>Object -> JSON text.</summary>
    public static string Serialize(object value)
    {
        return JsonSerializer.Serialize(value, value.GetType(), Options);
    }

    /// <summary>Object -> JSON tree (to put a typed resource into a Bundle entry).</summary>
    public static JsonObject ToNode(object value)
    {
        JsonNode? node = JsonNode.Parse(Serialize(value));
        if (node == null)
        {
            return new JsonObject();
        }
        return node.AsObject();
    }

    /// <summary>JSON text -> typed object.</summary>
    public static T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, Options);
    }

    /// <summary>JSON tree -> typed object (to read fields of a raw resource). Null in, default out.</summary>
    public static T? Read<T>(JsonNode? node)
    {
        if (node == null)
        {
            return default;
        }
        return node.Deserialize<T>(Options);
    }

    /// <summary>"resourceType" of a raw resource, or null.</summary>
    public static string? ResourceType(JsonNode? node)
    {
        return GetString(node, "resourceType");
    }

    /// <summary>"id" of a raw resource, or null.</summary>
    public static string? Id(JsonNode? node)
    {
        return GetString(node, "id");
    }

    /// <summary>A string property of a JSON object, or null when missing or not a string.</summary>
    public static string? GetString(JsonNode? node, string propertyName)
    {
        JsonObject? obj = node as JsonObject;
        if (obj == null)
        {
            return null;
        }
        JsonValue? value = obj[propertyName] as JsonValue;
        if (value == null)
        {
            return null;
        }
        string? text;
        if (value.TryGetValue<string>(out text))
        {
            return text;
        }
        return null;
    }

    /// <summary>Deep copy of a JSON object.</summary>
    public static JsonObject Copy(JsonObject source)
    {
        return source.DeepClone().AsObject();
    }

    /// <summary>Builds the options once.</summary>
    private static JsonSerializerOptions CreateOptions()
    {
        DefaultJsonTypeInfoResolver resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(SkipEmptyLists);

        JsonSerializerOptions options = new JsonSerializerOptions();
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = true;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.TypeInfoResolver = resolver;
        return options;
    }

    /// <summary>For every list property: only write it when it has items.</summary>
    private static void SkipEmptyLists(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }
        foreach (JsonPropertyInfo property in typeInfo.Properties)
        {
            if (property.PropertyType == typeof(string))
            {
                continue;
            }
            bool isList = typeof(ICollection).IsAssignableFrom(property.PropertyType);
            bool isJson = typeof(JsonNode).IsAssignableFrom(property.PropertyType);
            if (isList && !isJson)
            {
                property.ShouldSerialize = HasItems;
            }
        }
    }

    /// <summary>True when the list value has at least one item.</summary>
    private static bool HasItems(object parent, object? value)
    {
        ICollection? list = value as ICollection;
        return list != null && list.Count > 0;
    }
}
