using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;

namespace OpenDefinery
{
    public class DataType
    {
        [JsonPropertyName("pk")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// The token Revit writes in the DATATYPE column. Usually identical to
        /// <see cref="Name"/>; the API keeps them separate because export addresses it.
        /// </summary>
        [JsonPropertyName("revit_name")]
        public string ParameterTypeName { get; set; }

        /// <summary>
        /// Revit releases this type exists in. Empty means the API has not determined it yet
        /// rather than "incompatible" - the discipline-specific types are still unvalidated.
        /// </summary>
        [JsonPropertyName("revit_versions")]
        public List<int> RevitVersions { get; set; }

        /// <summary>
        /// Retrieve all DataTypes from OpenDefinery.
        /// </summary>
        public static List<DataType> GetAll(Definery definery)
        {
            return OdPage.GetAll<DataType>(
                definery,
                Definery.BaseUrl + "data-types/?page_size=" + OdPage.MaxPageSize);
        }

        /// <summary>
        /// Retrieve the DataType object from OpenDefinery from the name.
        /// </summary>
        /// <param name="definery">The main Definery object.</param>
        /// <param name="dataTypeName">The name the DataType to retrieve.</param>
        /// <returns>The DataType object.</returns>
        public static DataType GetFromName(Definery definery, string dataTypeName)
        {
            var foundDataTypes = definery.DataTypes.Where(g => g.Name.ToLower() == dataTypeName.ToLower());

            if (foundDataTypes.Count() == 1)
            {
                return foundDataTypes.FirstOrDefault();
            }
            if (foundDataTypes.Count() > 1)
            {
                Debug.WriteLine(String.Format(
                    "Multiple datatypes exist with the name {0}. Using the first or default.", dataTypeName
                    ));

                return foundDataTypes.FirstOrDefault();
            }

            return null;
        }

        /// <summary>
        /// Retrive the DataType ID from its name. This ID is useful when the DataType ID in OpenDefinery is required for an API call.
        /// </summary>
        /// <param name="allDataTypes">A list of all DataTypes typically sourced from the main Definery object.</param>
        /// <param name="dataTypeName">The nane of the DataType.</param>
        /// <returns>The DataType object.</returns>
        public static string GetIdFromName(List<DataType> allDataTypes, string dataTypeName)
        {
            var foundDataTypes = allDataTypes.Where(g => g.Name == dataTypeName);

            if (foundDataTypes.Count() == 1)
            {
                return foundDataTypes.FirstOrDefault().Id.ToString();
            }
            if (foundDataTypes.Count() > 1)
            {
                Debug.WriteLine(String.Format(
                    "Multiple data types exist with the name {0}. Using the first or default.", dataTypeName
                    ));

                return foundDataTypes.FirstOrDefault().Id.ToString();
            }

            return null;
        }

        /// <summary>
        /// Retrieve a DataType from its ParameterType Enumeration name.
        /// </summary>
        public static DataType GetByParamTypeName(string dataTypeName, List<DataType> dataTypes)
        {
            var dataType = dataTypes.Where(
                d => d.Name.Replace("_", string.Empty).ToLower() == dataTypeName.ToLower()).FirstOrDefault();

            if (dataType == null)
            {
                dataType = dataTypes.Where(d => d.ParameterTypeName == dataTypeName).FirstOrDefault();
            }

            return dataType;
        }
    }
}
