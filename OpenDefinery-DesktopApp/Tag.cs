﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace OpenDefinery
{
    public class Tag
    {
        public string Id { get; set; }
        public Guid Uuid { get; set; }
        public string Name { get; set; }

        public static string Create(Definery definery, string tagName)
        {
            var client = new RestClient(Definery.BaseUrl + "taxonomy/term?_format=hal_json");
            client.Timeout = -1;
            var request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("X-CSRF-Token", definery.CsrfToken);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            request.AddParameter("application/json", 
                "{\"vid\": \"tags\"," +
                "\"name\": [" +
                    "{\"value\": \"" + tagName + "\"}" +
                    "]}", 
                ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);

            JObject json = JObject.Parse(response.Content);

            var tagId = json["tid"].FirstOrDefault()["value"].ToString();

            return tagId;
        }

        public static string GetIdFromName(Definery definery, string tagName)
        {
            var client = new RestClient(Definery.BaseUrl + string.Format("rest/tags/{0}?_format=json", tagName));
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            IRestResponse response = client.Execute(request);

            if (response.StatusCode == System.Net.HttpStatusCode.OK && response.Content != "[]")
            {
                var tags = JsonConvert.DeserializeObject<List<Tag>>(response.Content);

                return tags.FirstOrDefault().Id;
            }
            if (response.StatusCode == System.Net.HttpStatusCode.OK && response.Content == "[]")
            {
                return "[]";
            }
            else
            {
                MessageBox.Show("There was an error getting the ID of " + tagName + ".");
                return null;
            }
        }

        /// <summary>
        /// Helper method to format tag names
        /// </summary>
        /// <param name="tagName"></param>
        /// <returns></returns>
        public static string FormatName(string tagName)
        {
            var newTag = tagName;
            
            // Remove spaces
            newTag = newTag.Replace(" ", "");

            return newTag;
        }
    }
}
