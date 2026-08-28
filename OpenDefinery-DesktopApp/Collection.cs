using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenDefinery
{
    public class Collection
    {
        [JsonPropertyName("pk")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>The author's user id. Compare against Definery.CurrentUser.Id.</summary>
        [JsonPropertyName("author")]
        public string Author { get; set; }

        /// <summary>"public" or "private". Use <see cref="IsPublic"/> to read it as a flag.</summary>
        [JsonPropertyName("visibility")]
        public string Visibility { get; set; }

        [JsonIgnore]
        public bool IsPublic
        {
            get { return Visibility != "private"; }
            set { Visibility = value ? "public" : "private"; }
        }

        /// <summary>
        /// Returned on both the list and the detail payloads, so unlike the old backend there
        /// is nothing to fetch separately before editing.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>How many definitions the Collection holds. Read-only.</summary>
        [JsonPropertyName("definition_count")]
        public int DefinitionCount { get; set; }

        /// <summary>
        /// The requesting user's effective role: "author", "editor", "viewer", or null for no
        /// access beyond it being public. A better test than comparing <see cref="Author"/>,
        /// because it accounts for a Collection shared with you.
        /// </summary>
        [JsonPropertyName("my_role")]
        public string MyRole { get; set; }

        /// <summary>
        /// Revit releases this Collection works in, derived by the API from the data types and
        /// categories its definitions use. Not assignable.
        /// </summary>
        [JsonPropertyName("revit_versions")]
        public List<int> RevitVersions { get; set; }

        /// <summary>Whether the current user may change this Collection's contents.</summary>
        [JsonIgnore]
        public bool CanWrite => MyRole == "author" || MyRole == "editor";

        /// <summary>
        /// One page of Shared Parameters from a Collection.
        /// </summary>
        public static ObservableCollection<DefineryParameter> GetParameters(
            Definery definery, Collection collection, int itemsPerPage, int page, bool resetTotals)
        {
            var listOfParams = new List<DefineryParameter>();

            var url = Definery.BaseUrl + string.Format(
                "definitions/?collection={0}&page={1}&page_size={2}",
                collection.Id, page, itemsPerPage);

            var chunk = OdPage.Get<DefineryParameter>(definery, url);
            if (chunk != null && chunk.Results != null)
            {
                listOfParams = chunk.Results;
            }

            return new ObservableCollection<DefineryParameter>(listOfParams);
        }

        /// <summary>
        /// Every Shared Parameter in a Collection, across as many pages as it takes.
        /// </summary>
        public static ObservableCollection<DefineryParameter> GetParameters(
            Definery definery, Collection collection)
        {
            var paramsOut = new ObservableCollection<DefineryParameter>();

            if (definery != null && collection != null)
            {
                var all = OdPage.GetAll<DefineryParameter>(
                    definery,
                    Definery.BaseUrl + string.Format(
                        "definitions/?collection={0}&page_size={1}",
                        collection.Id, OdPage.MaxPageSize));

                paramsOut = new ObservableCollection<DefineryParameter>(all);
            }

            return paramsOut;
        }

        /// <summary>
        /// The Collections the signed-in user authored or has been granted a role on.
        ///
        /// An anonymous session has none, so this falls back to the public list rather than
        /// returning nothing at all - which is what the old two-route split did.
        /// </summary>
        public static List<Collection> ByCurrentUser(Definery definery)
        {
            if (definery == null || !definery.IsAuthenticated)
            {
                return GetPublished(definery);
            }

            return OdPage.GetAll<Collection>(
                definery,
                Definery.BaseUrl + "collections/?mine=true&page_size=" + OdPage.MaxPageSize);
        }

        /// <summary>
        /// Every Collection visible to this session: the public library, plus anything private
        /// the user can see. Never returns null - callers enumerate it directly.
        /// </summary>
        public static List<Collection> GetPublished(Definery definery)
        {
            return OdPage.GetAll<Collection>(
                definery,
                Definery.BaseUrl + "collections/?page_size=" + OdPage.MaxPageSize);
        }

        /// <summary>Retrieve one Collection by id, or null if it isn't visible.</summary>
        public static Collection GetById(Definery definery, int collectionId)
        {
            var response = OdHttp.Get(
                Definery.BaseUrl + string.Format("collections/{0}/", collectionId), definery);

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                Debug.WriteLine(response.Content, "Error retrieving the Collection");
                return null;
            }

            try
            {
                return OdJson.Deserialize<Collection>(response.Content);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                return null;
            }
        }

        /// <summary>
        /// The parameters in a Collection, for membership checks. The list payload already
        /// carries the GUID and id, so this is the same call as
        /// <see cref="GetParameters(Definery, Collection)"/> - kept as a name that says why
        /// it is being asked for.
        /// </summary>
        public static List<DefineryParameter> GetIds(Definery definery, Collection collection)
        {
            if (definery == null || collection == null) return new List<DefineryParameter>();

            return OdPage.GetAll<DefineryParameter>(
                definery,
                Definery.BaseUrl + string.Format(
                    "definitions/?collection={0}&page_size={1}",
                    collection.Id, OdPage.MaxPageSize));
        }

        /// <summary>
        /// Create a new Collection. Returns the created Collection, or null on failure.
        /// </summary>
        public static Collection Create(Definery definery, string name, string description, bool? isPublic)
        {
            var body =
                "{" +
                    "\"name\": " + OdJson.ToJsonString(name ?? string.Empty) + "," +
                    "\"description\": " + OdJson.ToJsonString(description ?? string.Empty) + "," +
                    "\"visibility\": \"" + (isPublic == true ? "public" : "private") + "\"" +
                "}";

            var response = OdHttp.Post(Definery.BaseUrl + "collections/", body, definery);
            Debug.WriteLine(response.Content);

            if (response.StatusCode == System.Net.HttpStatusCode.Created)
            {
                try
                {
                    // The API echoes the whole record back, so there is nothing to reconstruct.
                    return OdJson.Deserialize<Collection>(response.Content);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                    return null;
                }
            }

            Debug.WriteLine("There was an error creating the Collection.");

            return null;
        }

        /// <summary>
        /// Update a Collection's name, description, and visibility.
        /// Returns true when the API accepts the change.
        /// </summary>
        public static bool Update(Definery definery, int collectionId, string name, string description, bool isPublic)
        {
            var body =
                "{" +
                    "\"name\": " + OdJson.ToJsonString(name ?? string.Empty) + "," +
                    "\"description\": " + OdJson.ToJsonString(description ?? string.Empty) + "," +
                    "\"visibility\": \"" + (isPublic ? "public" : "private") + "\"" +
                "}";

            var response = OdHttp.Patch(
                Definery.BaseUrl + string.Format("collections/{0}/", collectionId), body, definery);

            Debug.WriteLine(response.Content);

            var code = (int)response.StatusCode;
            return code >= 200 && code < 300;
        }

        /// <summary>
        /// Delete a Collection. Its parameters are orphaned rather than destroyed.
        /// </summary>
        public static void Delete(Definery definery, int collectionId)
        {
            var response = OdHttp.Delete(
                Definery.BaseUrl + string.Format("collections/{0}/", collectionId), definery);

            Debug.WriteLine(response.Content);
        }

        /// <summary>
        /// Compares the shared parameters in the OpenDefinery Collection to the parameters
        /// extracted from the current Revit model.
        /// </summary>
        public static List<DefineryParameter> ValidateParameters(
            Definery definery,
            Collection collection,
            List<DefineryParameter> revitParams)
        {
            if (definery == null || collection == null)
            {
                Debug.Write("There was an error retrieving the Collection.", "Error retrieving collection.");

                return null;
            }

            var odParams = GetParameters(definery, collection).ToList();
            var validatedParams = new List<DefineryParameter>();

            // A GUID is unique within a Collection - the API enforces it, because a Revit file
            // cannot use the same GUID twice - so this matches at most one. The old "multiple
            // found, using the first" branch could only ever fire under the previous backend,
            // where one parameter belonged to many Collections at once.
            foreach (var p in revitParams)
            {
                var found = odParams.FirstOrDefault(o => o.Guid == p.Guid);

                if (found == null)
                {
                    p.IsStandard = false;
                    validatedParams.Add(p);
                }
                else
                {
                    validatedParams.Add(DefineryParameter.SetDefineryData(found, p));
                }
            }

            return validatedParams;
        }
    }
}
