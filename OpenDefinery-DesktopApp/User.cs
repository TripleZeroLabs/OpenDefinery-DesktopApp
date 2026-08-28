using System.Diagnostics;
using System.Text.Json.Serialization;

namespace OpenDefinery
{
    /// <summary>
    /// The signed-in account. Populated straight from the token response, which carries the
    /// identity alongside the key.
    ///
    /// There is no user directory in the v1 API - the old lookups by id and by username have
    /// no replacement, and had no callers. A definition's `author` is an integer primary key,
    /// compared against <see cref="Id"/> rather than resolved to a name.
    /// </summary>
    public class User
    {
        [JsonPropertyName("pk")]
        public string Id { get; set; }

        [JsonPropertyName("username")]
        public string Name { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        /// <summary>
        /// Who the session's token belongs to. Returns null if the token is not accepted,
        /// which is how a restored "remember me" session finds out it has been revoked.
        /// </summary>
        public static User GetCurrent(Definery definery)
        {
            if (definery == null || !definery.IsAuthenticated) return null;

            try
            {
                var response = OdHttp.Get(Definery.BaseUrl + "auth/user/", definery);

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    Debug.WriteLine(response.Content, "Error resolving the current user");
                    return null;
                }

                return OdJson.Deserialize<User>(response.Content);
            }
            catch
            {
                return null;
            }
        }
    }
}
