using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace OpenDefinery
{
    public class DataCategory
    {
        [JsonPropertyName("pk")]
        public long Id { get; set; }

        /// <summary>The BuiltInCategory member, e.g. "OST_Doors". An identifier, not a label.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// What to show a user, e.g. "Doors". Computed by the API: Revit's own label where it
        /// has been harvested, otherwise derived from <see cref="Name"/>. Bind pickers to this.
        /// </summary>
        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        /// <summary>
        /// The value a shared parameter file carries in its DATACATEGORY column - the
        /// BuiltInCategory integer, e.g. "-2000023". This identifies the category, and it is
        /// the only form Revit hands the add-in.
        /// </summary>
        [JsonPropertyName("hashcode")]
        public string Hashcode { get; set; }

        /// <summary>Revit releases this category exists in.</summary>
        [JsonPropertyName("revit_versions")]
        public List<int> RevitVersions { get; set; }

        /// <summary>
        /// Retrieve all DataCategories from OpenDefinery.
        ///
        /// Over 1,200 of them, so this spans more than one page - see
        /// <see cref="OdPage.GetAll{T}"/>.
        /// </summary>
        public static List<DataCategory> GetAll(Definery definery)
        {
            return OdPage.GetAll<DataCategory>(
                definery,
                Definery.BaseUrl + "data-categories/?page_size=" + OdPage.MaxPageSize);
        }

        /// <summary>
        /// Retrieve a DataCategory using its hascode from the Revit API.
        /// </summary>
        /// <param name="definery">The main Definery object</param>
        /// <param name="hashcode">The hascode provided by the Revit API</param>
        /// <returns></returns>
        public static DataCategory GetByHashcode(Definery definery, string hashcode)
        {
            // Get DataCategory using the hashcode
            var dataCats = definery.DataCategories.Where(o => o.Hashcode == hashcode);

            // Only return one DataCategory
            if (dataCats.Count() == 1)
            {
                return dataCats.FirstOrDefault();
            }
            else
            {
                Debug.WriteLine("Error retrieving Data Categories (duplicate hashcodes).");

                return null;
            }
        }
    }
}
