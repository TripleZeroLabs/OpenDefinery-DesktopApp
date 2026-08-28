using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace OpenDefinery
{
    /// <summary>
    /// A parameter group name.
    ///
    /// This used to mirror a Drupal taxonomy term - a hundred lines of tid/vid/langcode/
    /// revision_* wrappers, each a single-element list. The v1 API returns {pk, name}.
    ///
    /// Groups behave as tags rather than a single field: the same parameter arriving from two
    /// files under two different group names keeps both. Writing one is just a name on the
    /// definition payload, which the API creates on demand - hence no Create method here, and
    /// no Tag class any more.
    /// </summary>
    public class Group
    {
        [JsonPropertyName("pk")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        public override string ToString() => Name;

        public static List<Group> GetAll(Definery definery)
        {
            return OdPage.GetAll<Group>(
                definery,
                Definery.BaseUrl + "parameter-groups/?page_size=" + OdPage.MaxPageSize);
        }

        /// <summary>
        /// Look up a group name in a shared parameter file's GROUP table by its id.
        ///
        /// Nothing to do with the API: those ids are local to the file they came from, which is
        /// exactly why they cannot be resolved without the file's own table.
        /// </summary>
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
