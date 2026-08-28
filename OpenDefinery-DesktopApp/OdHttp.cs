using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;


namespace OpenDefinery
{
    /// <summary>
    /// Result of an HTTP call. Mirrors the members the old RestSharp IRestResponse exposed
    /// (<see cref="StatusCode"/> / <see cref="Content"/>) so call sites stay stable.
    /// </summary>
    public sealed class OdResponse
    {
        public HttpStatusCode StatusCode { get; set; }
        public string Content { get; set; }
        public bool IsSuccessStatusCode { get; set; }
    }

    /// <summary>
    /// Minimal HTTP transport built on a single shared <see cref="HttpClient"/>, replacing
    /// RestSharp (a known DLL-conflict source inside Revit/Dynamo). All requests flow through
    /// here, so authentication, headers, and API versioning are configured in ONE place —
    /// which is what made the move from the Drupal backend to the v1 API a change to
    /// <see cref="ApplyAuth"/> and <see cref="Definery.BaseUrl"/> rather than to every caller.
    /// </summary>
    public static class OdHttp
    {
        // One HttpClient for the process lifetime (avoids socket exhaustion).
        private static HttpClient _client = CreateClient();

        private static HttpClient CreateClient()
        {
            // Token auth carries the whole session in a header, so no cookies are wanted.
            // Turning them off keeps a stale sessionid from an earlier browsable-API visit
            // from being sent alongside a token.
            return new HttpClient(new HttpClientHandler { UseCookies = false });
        }

        /// <summary>
        /// Start a fresh client, dropping any pooled connection state.
        ///
        /// Under the old Drupal backend this was mandatory before every login, because
        /// /user/login rejected an already-authenticated caller. Token auth has no such rule,
        /// so this is now only about not reusing a connection across a sign-out.
        /// </summary>
        public static void ResetSession()
        {
            // Not disposing the previous client: any in-flight request keeps using it and
            // the connection pool is reclaimed by the GC. Sign-out happens rarely.
            _client = CreateClient();
        }

        public static OdResponse Get(string url, Definery definery)
            => Send(HttpMethod.Get, url, definery, null);

        public static OdResponse Post(string url, string jsonBody, Definery definery)
            => Send(HttpMethod.Post, url, definery, jsonBody);

        public static OdResponse Patch(string url, string jsonBody, Definery definery)
            => Send(new HttpMethod("PATCH"), url, definery, jsonBody);

        public static OdResponse Delete(string url, Definery definery)
            => Send(HttpMethod.Delete, url, definery, null);

        public static OdResponse Send(HttpMethod method, string url, Definery definery, string jsonBody)
            => SendAsync(method, url, definery, jsonBody).GetAwaiter().GetResult();

        public static async Task<OdResponse> PostAsync(string url, string jsonBody, Definery definery)
            => await SendAsync(HttpMethod.Post, url, definery, jsonBody).ConfigureAwait(false);

        /// <summary>
        /// POST a body that is not JSON — used by the import endpoints, which take a shared
        /// parameter file as a raw text/plain body.
        /// </summary>
        public static OdResponse PostText(string url, string body, Definery definery)
            => SendAsync(HttpMethod.Post, url, definery, body, "text/plain")
                .GetAwaiter().GetResult();

        public static async Task<OdResponse> SendAsync(
            HttpMethod method, string url, Definery definery, string body,
            string contentType = "application/json")
        {
            using (var request = new HttpRequestMessage(method, url))
            {
                ApplyAuth(request, definery);

                if (body != null)
                {
                    request.Content = new StringContent(body, Encoding.UTF8, contentType);
                }

                using (var response = await _client.SendAsync(request).ConfigureAwait(false))
                {
                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    return new OdResponse
                    {
                        StatusCode = response.StatusCode,
                        Content = content,
                        IsSuccessStatusCode = response.IsSuccessStatusCode
                    };
                }
            }
        }

        /// <summary>
        /// Central place to attach credentials. The v1 API uses DRF token auth: one opaque key
        /// sent as "Authorization: Token &lt;key&gt;", obtained from /v1/auth/token/. Reads are
        /// open to anonymous callers, so a missing token is not an error here — the request
        /// simply goes out unauthenticated and the API returns what the public can see.
        /// </summary>
        private static void ApplyAuth(HttpRequestMessage request, Definery definery)
        {
            if (definery == null || string.IsNullOrEmpty(definery.Token))
            {
                return;
            }

            request.Headers.TryAddWithoutValidation(
                "Authorization", "Token " + definery.Token);
        }
    }
}
