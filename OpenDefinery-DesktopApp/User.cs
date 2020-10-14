using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OpenDefinery
{
    public class User
    {
        public string Id { get; set; }
        public string Name { get; set; }

        /// <summary>
        /// Retrieve a User by their user ID.
        /// </summary>
        /// <param name="definery">The main Definery object provides the auth code</param>
        /// <param name="userId">The ID of the user</param>
        /// <returns></returns>
        public static User GetById(Definery definery, int userId)
        {
            var users = new List<User>();
            var user = new User();

            try
            {
                var client = new RestClient(Definery.BaseUrl + string.Format("rest/user/id/{0}?_format=json", userId.ToString()));
                client.Timeout = -1;
                var request = new RestRequest(Method.GET);
                request.AddHeader("Authorization", "Basic " + definery.AuthCode);
                IRestResponse response = client.Execute(request);
                Console.WriteLine(response.Content);

                users = JsonConvert.DeserializeObject<List<User>>(response.Content);

                if (users.Count() == 1)
                {
                    return users.FirstOrDefault();
                }
                // If there are none or more than one result, return null
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// Retrieve a User by their username.
        /// </summary>
        /// <param name="definery">The main Definery object provides the auth code</param>
        /// <param name="userId">The name of the user</param>
        /// <returns></returns>
        public static User GetByUserName(Definery definery, string username)
        {
            var users = new List<User>();
            var user = new User();

            try
            {
                var client = new RestClient(Definery.BaseUrl + string.Format("rest/user/name/{0}?_format=json", username));
                client.Timeout = -1;
                var request = new RestRequest(Method.GET);
                request.AddHeader("Authorization", "Basic " + definery.AuthCode);
                IRestResponse response = client.Execute(request);
                Console.WriteLine(response.Content);

                users = JsonConvert.DeserializeObject<List<User>>(response.Content);

                if (users.Count() == 1)
                {
                    return users.FirstOrDefault();
                }
                // If there are none or more than one result, return null
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}