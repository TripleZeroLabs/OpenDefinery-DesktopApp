using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenDefinery
{
    /// <summary>
    /// JSON helpers built on System.Text.Json, replacing Newtonsoft.Json (which Revit bundles
    /// an old version of and force-binds add-ins to). One shared <see cref="Options"/> instance
    /// configured to be as lenient as Newtonsoft was for our read paths:
    ///   - case-insensitive property matching
    ///   - numbers readable from JSON strings (and vice-versa via the string converter)
    /// API v2 payloads can be handled by tuning these options in one place.
    /// </summary>
    public static class OdJson
    {
        public static readonly JsonSerializerOptions Options = CreateOptions();

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
            };
            options.Converters.Add(new LenientStringConverter());
            options.Converters.Add(new LenientBooleanConverter());
            options.Converters.Add(new LenientGuidConverter());
            return options;
        }

        /// <summary>
        /// Quote and escape a value as a JSON string literal, for the hand-built request
        /// bodies. Prevents a " or \ in user input (passwords, names, descriptions) from
        /// corrupting the JSON.
        /// </summary>
        public static string ToJsonString(string value)
        {
            return JsonSerializer.Serialize(value ?? string.Empty);
        }

        /// <summary>Deserialize an entire JSON document into <typeparamref name="T"/>.</summary>
        public static T Deserialize<T>(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(json, Options);
        }

        /// <summary>
        /// Return the raw JSON text of a top-level property, or null if absent
        /// (replaces Newtonsoft's <c>JObject.Parse(x).SelectToken("name").ToString()</c>).
        /// </summary>
        public static string GetPropertyRaw(string json, string property)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            using (var doc = JsonDocument.Parse(json))
            {
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty(property, out var element))
                {
                    return element.GetRawText();
                }
            }

            return null;
        }

        /// <summary>
        /// Return a nested string value following a property path
        /// (replaces <c>SelectToken("a.b.c")</c>).
        /// </summary>
        public static string GetString(string json, params string[] path)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            using (var doc = JsonDocument.Parse(json))
            {
                var element = doc.RootElement;
                foreach (var name in path)
                {
                    if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out element))
                    {
                        return null;
                    }
                }

                return element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
            }
        }

        /// <summary>Number of elements in a top-level array property (0 if absent/not an array).</summary>
        public static int CountProperty(string json, string property)
        {
            if (string.IsNullOrEmpty(json))
            {
                return 0;
            }

            using (var doc = JsonDocument.Parse(json))
            {
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty(property, out var element) &&
                    element.ValueKind == JsonValueKind.Array)
                {
                    return element.GetArrayLength();
                }
            }

            return 0;
        }
    }

    /// <summary>
    /// Reads a string from a string, number, or boolean JSON token (Newtonsoft did this
    /// implicitly; System.Text.Json does not). Keeps deserialization tolerant of the Drupal
    /// API returning numeric ids/flags where a string property is declared.
    /// </summary>
    internal sealed class LenientStringConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.Number:
                    return reader.TryGetInt64(out var l)
                        ? l.ToString(CultureInfo.InvariantCulture)
                        : reader.GetDouble().ToString(CultureInfo.InvariantCulture);
                case JsonTokenType.True:
                    return "true";
                case JsonTokenType.False:
                    return "false";
                case JsonTokenType.Null:
                    return null;
                default:
                    return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }

    /// <summary>
    /// Reads a bool from a JSON boolean, string ("1"/"0", "true"/"false", "yes"/"no"), or
    /// number (0 = false). The Drupal API returns flags such as a Collection's "public" as
    /// STRINGS; Newtonsoft coerced those implicitly, System.Text.Json throws without this.
    /// </summary>
    internal sealed class LenientBooleanConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.True:
                    return true;
                case JsonTokenType.False:
                    return false;
                case JsonTokenType.Number:
                    return reader.TryGetInt64(out var n) ? n != 0 : reader.GetDouble() != 0d;
                case JsonTokenType.String:
                    var s = reader.GetString();
                    if (string.IsNullOrWhiteSpace(s)) return false;
                    s = s.Trim();
                    if (bool.TryParse(s, out var parsed)) return parsed;
                    if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sn)) return sn != 0;
                    return s.Equals("yes", StringComparison.OrdinalIgnoreCase)
                        || s.Equals("y", StringComparison.OrdinalIgnoreCase)
                        || s.Equals("on", StringComparison.OrdinalIgnoreCase);
                case JsonTokenType.Null:
                default:
                    return false;
            }
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        {
            writer.WriteBooleanValue(value);
        }
    }

    /// <summary>
    /// Reads a Guid, falling back to <see cref="Guid.Empty"/> for null, empty, or malformed
    /// values instead of throwing. System.Text.Json rejects a null/invalid guid on a
    /// non-nullable Guid property; Newtonsoft yielded Guid.Empty. A single bad record in a
    /// page of parameters shouldn't fail the whole request.
    /// </summary>
    internal sealed class LenientGuidConverter : JsonConverter<Guid>
    {
        public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return Guid.TryParse(reader.GetString(), out var guid) ? guid : Guid.Empty;
            }

            return Guid.Empty;
        }

        public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}
