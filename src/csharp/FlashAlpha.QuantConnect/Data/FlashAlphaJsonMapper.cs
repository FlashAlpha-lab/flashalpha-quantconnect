using System;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlashAlpha.QuantConnect.Data;

/// <summary>
/// Reflection-driven snake_case JSON → PascalCase property mapper used by
/// <see cref="FlashAlphaSource"/>'s <c>Parse&lt;T&gt;</c>.
/// </summary>
/// <remarks>
/// Each FlashAlpha bar exposes PascalCase properties; this routine snake-cases
/// them and looks up the corresponding JSON value. A
/// <see cref="JsonPropertyNameAttribute"/> on a property overrides the
/// snake-case default.
/// </remarks>
internal static class FlashAlphaJsonMapper
{
    /// <summary>
    /// Populates the public, settable PascalCase properties of <paramref name="bar"/>
    /// from the JSON object <paramref name="root"/>.
    /// </summary>
    /// <param name="bar">The bar instance to populate. Must not be null.</param>
    /// <param name="root">The JSON root element. Must be a JSON object; any other
    /// kind (array, null, primitive) is a silent no-op.</param>
    public static void PopulateProperties(object bar, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return;
        var type = bar.GetType();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanWrite) continue;
            var jsonName = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                          ?? ToSnakeCase(prop.Name);
            if (!root.TryGetProperty(jsonName, out var element)) continue;
            var value = ReadValue(element, prop.PropertyType);
            if (value != null) prop.SetValue(bar, value);
        }
    }

    private static string ToSnakeCase(string pascal)
    {
        var sb = new StringBuilder(pascal.Length + 4);
        for (int i = 0; i < pascal.Length; i++)
        {
            char c = pascal[i];
            if (i > 0 && char.IsUpper(c)) sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static object? ReadValue(JsonElement el, Type targetType)
    {
        if (el.ValueKind == JsonValueKind.Null) return null;

        // Strip Nullable<T> if present
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlying == typeof(decimal))  return el.GetDecimal();
        if (underlying == typeof(double))   return el.GetDouble();
        if (underlying == typeof(float))    return (float)el.GetDouble();
        if (underlying == typeof(int))      return el.GetInt32();
        if (underlying == typeof(long))     return el.GetInt64();
        if (underlying == typeof(string))   return el.GetString();
        if (underlying == typeof(bool))     return el.GetBoolean();
        if (underlying == typeof(DateTime)) return el.GetDateTime();

        // Nested objects / lists — defer to JsonSerializer with snake_case naming
        return JsonSerializer.Deserialize(el.GetRawText(), targetType,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
    }
}
