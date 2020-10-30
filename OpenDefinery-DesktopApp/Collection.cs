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

        /// <summary>
        /// Create a new Collction.
        /// </summary>
        /// <param name="definery">The main Definery object</param>
        /// <param name="name">The name of the Colllection</param>
        /// <param name="description">The description of the Collection</param>
        /// <returns></returns>
        public static Collection Create(Definery definery, string name, string description)
        {
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

            // Deserialize the response to a generic Node first
            if (response.StatusCode.ToString() == "Created")
            {
                var genericNode = JsonConvert.DeserializeObject<Node>(response.Content);

                // Instantiate the collection by retrieving it via the API
                var newCollection = GetById(definery, genericNode.Nid[0].Value);

                return newCollection;
            }
            else
            {
                MessageBox.Show("There was an error creating the Collection.");

                return null;
            }
        }

        public static Collection GetById(Definery definery, int collectionId)
        {
            var client = new RestClient(Definery.BaseUrl + string.Format("node/{0}?_format=json", collectionId.ToString()));
            var request = new RestRequest(Method.GET);
            request.AddHeader("Content-Type", "application/json");
            //request.AddHeader("X-CSRF-Token", definery.CsrfToken);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);

            IRestResponse response = client.Execute(request);
            Debug.WriteLine(response.Content);

            // Instantiate a new Collection from response
            if (response.StatusCode.ToString() == "OK")
            { 
                var foundCollection = JsonConvert.DeserializeObject<Collection>(response.Content);

                return foundCollection;
            }
            else
            {
                MessageBox.Show("There was an error retrieving the Collection.");

                return null;
            }

        }

        /// <summary>
        /// Check that a Collection has duplicate GUIDs.
        /// </summary>
        /// <param name="collection">The Collection to check</param>
        /// <param name="guid">The GUID to check for</param>
        /// <returns></returns>
        public static bool HasDuplicateGuids(Collection collection, Guid guid)
        {
            var hasDuplicate = false;



            return hasDuplicate;
        }

        /// <summary>
        /// Retrieve a list of Collections from a comma separated values string (typically returned from the API).
        /// </summary>
        /// <param name="definery">The main Definery object</param>
        /// <param name="collectionsString">A comma separated values string of Collection IDs</param>
        /// <returns></returns>
        public static SharedParameter GetFromString(Definery definery, SharedParameter parameter, string collectionsString)
        {
            var collections = new List<Collection>();

            // Get multiple Collections
            if (!string.IsNullOrEmpty(collectionsString) && collectionsString.Contains(","))
            {
                var strings = collectionsString.Split(',');

                foreach (var s in strings)
                {
                    // Get Collection from ID
                    var foundCollections = definery.AllCollections.Where(o => o.Id.ToString() == s.Trim());

                    foreach (var foundCollection in foundCollections)
                    {
                        // Add Collection to list
                        collections.Add(foundCollection);
                    }
                }
            }
            // Get a single Collection
            if (!string.IsNullOrEmpty(collectionsString) && !collectionsString.Contains(","))
            {
                // Get Collection from ID
                var foundCollection = definery.AllCollections.Where(o => o.Id.ToString() == collectionsString.Trim()).FirstOrDefault();

                // Add Collection to list
                collections.Add(foundCollection);
            }
            
            // Set the new list to the SharedParameter property and return
            parameter.Collections = collections;

            return parameter;
        }
    }
}
