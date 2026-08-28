using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace OpenDefinery
{
    /// <summary>
    /// The envelope every v1 list endpoint returns: {count, next, previous, results}.
    /// Replaces Drupal's {rows, pager} shape.
    /// </summary>
    public class Paginated<T>
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        /// <summary>Absolute URL of the next page, or null on the last one.</summary>
        [JsonPropertyName("next")]
        public string Next { get; set; }

        [JsonPropertyName("previous")]
        public string Previous { get; set; }

        [JsonPropertyName("results")]
        public List<T> Results { get; set; }
    }

    /// <summary>Fetching one page, or every page, of a v1 list endpoint.</summary>
    public static class OdPage
    {
        /// <summary>
        /// The server's ceiling on ?page_size=. Asking for more is silently clamped to this,
        /// which is the trap <see cref="GetAll{T}"/> exists to avoid.
        /// </summary>
        public const int MaxPageSize = 1000;

        /// <summary>Stops a malformed `next` chain from looping forever.</summary>
        private const int MaxPages = 100;

        /// <summary>One page. Returns null if the call fails.</summary>
        public static Paginated<T> Get<T>(Definery definery, string url)
        {
            var response = OdHttp.Get(url, definery);

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                Debug.WriteLine(response.Content, "Error fetching " + url);
                return null;
            }

            try
            {
                return OdJson.Deserialize<Paginated<T>>(response.Content);
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine(ex.ToString(), "Error parsing " + url);
                return null;
            }
        }

        /// <summary>
        /// Every result, following `next` until it runs out.
        ///
        /// Worth using rather than one big page: the data categories alone number over 1,200
        /// against a ceiling of <see cref="MaxPageSize"/>, so a single request would quietly
        /// return a partial vocabulary and every lookup against the missing tail would fail
        /// as though the category did not exist.
        ///
        /// Never returns null - a failed call yields an empty list, because every caller
        /// enumerates the result directly.
        /// </summary>
        public static List<T> GetAll<T>(Definery definery, string url)
        {
            var all = new List<T>();
            var next = url;

            for (var page = 0; page < MaxPages && !string.IsNullOrEmpty(next); page++)
            {
                var chunk = Get<T>(definery, next);
                if (chunk == null) break;

                if (chunk.Results != null) all.AddRange(chunk.Results);
                next = chunk.Next;
            }

            return all;
        }

        /// <summary>Append a query parameter, choosing ? or &amp; as needed.</summary>
        public static string WithQuery(string url, string name, string value)
        {
            var separator = url.Contains("?") ? "&" : "?";
            return url + separator + name + "=" + System.Uri.EscapeDataString(value ?? string.Empty);
        }
    }
}
