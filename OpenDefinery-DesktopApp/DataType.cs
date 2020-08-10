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