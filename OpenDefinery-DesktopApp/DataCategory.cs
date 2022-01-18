using Newtonsoft.Json;
using OpenDefinery_DesktopApp;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OpenDefinery
{
    public class DataCategory
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("hashcode")]
        public string Hashcode { get; set; }

        /// <summary>
        /// Retrieve all DataCategoreis from Drupal.
        /// </summary>
        /// <param name="definery">The main Definery object provides the CSRF token.</param>
        /// <returns>A list of DataType objects.</returns>
        public static List<DataCategory> GetAll(Definery definery)
        {
            var dataCategories = new List<DataCategory>();

            var client = new RestClient(Definery.BaseUrl + "rest/datacategories?_format=json");
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("X-CSRF-Token", definery.CsrfToken);
            IRestResponse response = client.Execute(request);
            Console.WriteLine(response.Content);

            dataCategories = JsonConvert.DeserializeObject<List<DataCategory>>(response.Content);

            return dataCategories;
        }

        /// <summary>
        /// Retrieve a DataCategory using its hascode from the Revit API.
        /// </summary>
        /// <param name="definery">The main Definery object</param>
        /// <param name="hashcode">The hascode provided by the Revit API</param>
        /// <returns></returns>
        public static DataCategory GetByHashcode(Definery definery, string hashcode)
        {
            // Get DataCategory using the hashcode
            var dataCats = definery.DataCategories.Where(o => o.Hashcode == hashcode);

            // Only return one DataCategory
            if (dataCats.Count() == 1)
            {
                return dataCats.FirstOrDefault();
            }
            else
            {
                MessageBox.Show("Error retrieving Data Category.");

                return null;
            }
        }
    }
}
