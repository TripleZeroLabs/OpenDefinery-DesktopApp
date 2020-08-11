using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Get all Collections from Drupal.
        /// </summary>
        /// <param name="definery">The main Definery object provides the CSRF token.</param>
        /// <returns>A list of Collection objects</returns>
        public static List<Collection> GetAll(Definery definery)
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

    }
}
