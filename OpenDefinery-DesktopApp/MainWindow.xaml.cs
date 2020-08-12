using OpenDefinery;
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
        public static Pager Pagination { get; set; }

        public MainWindow()
        {
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;

            InitializeComponent();

            // Instantiate a new objects
            Definery = new Definery();
            Pagination = new Pager();

            // Set current pagination fields
            Pagination.CurrentPage = 0;
            Pagination.ItemsPerPage = 50;
            Pagination.Offset = 0;

            // Disable and/or hide UI elements at launch of app
            PagerNextButton.IsEnabled = false;
            PagerPreviousButton.IsEnabled = false;
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
                Definery.Parameters = SharedParameter.GetParamsByUser(
                    Definery, Definery.CurrentUser.Name, Pagination.ItemsPerPage, Pagination.Offset
                    );
                DataGridParameters.ItemsSource = Definery.Parameters;

                // Update the pager UI elements
                Pagination = Pager.LoadData(Definery, Pagination.ItemsPerPage, Pagination.Offset);
                UpdatePagerUi(Pagination, 0);
            }
            else
            {
                MessageBox.Show("There was an error logging in. Please try again.\n\n" +
                    "Error: No CSRF token found.");
            }
        }

        /// <summary>
        /// Helper method to update the UI for the pagination
        /// </summary>
        /// <param name="pager">The Pager object to update</param>
        /// <param name="incrementChange">The increment in which to update the page number (can be positive or negative)</param>
        private void UpdatePagerUi(Pager pager, int incrementChange)
        {
            // Increment the current page
            pager.CurrentPage += incrementChange;

            // Set the Offset
            if (pager.CurrentPage == 0)
            {
                pager.Offset = 0;
            }
            if (pager.CurrentPage >= 1)
            {
                pager.Offset = pager.ItemsPerPage * pager.CurrentPage;
            }

            // Enable UI as needed
            if (pager.TotalPages > pager.CurrentPage + 1)
            {
                PagerNextButton.IsEnabled = true;
            }
            else
            {
                PagerNextButton.IsEnabled = false;
            }

            if (pager.CurrentPage <= pager.TotalPages - 1 && pager.CurrentPage >= 0)
            {
                PagerPreviousButton.IsEnabled = true;
            }
            if (pager.CurrentPage <= 0)
            {
                PagerPreviousButton.IsEnabled = false;
            }

            // Update the textbox
            PagerTextBox.Text = string.Format("Page {0} of {1} (Total Parameters: {2})", pager.CurrentPage + 1, pager.TotalPages, pager.TotalItems);

            // Set the object from the updated Pager object
            Pagination = pager;
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
                        var newParameter = SharedParameter.FromTxt(line);

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

            var loginResponse = Definery.Authenticate(Definery, username, password);

            // If the CSRF token was retrieved from Drupal
            if (!string.IsNullOrEmpty(Definery.CsrfToken))
            {
                // Store the auth code for GET requests
                Definery.AuthCode = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password));

                // Hide login form
                OverlayGrid.Visibility = Visibility.Hidden;
                LoginGrid.Visibility = Visibility.Hidden;
            }
            
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

        /// <summary>
        /// Method to execute when the Next button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PagerNextButton_Click(object sender, RoutedEventArgs e)
        {
            // Upate the pager data and UI
            UpdatePagerUi(Pagination, 1);

            // Load the data and display in the DataGrid
            Definery.Parameters = SharedParameter.GetParamsByUser(
                Definery, Definery.CurrentUser.Name, Pagination.ItemsPerPage, Pagination.Offset
                );
            DataGridParameters.ItemsSource = Definery.Parameters;
        }

        /// <summary>
        /// Method to execute when the Previous button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PagerPreviousButton_Click(object sender, RoutedEventArgs e)
        {
            // Upate the pager data and UI
            UpdatePagerUi(Pagination, -1);

            // Load the data and display in the DataGrid
            Definery.Parameters = SharedParameter.GetParamsByUser(
                Definery, Definery.CurrentUser.Name, Pagination.ItemsPerPage, Pagination.Offset
                );
            DataGridParameters.ItemsSource = Definery.Parameters;
        }
    }
}
