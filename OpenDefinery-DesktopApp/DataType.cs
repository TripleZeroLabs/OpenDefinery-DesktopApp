using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace OpenDefinery
{
    public class DataType
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Retrieve all DataTypes from Drupal.
        /// </summary>
        /// <param name="definery">The main Definery object provides the CSRF token.</param>
        /// <returns>A list of DataType objects.</returns>
        public static List<DataType> GetAll(Definery definery)
        {
            var dataTypes = new List<DataType>();

            var client = new RestClient(Definery.BaseUrl + "rest/datatypes?_format=json");
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("X-CSRF-Token", definery.CsrfToken);
            IRestResponse response = client.Execute(request);
            Console.WriteLine(response.Content);

            dataTypes = JsonConvert.DeserializeObject<List<DataType>>(response.Content);

            return dataTypes;
        }

        /// <summary>
        /// Retrieve the DataType object from Drupal from the name.
        /// </summary>
        /// <param name="allDataTypes">A list of all DataTypes typically sourced from the main Definery object.</param>
        /// <param name="dataTypeName">The name the DataType to retrieve.</param>
        /// <returns>The DataType object.</returns>
        public static DataType GetFromName(List<DataType> allDataTypes, string dataTypeName)
        {
            var foundDataTypes = allDataTypes.Where(g => g.Name == dataTypeName);

            if (foundDataTypes.Count() == 1)
            {
                return foundDataTypes.FirstOrDefault();
            }
            if (foundDataTypes.Count() > 1)
            {
                MessageBox.Show(String.Format("Multiple datatypes exist with the name {0}. Using the first or default.", dataTypeName));

                return foundDataTypes.FirstOrDefault();
            }

            return null;
        }

        /// <summary>
        /// Retrive the DataType ID from its name. This ID is useful when the DataType ID in Drupal is required for an API call.
        /// </summary>
        /// <param name="allDataTypes">A list of all DataTypes typically sourced from the main Definery object.</param>
        /// <param name="dataTypeName">The nane of the DataType.</param>
        /// <returns>The DataType object.</returns>
        public static string GetIdFromName(List<DataType> allDataTypes, string dataTypeName)
        {
            var foundDataTypes = allDataTypes.Where(g => g.Name == dataTypeName);

            if (foundDataTypes.Count() == 1)
            {
                return foundDataTypes.FirstOrDefault().Id.ToString();
            }
            if (foundDataTypes.Count() > 1)
            {
                MessageBox.Show(String.Format("Multiple data types exist with the name {0}. Using the first or default.", dataTypeName));

                return foundDataTypes.FirstOrDefault().Id.ToString();
            }

            return null;
        }
    }
}