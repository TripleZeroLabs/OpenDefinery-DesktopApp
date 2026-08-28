using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;

namespace OpenDefinery
{
    public class Definery
    {
        public static string BaseUrl = "https://api.opendefinery.com/v1/";

        /// <summary>
        /// The DRF token from /v1/auth/token/, sent as "Authorization: Token &lt;key&gt;".
        /// Null for an anonymous session, which is still useful: reads are public.
        /// </summary>
        public string Token { get; set; }

        /// <summary>Whether this session can write. Reads work without it.</summary>
        public bool IsAuthenticated => !string.IsNullOrEmpty(Token);
        public List<Collection> MyCollections { get; set; }
        public List<Collection> PublishedCollections { get; set; }
        public List<Collection> AllCollections { get; set; }
        public Collection SelectedCollection { get; set; }
        public List<DefineryParameter> DefineryParameters { get; set; }
        public List<DefineryParameter> RevitParameters { get; set; }
        public List<DefineryParameter> ValidatedParams { get; set; }
        public ObservableCollection<DefineryParameter> Parameters { get; set; }
        public List<DataType> DataTypes { get; set; }
        public List<DataCategory> DataCategories { get; set; }
        public List<Group> Groups { get; set; }
        public User CurrentUser { get; set; }

        /// <summary>
        /// Login to OpenDefinery using a username and password.
        /// </summary>
        /// <param name="definery">The main Definery object</param>
        /// <param name="username">The OpenDefinery username to login</param>
        /// <param name="password">The password of the OpenDefinery user</param>
        /// <summary>
        /// The authenticated session for this Revit process, set by <see cref="Init"/> on
        /// success. Lets a second window (e.g. Export Parameters) reuse the sign-in instead
        /// of prompting again.
        /// </summary>
        public static Definery Current { get; private set; }

        /// <summary>
        /// Clear the current session: drop the published <see cref="Current"/> and reset the
        /// HTTP client so nothing is reused by the next sign-in.
        /// </summary>
        public static void SignOut()
        {
            Current = null;
            OdHttp.ResetSession();
        }

        public static Definery Init(Definery definery, string username, string password)
        {
            var body = "{\"username\": " + OdJson.ToJsonString(username) +
                       ", \"password\": " + OdJson.ToJsonString(password) + "}";
            var response = OdHttp.Post(BaseUrl + "auth/token/", body, definery);

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                Debug.WriteLine(response.Content, "Error Logging In");

                // Return the original Definery object to maintain previously set properties
                return definery;
            }

            try
            {
                // The token response carries the account as well as the key, so there is no
                // follow-up call to find out who just signed in.
                definery.Token = OdJson.GetString(response.Content, "token");
                definery.CurrentUser = new User
                {
                    Id = OdJson.GetString(response.Content, "pk"),
                    Name = OdJson.GetString(response.Content, "username"),
                    Email = OdJson.GetString(response.Content, "email")
                };

                // Publish the session so other windows can reuse this sign-in.
                Current = definery;

                return definery;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[" + ex.ToString() + "]" + response.Content, "Error Logging In");

                // Return the original Definery object to maintain previously set properties
                return definery;
            }
        }

        /// <summary>
        /// Rebuild a session from a stored token, without a username or password.
        ///
        /// "Remember me" persists the token rather than the credentials, so on restart there is
        /// a key but no idea whose it is; /v1/auth/user/ answers that. Returns null when the
        /// token has been revoked, which is the signal to prompt for a sign-in.
        /// </summary>
        public static Definery FromToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;

            var definery = new Definery { Token = token };
            var user = User.GetCurrent(definery);
            if (user == null) return null;

            definery.CurrentUser = user;
            Current = definery;

            return definery;
        }

        /// <summary>
        /// Main method to load all the data from OpenDefinery
        /// </summary>
        public static Definery LoadData(Definery definery)
        {
            // Load the data from OpenDefinery
            definery.Groups = Group.GetAll(definery);
            definery.DataTypes = DataType.GetAll(definery);
            definery.DataCategories = DataCategory.GetAll(definery);

            // Category names used to be trimmed here with Name.Split('_')[1], turning
            // "OST_Doors" into "Doors". The API computes that now and returns it as
            // DisplayName, so Name keeps the identifier it always was.

            // Sort the lists for future use by UI
            definery.DataTypes.Sort(delegate (DataType x, DataType y)
            {
                if (x.Name == null && y.Name == null) return 0;
                else if (x.Name == null) return -1;
                else if (y.Name == null) return 1;
                else return x.Name.CompareTo(y.Name);
            });

            // Sorted by what a picker shows, not by the OST_ identifier behind it.
            definery.DataCategories.Sort(delegate (DataCategory x, DataCategory y)
            {
                if (x.DisplayName == null && y.DisplayName == null) return 0;
                else if (x.DisplayName == null) return -1;
                else if (y.DisplayName == null) return 1;
                else return x.DisplayName.CompareTo(y.DisplayName);
            });

            return definery;
        }
    }
}
