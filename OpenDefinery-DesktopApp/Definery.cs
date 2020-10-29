using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OpenDefinery
{
    public class Definery
    {
        public static string BaseUrl = "http://app.opendefinery.com/";
        public string CsrfToken { get; set; }
        public string AuthCode { get; set; }

        public List<Collection> MyCollections { get; set; }
        public List<Collection> PublishedCollections { get; set; }
        public List<Collection> AllCollections { get; set; }
        public ObservableCollection<SharedParameter> Parameters { get; set; }
        public List<DataType> DataTypes { get; set; }
        public List<DataCategory> DataCategories { get; set; }
        public List<Group> Groups { get; set; }
        public static User CurrentUser { get; set; }
        
        /// <summary>
        /// Login to Drupal using a username and password.
        /// </summary>
        /// <param name="definery">The main Definery object</param>
        /// <param name="username">The Drupal username to login</param>
        /// <param name="password">The password of the Drupal user</param>
        public static System.Net.HttpStatusCode Authenticate(Definery definery, string username, string password)
        {
            var client = new RestClient(BaseUrl + "user/login?_format=json");
            client.Timeout = -1;
            var request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "application/json");
            request.AddParameter("application/json", "{\r\n    \"name\": \""+ username + "\",\r\n    \"pass\": \"" + password + "\"\r\n}", ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);

            // Return the CSRF token if the response was OK
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                JObject json = JObject.Parse(response.Content);

                // Assign tokens to Definery members
                definery.CsrfToken = json.SelectToken("csrf_token").ToString();

                // Add logged in user data
                CurrentUser = new User();
                CurrentUser.Id = json.SelectToken("current_user.uid").ToString();
                CurrentUser.Name = json.SelectToken("current_user.name").ToString();
            }
            else
            {
                MessageBox.Show("There was an error logging in.");
            }

            return response.StatusCode;
        }
    }
}
