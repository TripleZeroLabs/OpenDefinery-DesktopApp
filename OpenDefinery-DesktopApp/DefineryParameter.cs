using System.Text.Json;
using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace OpenDefinery
{
    public class DefineryParameter
    {
        [JsonPropertyName("pk")]
        public int DefineryId { get; set; }

        [JsonPropertyName("guid")]
        public Guid Guid { get; set; }

        // 64-bit to match Revit 2024+ element ids (ElementId.Value is long).
        // Widening from int is backwards compatible for older models.
        [JsonIgnore]
        public long ElementId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("data_type")]
        public string DataType { get; set; }

        /// <summary>
        /// The value a shared parameter file carries in its DATACATEGORY column - the
        /// BuiltInCategory integer, e.g. "-2000023". Read from `data_category_hashcode`;
        /// written back as `data_category`, which the API accepts in this form.
        /// </summary>
        [JsonPropertyName("data_category_hashcode")]
        public string DataCategoryHashcode { get; set; }

        /// <summary>
        /// Write-only on the API: assigns a group by name, created on demand. Reads come back
        /// in <see cref="Groups"/>, because a parameter can carry several group tags.
        /// </summary>
        [JsonPropertyName("group")]
        public string Group { get; set; }

        [JsonPropertyName("groups")]
        public List<string> Groups { get; set; }

        // Booleans on the v1 API. They were "1"/"0" strings under the old backend, which is
        // still the shared parameter FILE format - see Flag() and the FromTxt/CreateParamTable
        // pair, where that conversion now lives.
        [JsonPropertyName("user_modifiable")]
        public bool UserModifiable { get; set; } = true;

        [JsonPropertyName("visible")]
        public bool Visible { get; set; } = true;

        /// <summary>The author's user id. Compare against Definery.CurrentUser.Id.</summary>
        [JsonPropertyName("author")]
        public string Author { get; set; }

        /// <summary>
        /// The Collection this parameter belongs to, or null if orphaned. A parameter lives in
        /// exactly one: adding it to another Collection copies it, so each holds its own.
        /// </summary>
        [JsonPropertyName("collection")]
        public int? CollectionId { get; set; }

        /// <summary>The requester's role: "author", "editor", "viewer", or null.</summary>
        [JsonPropertyName("my_role")]
        public string MyRole { get; set; }

        // Populated in code, never from the API payload.
        [JsonIgnore]
        public int ForkedSourceId { get; set; }

        /// <summary>The resolved <see cref="CollectionId"/>, for display. Set by the UI.</summary>
        [JsonIgnore]
        public Collection Collection { get; set; }

        [JsonIgnore]
        public bool IsStandard { get; set; }

        [JsonIgnore]
        public bool IsShared { get; set; }

        /// <summary>Client-side batch-upload correlation id (not sent to the API).</summary>
        [JsonIgnore]
        public string BatchId { get; set; }

        /// <summary>Whether the current user may edit this parameter.</summary>
        [JsonIgnore]
        public bool CanWrite => MyRole == "author" || MyRole == "editor";

        /// <summary>
        /// Render a flag the way a shared parameter file wants it. The API speaks booleans;
        /// the file format speaks "1" and "0".
        /// </summary>
        public static string Flag(bool value) => value ? "1" : "0";

        /// <summary>Read a flag out of a shared parameter file. Anything but "0" is true.</summary>
        public static bool ParseFlag(string value) =>
            string.IsNullOrWhiteSpace(value) || value.Trim() != "0";

        // Standard constructor. isVisible/isUserModifiable are the file format's "1"/"0"
        // strings, parsed here so callers reading a shared parameter file can pass columns
        // straight through.
        public DefineryParameter(
            Guid guid,
            string name,
            string dataTypeName,
            string dataCatHashcode,
            string groupId,
            string isVisible,
            string description,
            string isUserModifiable,
            bool isShared)
        {
            Guid = guid;
            Name = name;
            DataType = dataTypeName;
            DataCategoryHashcode = dataCatHashcode;
            Group = groupId;
            Visible = ParseFlag(isVisible);
            Description = description;
            UserModifiable = ParseFlag(isUserModifiable);
            IsShared = isShared;
        }

        // Lite constructor
        // Deliberately NOT [JsonConstructor]. System.Text.Json requires every constructor
        // parameter to bind to a property by name, and "id" does not match the DefineryId
        // property (JsonPropertyName is not used for constructor binding). Deserialization
        // uses the parameterless constructor below and sets properties via their setters,
        // which yields the same result for the "lite" (guid + id) payloads.
        public DefineryParameter(Guid guid, int id)
        {
            Guid = guid;
            DefineryId = id;
        }

        // Empty constructor
        public DefineryParameter()
        {

        }

        /// <summary>
        /// Create a DefineryParameter object from a line in a shared parameter text file (typically generated by Revit).
        /// </summary>
        /// <param name="txtLine">The line of text from the shared parmater text file.</param>
        /// <returns></returns>
        public static DefineryParameter FromTxt(Definery definery, string txtLine)
        {
            if (txtLine[0] != '#')  // Ignore the comment lines
            {
                var values = txtLine.Split('\t');

                var parameter = new DefineryParameter(
                    new Guid(values[1]),
                    values[2],
                    values[3],
                    values[4],
                    values[5],
                    values[6],
                    string.Empty,
                    "1",
                    true
                    );

                // Older shared parameter text files do not have the DESCRIPTION column
                if (values.Count() == 8)
                {
                    parameter.Description = string.Empty;
                    parameter.UserModifiable = ParseFlag(values[7]);
                }
                if (values.Count() == 9)
                {
                    parameter.Description = values[7];
                    parameter.UserModifiable = ParseFlag(values[8]);
                }

                return parameter;
            }

            return null;
        }

        /// <summary>
        /// Every parameter sharing a GUID.
        ///
        /// More than one is normal, not a conflict: a GUID is unique within a Collection but
        /// not across the library, so the same Revit parameter appears once per Collection
        /// holding a copy, each free to carry its own name.
        /// </summary>
        public static ObservableCollection<DefineryParameter> FromGuid(Definery definery, Guid guid)
        {
            var found = OdPage.GetAll<DefineryParameter>(
                definery,
                Definery.BaseUrl + string.Format(
                    "definitions/?guid={0}&page_size={1}", guid, OdPage.MaxPageSize));

            return new ObservableCollection<DefineryParameter>(found);
        }

        /// <summary>The copy of a parameter held by one specific Collection, or null.</summary>
        public static DefineryParameter FromGuidInCollection(
            Definery definery, Guid guid, int collectionId)
        {
            var found = OdPage.GetAll<DefineryParameter>(
                definery,
                Definery.BaseUrl + string.Format(
                    "definitions/?guid={0}&collection={1}", guid, collectionId));

            return found.FirstOrDefault();
        }

        /// <summary>
        /// Checks that an exact match of a SharedParameter already exists in OpenDefinery. Useful for mitigating duplicates.
        /// </summary>
        /// <param name="definery">The main Definery object provides the basic auth token</param>
        /// <param name="newParameter">The SharedParameter to validate</param>
        /// <returns>True if a match is found, false if not</returns>
        //public static bool HasExactMatch(Definery definery, DefineryParameter newParameter)
        //{
        //    var foundMatch = false;

        //    // Retrieve all Parameters from the GUID
        //    var foundParams = FromGuid(definery, newParameter.Guid);

        //    // Logic when one ore more DefineryParameter is found in OpenDefinery
        //    if (foundParams != null && foundParams.Count() > 1)
        //    {
        //        foreach (var p in foundParams)
        //        {
        //            // Only consider exact match if the current user is the author
        //            if (p.Author == definery.CurrentUser.Id)
        //            {
        //                // Compare the two parameters
        //                CompareLogic compareLogic = new CompareLogic();

        //                compareLogic.Config.MembersToInclude.Add("Guid");
        //                compareLogic.Config.MembersToInclude.Add("Name");
        //                compareLogic.Config.MembersToInclude.Add("DataType");
        //                compareLogic.Config.MembersToInclude.Add("DataCategory");
        //                compareLogic.Config.MembersToInclude.Add("Visible");
        //                compareLogic.Config.MembersToInclude.Add("Description");
        //                compareLogic.Config.MembersToInclude.Add("UserModifiable");

        //                ComparisonResult result = compareLogic.Compare(newParameter, p);

        //                if (result.AreEqual)
        //                {
        //                    // Break the loop if there is any Parameter that is equal
        //                    foundMatch = true;

        //                    break;
        //                }
        //                else
        //                {
        //                    foundMatch = false;
        //                }
        //            }
        //            else
        //            {
        //                foundMatch = false;
        //            }
        //        }
        //    }
        //    if (foundParams != null && foundParams.Count() == 0)
        //    {
        //        foundMatch = false;
        //    }

        //    return foundMatch;
        //}

        /// <summary>
        /// Retrieve a page of ShareParameters from OpenDefinery.
        /// </summary>
        /// <param name="definery">The main Definery object provides the basic auth code.</param>
        /// <param name="itemsPerPage">The number of items per page (only increments of 5, 10, 25, 50, and 100 are allowed)</param>
        /// <param name="offset">The offset of items from zero (i.e., to start page two at 50 items per page, this should be set to 50).</param>
        /// <param name="resetTotals">Clear the total pages and items from the pager to start over?</param>
        /// <returns>A list of SharedParameters</returns>
        public static ObservableCollection<DefineryParameter> GetPage(
            Definery definery, int itemsPerPage, int page, bool resetTotals, Pager pager = null)
        {
            var chunk = OdPage.Get<DefineryParameter>(
                definery,
                Definery.BaseUrl + string.Format(
                    "definitions/?page={0}&page_size={1}", page, itemsPerPage));

            if (chunk == null) return null;

            pager?.Update(chunk, resetTotals);

            return new ObservableCollection<DefineryParameter>(
                chunk.Results ?? new List<DefineryParameter>());
        }

        /// <summary>
        /// Resolve each parameter's Collection id to the Collection itself, for display.
        ///
        /// Replaces the old routine that expanded a comma-separated list of Collection ids -
        /// a parameter belongs to exactly one Collection now.
        /// </summary>
        public static ObservableCollection<DefineryParameter> SetCollections(
            Definery definery, ObservableCollection<DefineryParameter> parameters)
        {
            var known = definery?.PublishedCollections;
            if (known == null) return parameters;

            foreach (var p in parameters)
            {
                if (p.CollectionId.HasValue)
                {
                    p.Collection = known.FirstOrDefault(c => c.Id == p.CollectionId.Value);
                }
            }

            return parameters;
        }

        /// <summary>
        /// Retrieve a page of parameters that belong to a specific Collection.
        /// </summary>
        public static ObservableCollection<DefineryParameter> ByCollection(
            Definery definery, Collection collection, int itemsPerPage, int page,
            bool resetTotals, Pager pager = null)
        {
            var chunk = OdPage.Get<DefineryParameter>(
                definery,
                Definery.BaseUrl + string.Format(
                    "definitions/?collection={0}&page={1}&page_size={2}",
                    collection.Id, page, itemsPerPage));

            if (chunk == null)
            {
                Debug.WriteLine("There was an error getting the parameters.");
                return new ObservableCollection<DefineryParameter>();
            }

            pager?.Update(chunk, resetTotals);

            return SetCollections(definery, new ObservableCollection<DefineryParameter>(
                chunk.Results ?? new List<DefineryParameter>()));
        }

        /// <summary>
        /// Whether a Collection already holds this parameter with identical content.
        ///
        /// Rewritten for the one-Collection-per-parameter model. It used to fetch every copy
        /// of a GUID across the library and compare only those the current user authored,
        /// which asked the wrong question: what matters is whether *this* Collection already
        /// has it. It also listed "DataCategory" among the members to compare, and no such
        /// member exists - so the category was silently never part of the match.
        ///
        /// The API enforces one GUID per Collection regardless, so this is an early exit to
        /// save a doomed write rather than the thing standing between you and a duplicate.
        /// </summary>
        public static bool HasExactMatch(
            Definery definery, DefineryParameter newParameter, int? collectionId = null)
        {
            if (definery == null || newParameter == null) return false;

            var candidates = collectionId.HasValue
                ? new List<DefineryParameter>
                  {
                      FromGuidInCollection(definery, newParameter.Guid, collectionId.Value)
                  }
                : FromGuid(definery, newParameter.Guid).ToList();

            return candidates.Any(p => p != null && IsSameContent(p, newParameter));
        }

        /// <summary>The fields that decide whether two parameters are the same parameter.</summary>
        private static bool IsSameContent(DefineryParameter a, DefineryParameter b)
        {
            return a.Guid == b.Guid
                && string.Equals(a.Name, b.Name, StringComparison.Ordinal)
                && string.Equals(a.DataType, b.DataType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.DataCategoryHashcode ?? string.Empty,
                                 b.DataCategoryHashcode ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(a.Description ?? string.Empty,
                                 b.Description ?? string.Empty, StringComparison.Ordinal)
                && a.Visible == b.Visible
                && a.UserModifiable == b.UserModifiable;
        }

        /// <summary>
        /// Async version of Create (used by batch upload for throughput). Returns the parameter
        /// with its new node id set, without an extra GET round-trip.
        /// </summary>
        public static async Task<DefineryParameter> CreateAsync(Definery definery, DefineryParameter param, int? collectionId = null, int? forkedId = null)
        {
            var response = await OdHttp.PostAsync(
                Definery.BaseUrl + "definitions/", BuildBody(param, collectionId), definery);

            if (response.StatusCode == System.Net.HttpStatusCode.Created)
            {
                // The API echoes the created record, so the id comes back without a second call.
                var created = OdJson.Deserialize<DefineryParameter>(response.Content);
                param.DefineryId = created.DefineryId;
                return param;
            }

            Debug.WriteLine("Error creating Shared Parameter: " + response.Content);
            return null;
        }

        /// <summary>
        /// The request body for creating or updating a definition.
        ///
        /// Far shorter than the Drupal equivalent because the API takes values rather than
        /// term ids: a data type by name, a category by the hashcode a Revit file carries, and
        /// a group by name - each created on demand if it is new. That removed the lookup and
        /// create-if-missing dance that used to run before every write.
        /// </summary>
        private static string BuildBody(DefineryParameter param, int? collectionId)
        {
            var body = "{" +
                "\"guid\": " + OdJson.ToJsonString(param.Guid.ToString()) + "," +
                "\"name\": " + OdJson.ToJsonString(param.Name ?? string.Empty) + "," +
                "\"description\": " + OdJson.ToJsonString(param.Description ?? string.Empty) + "," +
                "\"data_type\": " + OdJson.ToJsonString(param.DataType ?? string.Empty) + "," +
                "\"visible\": " + (param.Visible ? "true" : "false") + "," +
                "\"user_modifiable\": " + (param.UserModifiable ? "true" : "false");

            if (!string.IsNullOrWhiteSpace(param.DataCategoryHashcode))
            {
                body += ",\"data_category\": " + OdJson.ToJsonString(param.DataCategoryHashcode);
            }

            if (!string.IsNullOrWhiteSpace(param.Group))
            {
                body += ",\"group\": " + OdJson.ToJsonString(param.Group);
            }

            if (collectionId.HasValue)
            {
                body += ",\"collection\": " + collectionId.Value;
            }

            return body + "}";
        }

        /// <summary>
        /// Retrieve the Shared Parameters that don't belong to any Collections
        /// </summary>
        /// <param name="definery"></param>
        /// <param name="collection"></param>
        /// <returns></returns>
        public static ObservableCollection<DefineryParameter> GetOrphaned(
            Definery definery, int itemsPerPage, int page, bool resetTotals, Pager pager = null)
        {
            return Fetch(
                definery,
                Definery.BaseUrl + string.Format(
                    "definitions/?orphaned=true&page={0}&page_size={1}", page, itemsPerPage),
                resetTotals, pager);
        }

        /// <summary>
        /// Run a definitions query and fold the result into the pager. Every paged read here
        /// differs only by its query string.
        /// </summary>
        private static ObservableCollection<DefineryParameter> Fetch(
            Definery definery, string url, bool resetTotals, Pager pager)
        {
            var chunk = OdPage.Get<DefineryParameter>(definery, url);

            if (chunk == null)
            {
                Debug.WriteLine("There was an error getting the parameters.");
                return new ObservableCollection<DefineryParameter>();
            }

            pager?.Update(chunk, resetTotals);

            return SetCollections(definery, new ObservableCollection<DefineryParameter>(
                chunk.Results ?? new List<DefineryParameter>()));
        }

        /// <summary>
        /// Search for Shared Parmeters by keyword, GUID, or data type in a single query.
        /// </summary>
        /// <param name="definery">The main Definery object</param>
        /// <param name="searchQuery">The term(s) to search for</param>
        /// <returns></returns>
        public static ObservableCollection<DefineryParameter> Search(
            Definery definery, string searchQuery, int itemsPerPage, int page,
            bool resetTotals, Pager pager = null)
        {
            return Search(definery, searchQuery, null, itemsPerPage, page, resetTotals, pager);
        }

        /// <summary>
        /// Search by keyword or GUID, optionally narrowed to one Data Type.
        ///
        /// `?search=` covers name, description and GUID in one query - including a partial
        /// GUID, which the old backend could not do.
        /// </summary>
        public static ObservableCollection<DefineryParameter> Search(
            Definery definery,
            string searchQuery,
            string dataTypeName,
            int itemsPerPage,
            int page,
            bool resetTotals,
            Pager pager = null
            )
        {
            var url = Definery.BaseUrl + string.Format(
                "definitions/?page={0}&page_size={1}", page, itemsPerPage);

            url = OdPage.WithQuery(url, "search", searchQuery ?? string.Empty);

            if (!string.IsNullOrWhiteSpace(dataTypeName))
            {
                url = OdPage.WithQuery(url, "data_type", dataTypeName);
            }

            return Fetch(definery, url, resetTotals, pager);
        }

        /// <summary>
        /// Creates a new Shared Parameter on OpenDefinery
        /// Response codes:
        ///     201: Created
        ///     422: Unprocessable entity (possibly missing a required field)
        /// </summary>
        /// <param name="definery"></param>
        /// <param name="param"></param>
        /// <param name="collectionId"></param>
        /// <returns></returns>
        public static DefineryParameter Create(
            Definery definery,
            DefineryParameter param,
            int? collectionId = null,
            int? forkedId = null,
            string updatedName = null,
            string updatedDescription = null)
        {
            // Apply a new name/description only when provided (callers that create from an
            // already-populated param pass null here and must keep the existing values).
            if (updatedName != null) param.Name = updatedName;
            if (updatedDescription != null) param.Description = updatedDescription;

            var response = OdHttp.Post(
                Definery.BaseUrl + "definitions/", BuildBody(param, collectionId), definery);

            Debug.WriteLine(response.Content);

            if (response.StatusCode == System.Net.HttpStatusCode.Created)
            {
                // The response is the created record, so unlike the old backend - which
                // returned a node envelope and needed a second GET to read the parameter back
                // - this is the parameter.
                return OdJson.Deserialize<DefineryParameter>(response.Content);
            }

            Debug.WriteLine("There was an error creating the Shared Parameter: " + response.Content);

            return null;
        }

        /// <summary>Retrieve one Shared Parameter by its id.</summary>
        public static DefineryParameter FromId(Definery definery, int definitionId)
        {
            var response = OdHttp.Get(
                Definery.BaseUrl + string.Format("definitions/{0}/", definitionId), definery);

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                Debug.WriteLine(response.Content, "Error retrieving the Shared Parameter");
                return null;
            }

            return OdJson.Deserialize<DefineryParameter>(response.Content);
        }

        /// <summary>
        /// Copy a Shared Parameter into a Collection.
        ///
        /// Adding is copying: a parameter belongs to exactly one Collection, so the target
        /// gets its own instance to rename and edit without touching the original. The
        /// response reports which definitions were copied and which were already present
        /// under that GUID, so a caller can tell the two apart without comparing anything.
        /// </summary>
        public static OdResponse AddToCollection(
            Definery definery, DefineryParameter param, int newCollectionId)
        {
            var body = "{\"definitions\": [" + param.DefineryId + "]}";

            var response = OdHttp.Post(
                Definery.BaseUrl + string.Format("collections/{0}/definitions/", newCollectionId),
                body, definery);

            Debug.WriteLine(response.Content);

            return response;
        }

        /// <summary>
        /// Whether a copy request actually created something, as opposed to finding the GUID
        /// already there. Both are a 200, and the difference is in the body.
        /// </summary>
        public static bool WasCopied(OdResponse response)
        {
            if (response == null || !response.IsSuccessStatusCode) return false;

            var copied = OdJson.GetPropertyRaw(response.Content, "copied");

            return !string.IsNullOrEmpty(copied) && copied.Trim() != "[]";
        }

        /// <summary>
        /// Remove a Shared Parameter from a Collection.
        ///
        /// This DELETES that Collection's copy. Under the previous backend one parameter
        /// belonged to many Collections and this unlinked it from one; now the row belongs to
        /// the Collection, so removing it and deleting it are the same act. Copies held by
        /// other Collections are untouched.
        /// </summary>
        public static bool RemoveCollection(
            Definery definery, DefineryParameter param, int removedCollectionId)
        {
            var response = OdHttp.Delete(
                Definery.BaseUrl + string.Format(
                    "collections/{0}/definitions/{1}/", removedCollectionId, param.DefineryId),
                definery);

            Debug.WriteLine(response.Content);

            return response.IsSuccessStatusCode;
        }

        /// <summary>
        /// Modify an existing Shared Parameter's name and description.
        /// </summary>
        public static void Modify(Definery definery, DefineryParameter param, string name, string description)
        {
            var body = "{" +
                "\"name\": " + OdJson.ToJsonString(name ?? string.Empty) + "," +
                "\"description\": " + OdJson.ToJsonString(description ?? string.Empty) +
                "}";

            var response = OdHttp.Patch(
                Definery.BaseUrl + string.Format("definitions/{0}/", param.DefineryId),
                body, definery);

            Debug.WriteLine(response.Content);
        }

        /// <summary>
        /// Retrieve data from the OpenDefinery parameters and pass to parameters from Revit
        /// </summary>
        /// <param name="defineryParams">A list of OpenDefinery parameters from a particular collection</param>
        /// <param name="revitParams">A list of Revit parameters retrieved from a project</param>
        /// <returns></returns>
        public static DefineryParameter SetDefineryData(DefineryParameter defineryParam, DefineryParameter revitParam)
        {
            if (defineryParam != null && revitParam != null)
            {
                var updatedParam = defineryParam;
                updatedParam.ElementId = revitParam.ElementId;
                updatedParam.Name = revitParam.Name;
                updatedParam.IsStandard = true;
                updatedParam.IsShared = true;

                return updatedParam;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Generate a tab delimited string of shared parameters (including header)
        /// </summary>
        /// <param name="paramList">The list of parameters to convert</param>
        /// <returns></returns>
        public static string CreateParamTable(List<DefineryParameter> paramList)
        {
            // Instatiate string with header row of TSV
            var output = "" +
                "# This is a Revit shared parameter file which has been generated by OpenDefinery." +
                "\r\n" +
                "# Do not edit manually." +
                "\r\n" +
                "*META\tVERSION\tMINVERSION\r\nMETA\t2\t1\r\n*GROUP\tID\tNAME\r\nGROUP\t1\tDefault" +
                "\r\n";
            output += "*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE\n";

            // Add a line of text for each DefineryParameter from the list
            foreach (var p in paramList)
            {
                output += "PARAM\t";
                output += p.Guid + "\t";
                output += p.Name + "\t";
                output += p.DataType + "\t";
                output += p.DataCategoryHashcode + "\t";
                //output += p.Group + "\t";
                output += "1\t";  // Assign the "Default Group" until more robust group system is in place
                // Flag(), not the bool: the file wants "1"/"0", and ToString() would write
                // "True"/"False", which Revit does not read back.
                output += Flag(p.Visible) + "\t";
                output += p.Description + "\t";
                output += Flag(p.UserModifiable) + "\t";

                // Finally, add a new line
                output += '\n';
            }

            return output;
        }
    }
}
