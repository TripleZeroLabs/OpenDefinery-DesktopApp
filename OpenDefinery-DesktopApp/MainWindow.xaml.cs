﻿using OpenDefinery;
using RestSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace OpenDefinery_DesktopApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public Definery Definery { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            // Instantiate a new Definery object
            Definery = new Definery();
        }

        /// <summary>
        /// Main method to load all the data from Drupal
        /// </summary>
        private void LoadData()
        {
            if (!string.IsNullOrEmpty(Definery.CsrfToken))
            {
                // Load the data from Drupal
                //Definery.Parameters = SharedParameter.GetParamsByUser(Definery, Definery.CurrentUser.Name);
                Definery.Groups = Group.GetAll(Definery);
                Definery.DataTypes = DataType.GetAll(Definery);

                // Display Collections in listbox
                Definery.Collections = Collection.GetAll(Definery);
                CollectionsList.DisplayMemberPath = "Name";
                CollectionsList.ItemsSource = Definery.Collections;

                // Get the parameters of the logged in user by default and display in the DataGrid
                Definery.Parameters = SharedParameter.GetParamsByUser(Definery, Definery.CurrentUser.Name);
                DataGridParameters.ItemsSource = Definery.Parameters;
            }
            else
            {
                MessageBox.Show("There was an error logging in. Please try again.\n\n" +
                    "Error: No CSRF token found.");
            }
        }

        /// <summary>
        /// Method to execute when the Upload button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BttnUpload_Click(object sender, RoutedEventArgs e)
        {
            // Generate an ID for the batch upload
            var batchId = Guid.NewGuid().ToString();

            var introTable = string.Empty;
            var metaDataTable = string.Empty;
            var groupTable = string.Empty;
            var parameterTable = string.Empty;

            var parameters = new List<SharedParameter>();

            DataTable datatable = new DataTable();

            // Read the text file and split the tables based on Revit's shared parameter file format
            try
            {
                using (StreamReader streamReader = new StreamReader(TxtBoxSpPath.Text))
                {
                    var text = streamReader.ReadToEnd();
                    var tables = text.Split('*');
                    char[] delimiter = new char[] { '\t' };

                    // Store parsed data in strings
                    introTable = tables[0];
                    metaDataTable = tables[1];
                    groupTable = tables[2];
                    parameterTable = tables[3];
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            // Parse the parameters string and cast each line to SharedParameter class
            using (StringReader stringReader = new StringReader(parameterTable))
            {
                string line = string.Empty;
                string headerLine = stringReader.ReadLine();
                do
                {
                    line = stringReader.ReadLine();
                    if (line != null)
                    {
                        // Cast tab delimited line from shared parameter text file to SharedParameter object
                        var newParameter = SharedParameter.FromTxt(Definery, line);

                        // Get the name of the group and assign this to the property rather than the ID 
                        // This name will be passed to the Create() method to add as the tag
                        var groupName = Group.GetNameFromTable(groupTable, newParameter.Group);
                        newParameter.Group = groupName;

                        // Check if the parameter exists
                        if (SharedParameter.HasExactMatch(Definery, newParameter))
                        {
                            // Do nothing for now
                            // TODO: Add existing SharedParameters to a log or report of some kind.
                            Debug.WriteLine(newParameter.Name + " exists. Skipping");
                        }
                        else
                        {
                            newParameter.BatchId = batchId;
                            var response = SharedParameter.Create(Definery, newParameter, CollectionIdTextBox.Text);

                            Debug.WriteLine(response);
                        }
                    }

                } while (line != null);
            }
        }

        /// <summary>
        /// Method to execute when the Login button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameTextBox.Text;
            var password = PasswordPasswordBox.Password;

            Definery.Authenticate(Definery, username, password);
            Definery.AuthCode = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password));
            
            // Load all of the things!!!
            LoadData();
        }

        /// <summary>
        /// Method to execute when the Refresh button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }
    }
}
