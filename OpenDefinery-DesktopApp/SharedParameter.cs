﻿using KellermanSoftware.CompareNetObjects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenDefinery_DesktopApp;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;

namespace OpenDefinery
{
    public class SharedParameter
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("guid")]
        public Guid Guid { get; set; }
        
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("data_category")]
        public string DataCategory { get; set; }

        [JsonProperty("data_type")]
        public string DataType { get; set; }

        [JsonProperty("group")]
        public string Group { get; set; }

        [JsonProperty("user_modifiable")]
        public string UserModifiable { get; set; }

        [JsonProperty("visible")]
        public string  Visible { get; set; }

        public string BatchId { get; set; }

        /// <summary>
        /// Create a SharedParameter object from a line in a shared parameter text file (typically generated by Revit).
        /// </summary>
        /// <param name="txtLine">The line of text from the shared parmater text file.</param>
        /// <returns></returns>
        public static SharedParameter FromTxt(string txtLine)
        {
            var parameter = new SharedParameter();

            var values = txtLine.Split('\t');

            parameter.Guid = new Guid(values[1]);
            parameter.Name = values[2];
            parameter.DataType = values[3];
            parameter.DataCategory = values[4];
            parameter.Group = values[5];
            parameter.Visible = values[6];
            parameter.Description = values[7];
            parameter.UserModifiable = values[8];

            return parameter;
        }

        /// <summary>
        /// Retrieve SharedParameter objects which have a specific GUID.
        /// </summary>
        /// <param name="definery">The main Definery object provides the basic authorization code</param>
        /// <param name="guid">The GUID of the SharedParameters to retrieve</param>
        /// <returns>A list of SharedParameter objects</returns>
        public static List<SharedParameter> FromGuid(Definery definery, Guid guid)
        {
            var client = new RestClient(Definery.BaseUrl + "rest/params?_format=json&guid=" + guid);
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            IRestResponse response = client.Execute(request);

            JObject json = JObject.Parse(response.Content);

            var paramResponse = json.SelectToken("rows");

            if (paramResponse != null)
            {
                var parameters = JsonConvert.DeserializeObject<List<SharedParameter>>(paramResponse.ToString());

                return parameters;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Checks that an exact match of a SharedParameter already exists in Drupal. Useful for mitigating duplicates.
        /// </summary>
        /// <param name="definery">The main Definery object provides the basic auth token</param>
        /// <param name="newParameter">The SharedParameter to validate</param>
        /// <returns>True if a match is found, false if not</returns>
        public static bool HasExactMatch(Definery definery, SharedParameter newParameter)
        {
            // TODO: Only validate parameters from the current Drupal user
            var foundParams = FromGuid(definery, newParameter.Guid);
            var foundMatch = false;

            // Logic when one ore more SharedParameter is found in Drupal
            if (foundParams != null && foundParams.Count() > 0)
            {
                foreach (var p in foundParams)
                {
                    // Compare the two parameters
                    CompareLogic compareLogic = new CompareLogic();

                    compareLogic.Config.MembersToInclude.Add("Guid");
                    compareLogic.Config.MembersToInclude.Add("Name");
                    compareLogic.Config.MembersToInclude.Add("DataType");
                    compareLogic.Config.MembersToInclude.Add("DataCategory");
                    compareLogic.Config.MembersToInclude.Add("Visible");
                    compareLogic.Config.MembersToInclude.Add("Description");
                    compareLogic.Config.MembersToInclude.Add("UserModifiable");

                    ComparisonResult result = compareLogic.Compare(newParameter, p);

                    if (result.AreEqual)
                    {
                        foundMatch = true;

                        break;
                    }
                }
            }
            else
            {
                foundMatch = false;
            }

            return foundMatch;
        }

        /// <summary>
        /// Retrieve a page of ShareParameters from Drupal.
        /// </summary>
        /// <param name="definery">The main Definery object provides the basic auth code.</param>
        /// <param name="itemsPerPage">The number of items per page (only increments of 5, 10, 25, 50, and 100 are allowed)</param>
        /// <param name="offset">The offset of items from zero (i.e., to start page two at 50 items per page, this should be set to 50).</param>
        /// <returns>A list of SharedParameters</returns>
        public static List<SharedParameter> GetPage(Definery definery, int itemsPerPage, int offset)
        {
            var client = new RestClient(Definery.BaseUrl + 
                string.Format("rest/params?_format=json&items_per_page={0}&offset={1}", itemsPerPage, offset));
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            IRestResponse response = client.Execute(request);

            // Return the CSRF token if the response was OK
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                JObject json = JObject.Parse(response.Content);

                var paramResponse = json.SelectToken("rows");

                return JsonConvert.DeserializeObject<List<SharedParameter>>(paramResponse.ToString());

            }
            else
            {
                MessageBox.Show("There was an error getting the parameters.");

                return null;
            }
        }

        /// <summary>
        /// Retrieve all parameters from a specific User on Drupal.
        /// </summary>
        /// <param name="definery">The main Definery object provides the basic auth token</param>
        /// <param name="userName">The Drupal username of the user to check for</param>
        /// <param name="itemsPerPage">The number of items to return per page</param>
        /// <param name="offset">The offset of items to skip pages</param>
        /// <returns>A list of SharedParameter objects</returns>
        public static List<SharedParameter> GetParamsByUser(Definery definery, string userName, int itemsPerPage, int offset)
        {
            var client = new RestClient(Definery.BaseUrl + string.Format(
                "rest/params/user/{0}?_format=json&items_per_page={1}&offset={2}", userName, itemsPerPage, offset)
                );
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            IRestResponse response = client.Execute(request);

            // Logic if the response was OK
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                // Cast the rows from the reponse to a generic JSON object
                JObject json = JObject.Parse(response.Content);
                var paramResponse = json.SelectToken("rows");

                return JsonConvert.DeserializeObject<List<SharedParameter>>(paramResponse.ToString());
            }
            else
            {
                MessageBox.Show("There was an error getting the parameters.");

                return null;
            }
        }

        /// <summary>
        /// Creates a new Shared Parameter on Drupal
        /// </summary>
        /// <param name="definery"></param>
        /// <param name="param"></param>
        /// <param name="collectionId"></param>
        /// <returns></returns>
        public static string Create(Definery definery, SharedParameter param, string collectionId)
        {
            var client = new RestClient(Definery.BaseUrl + "node?_format=json");
            client.Timeout = -1;

            // Assign the datatype value by the Term ID defined by Drupal to pass to the API call (we cannot pass the name)
            var dataType = definery.DataTypes.Find(d => d.Name.ToString() == param.DataType);

            // Format values before assigning
            if (dataType != null)
            { 
                param.DataType = dataType.Id.ToString();
            }

            // Get the tag ID from Drupal. If the tag does not exist, create a tag and assign the ID to request body
            // First format the tag name accordingly...
            var tagName = Tag.FormatName(param.Group);

            // ... Then attempt to retrieve the tag from Drupal
            var tagId = Tag.GetIdFromName(definery, tagName);

            // ... If the tag does not exist, an empty array is returned. Create the tag if neccessary.
            if (tagId == "[]")
            {
                Debug.WriteLine(string.Format("The tag \"{0}\" does not exist. Creating...", tagName));

                // Create the tag
                var newTagId = Tag.Create(definery, tagName);
                Debug.WriteLine(string.Format("New tag created for {0} with ID: {1}", tagName, newTagId));

                // Get the ID of the newly created tag
                tagId = newTagId;
            }
            else
            {

            }

            //TODO: Clean up this mess some day.
            var requestBody = "{" +
                "\"type\": [{" +
                    "\"target_id\": \"shared_parameter\"" +
                "}]," +
                "\"title\": [{" +
                    "\"value\": \"" + param.Name + "\"" +
                "}]," +
                "\"field_guid\": [{" +
                    "\"value\": \"" + param.Guid.ToString() + "\"" +
                "}]," +
                "\"field_description\": [{" +
                    "\"value\": \"" + param.Description + "\"" +
                "}]," +
                 "\"field_batch_id\": [{" +
                    "\"value\": \"" + param.BatchId + "\"" +
                "}]," +
                // OpenDefinery ignores the out-of-the-box Revit parameter groups as they are not as robust as Collections
                "\"field_group\": {" +
                    "\"und\": \"41\"" +
                "}," +
                "\"field_data_type\": {" +
                "\"und\": \"" + param.DataType + "\"" +
                "}," +
                "\"field_collections\": {" +
                "\"und\": \"" + collectionId + "\"" +
                "}," +
                // Here we pass the existing parameter "group" from the text file and assign this as a tag instead of the group to maintain the data point
                "\"field_tags\": {" +
                "\"und\": \"" + tagId + "\"" +
                "}" +

            "}";

            var request = new RestRequest(Method.POST);
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            request.AddHeader("X-CSRF-Token", definery.CsrfToken);
            request.AddHeader("Content-Type", "application/json");
            request.AddParameter("application/json", requestBody, ParameterType.RequestBody);

            IRestResponse response = client.Execute(request);

            return response.Content;
        }

        public static void AddToCollection(Definery definery, SharedParameter param, Collection collection)
        {
            var client = new RestClient(string.Format(Definery.BaseUrl + "node/{0}?_format=json", param.Id));
            client.Timeout = -1;
            var request = new RestRequest(Method.PATCH);
            request.AddHeader("X-CSRF-Token", definery.CsrfToken);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", "Basic " + definery.AuthCode);
            request.AddParameter("application/json", "{" +
                "\"type\": [{" +
                        "\"target_id\": \"shared_parameter\"" +
                    "}]," +
                    "\"field_collections\": {" +
                        "\"und\": " + "\"" + collection.Id + "\"" +
                    "}" +
                "}", 
                ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);

            Debug.WriteLine(response.Content);
        }
    }
}
