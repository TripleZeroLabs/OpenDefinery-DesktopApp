using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenDefinery_DesktopApp;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDefinery
{
    public class Pager
    {
        // Ignoring the current_page property from Drupal response until it reports the correct value
        //[JsonProperty("current_page")]
        public int CurrentPage { get; set; }

        [JsonProperty("total_items")]
        public int TotalItems { get; set; }

        [JsonProperty("total_pages")]
        public int TotalPages { get; set; }

        [JsonProperty("items_per_page")]
        public int ItemsPerPage { get; set; }

        // The Offset is not included in the Drupal response, so it must be set elsewhere
        public int Offset { get; set; }

        /// <summary>
        /// Retrieve the Pager data from Drupal using an API call.
        /// </summary>
        /// <param name="definery">The main Definery object provides the basic auth code</param>
        /// <param name="itemsPerPage">The number of items per page</param>
        /// <param name="offset">The number of items to skip for the current call</param>
        /// <returns>A Pager object with total_pages and total_items set</returns>
        public static Pager LoadData (Definery definery, int itemsPerPage, int offset)
        {
            // TODO: Find a way to get this total without making a full API call. 
            // Or can we implement this in the main SharedParameters.LoadData() method?
            var pager = new Pager();

            // First get a small subset of data from the first page to retrieve the pager values
            var client = new RestClient(Definery.BaseUrl + string.Format(
                "rest/params/user/{0}?_format=json&items_per_page={1}&offset={2}", Definery.CurrentUser.Name, itemsPerPage, offset)
                );
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            IRestResponse response = client.Execute(request);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                // Cast the rows from the reponse to a generic JSON object
                JObject json = JObject.Parse(response.Content);

                // Add the Drupal pager data to the Pager object
                var pagerResponse = json.SelectToken("pager");
                pager = JsonConvert.DeserializeObject<Pager>(pagerResponse.ToString());

                // Set the Pager object to the MainWindow instance
                MainWindow.Pagination = pager;
            }

            return pager;
        }
    }
}
