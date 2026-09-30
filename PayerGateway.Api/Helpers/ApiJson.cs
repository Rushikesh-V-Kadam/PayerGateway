using System.Text.Json;

namespace PayerGateway.Api.Helpers;

/// <summary>
/// JSON settings for the plain (non-FHIR) JSON the gateway stores and sends to the EHR:
/// camelCase names, same as the API responses.
/// </summary>
public static class ApiJson
{
    /// <summary>Web defaults (camelCase, case-insensitive reading).</summary>
    public static readonly JsonSerializerOptions Options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    /// <summary>Object -> JSON text.</summary>
    public static string Write<T>(T value)
    {
        return JsonSerializer.Serialize(value, Options);
    }

    /// <summary>JSON text -> object; a new empty object when the text is empty.</summary>
    public static T Read<T>(string? json) where T : new()
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new T();
        }
        T? value = JsonSerializer.Deserialize<T>(json, Options);
        if (value == null)
        {
            return new T();
        }
        return value;
    }
}
