﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenDefinery_DesktopApp;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDefinery
{
    public class Pager
    {
        // Ignoring the current_page property from Drupal response until it reports the correct value
        //[JsonProperty("current_page")]
        public int CurrentPage { get; set; }

        [JsonProperty("total_items")]
        public int TotalItems { get; set; }

        [JsonProperty("total_pages")]
        public int TotalPages { get; set; }

        [JsonProperty("items_per_page")]
        public int ItemsPerPage { get; set; }

        // The Offset is not included in the Drupal response, so it must be set elsewhere
        public int Offset { get; set; }
        public bool IsFirstPage { get; set; }
        public bool IsLastPage { get; set; }

        /// <summary>
        /// Helper method to update the Pager object based on a Response
        /// </summary>
        /// <param name="responseContent">The IRestResponse.Content value as a string</param>
        /// <returns>The new Pager object</returns>
        public static Pager SetFromParamReponse(string responseContent, bool resetTotals)
        {
            // Instantiate a new pager
            var pager = new Pager();

            // Cast the rows from the reponse to a generic JSON object
            JObject json = JObject.Parse(responseContent);

            // Add the Drupal pager data to the Pager object
            var pagerResponse = json.SelectToken("pager");
            pager = JsonConvert.DeserializeObject<Pager>(pagerResponse.ToString());

            // Add the MainWindow data to the Pager object
            pager.CurrentPage = MainWindow.Pager.CurrentPage;

            // Always reassign values for total pages and items because the pager property from Drupal is relative to the current request,
            // however we always want to report the absolute totals if they are greater than zero.
            if (!resetTotals)
            {
                pager.TotalPages = MainWindow.Pager.TotalPages;
                pager.TotalItems = MainWindow.Pager.TotalItems;
            }
            else
            {
                // Do nothing with the pager
            }

            return pager;
        }
    }
}