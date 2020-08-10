using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace OpenDefinery
{
    public class Group
    {
        public List<Tid> tid { get; set; }
        public List<Uuid> uuid { get; set; }
        public List<RevisionId> revision_id { get; set; }
        public List<Langcode> langcode { get; set; }
        public List<Vid> vid { get; set; }
        public List<RevisionCreated> revision_created { get; set; }
        public List<object> revision_user { get; set; }
        public List<object> revision_log_message { get; set; }
        public List<Status> status { get; set; }
        public List<Name> name { get; set; }
        public List<Description> description { get; set; }
        public List<Weight> weight { get; set; }
        public List<Parent> parent { get; set; }
        public List<Changed> changed { get; set; }
        public List<DefaultLangcode> default_langcode { get; set; }
        public List<RevisionTranslationAffected> revision_translation_affected { get; set; }
        public List<Path> path { get; set; }

        public class Tid
        {
            public int value { get; set; }
        }

        public class Uuid
        {
            public string value { get; set; }
        }

        public class RevisionId
        {
            public int value { get; set; }
        }

        public class Langcode
        {
            public string value { get; set; }
        }

        public class Vid
        {
            public string target_id { get; set; }
            public string target_type { get; set; }
            public string target_uuid { get; set; }
        }

        public class RevisionCreated
        {
            public DateTime value { get; set; }
            public string format { get; set; }
        }

        public class Status
        {
            public bool value { get; set; }
        }

        public class Name
        {
            public string value { get; set; }
        }

        public class Description
        {
            public object value { get; set; }
            public object format { get; set; }
            public string processed { get; set; }
        }

        public class Weight
        {
            public int value { get; set; }
        }

        public class Parent
        {
            public object target_id { get; set; }
        }

        public class Changed
        {
            public DateTime value { get; set; }
            public string format { get; set; }
        }

        public class DefaultLangcode
        {
            public bool value { get; set; }
        }

        public class RevisionTranslationAffected
        {
            public bool value { get; set; }
        }

        public class Path
        {
            public object alias { get; set; }
            public object pid { get; set; }
            public string langcode { get; set; }
        }

        public class Root
        {
            public List<Group> GroupArray { get; set; }
        }

        public static List<Group> GetAll(Definery definery)
        {
            var groups = new List<Group>();

            try
            {
                var client = new RestClient(Definery.BaseUrl + "rest/groups?_format=json");
                client.Timeout = -1;
                var request = new RestRequest(Method.GET);
                request.AddHeader("Authorization", "Basic " + definery.AuthCode);
                IRestResponse response = client.Execute(request);
                Console.WriteLine(response.Content);

                groups = JsonConvert.DeserializeObject<List<Group>>(response.Content);

                return groups;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                return null;
            }
        }

        public static Group GetGroupFromName(List<Group> allGroups, string groupName)
        {

            var foundGroups = allGroups.Where(g => g.name.FirstOrDefault().value == groupName);

            if (foundGroups.Count() == 1)
            {
                return foundGroups.FirstOrDefault();
            }
            if (foundGroups.Count() > 1)
            {
                MessageBox.Show(String.Format("Multiple groups with the name {0}. Using the first or default.", groupName));

                return foundGroups.FirstOrDefault();
            }
            return null;
        }

        public static string GetNameFromTable(string tableOfGroups, string groupId)
        {
            var groupName = string.Empty;
            var groups = Regex.Split(tableOfGroups, "\r\n");

            groups = groups.Skip(1).ToArray();

            foreach (string line in groups)
            {
                if (line != null && line.Contains(groupId.ToString()))
                {
                    groupName = line.Split('\t').Last();
                }
            }

            return groupName;
        }
    }
}
