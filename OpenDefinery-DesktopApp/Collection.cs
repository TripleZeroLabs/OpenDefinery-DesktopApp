using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenDefinery_DesktopApp;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OpenDefinery
{
    public class Collection
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        /// <summary>
        /// Retrieve the currently logged in user's Collections.
        /// </summary>
        /// <param name="definery">The main Definery object provides the CSRF token.</param>
        /// <returns>A list of Collection objects</returns>
        public static List<Collection> ByCurrentUser(Definery definery)
        {
            var client = new RestClient(Definery.BaseUrl + "rest/collections?_format=json");
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            IRestResponse response = client.Execute(request);

            // Return the data if the response was OK
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                // If the user has no Collections, it returns an empty array
                // Only process the response if it is not an empty array
                if (response.Content != "[]")
                {
                    try
                    {
                        var collections = JsonConvert.DeserializeObject<List<Collection>>(response.Content);

                        return collections;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());

                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
            else
            {
                MessageBox.Show(response.StatusCode.ToString());

                return null;
            }
        }

        /// <summary>
        /// Retrieve all published Collections excluding the current user's Collections.
        /// </summary>
        /// <param name="definery"></param>
        /// <returns></returns>
        public static List<Collection> GetPublished(Definery definery)
        {
            var client = new RestClient(Definery.BaseUrl + "rest/collections/published?_format=json");
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            IRestResponse response = client.Execute(request);

            // Return the data if the response was OK
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                // If the user has no Collections, it returns an empty array
                // Only process the response if it is not an empty array
                if (response.Content != "[]")
                {
                    try
                    {
                        var collections = JsonConvert.DeserializeObject<List<Collection>>(response.Content);
                        var filteredCollections = new List<Collection>();

                        // Add Collection to filtered list only if it isn't authored by the current user
                        foreach(var collection in collections)
                        {
                            if (collection.Author != Definery.CurrentUser.Id)
                            {
                                filteredCollections.Add(collection);
                            }
                        }

                        return filteredCollections;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());

                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
            else
            {
                MessageBox.Show(response.StatusCode.ToString());

                return null;
            }
        }

        public static IRestResponse Create(Definery definery, string name, string description)
        {
            var newCollection = new Collection();

            var client = new RestClient(Definery.BaseUrl + "node?_format=hal_json");
            client.Timeout = -1;
            var request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("X-CSRF-Token", definery.CsrfToken);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            request.AddParameter("application/json", 
                "{" +
                    "\"type\":" +
                    "[{" +
                        "\"target_id\": \"collection\"" +
                    "}]," +
                    "\"title\":" +
                    "[{" +
                        "\"value\": \"" + name + "\"" +
                    "}]," +
                    "\"body\":" +
                    "[{" +
                        "\"value\": \"" + description + "\"" +
                    "}]" +
                "}", 

                ParameterType.RequestBody);

            IRestResponse response = client.Execute(request);

            Debug.WriteLine(response.Content);

            // TODO: Return the new Collection object rather than the response
            return response;
        }
    }
}
