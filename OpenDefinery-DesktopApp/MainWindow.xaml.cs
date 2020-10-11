using OpenDefinery;
using RestSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
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
using System.Windows.Threading;
using RestSharp;

namespace OpenDefinery_DesktopApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public Definery Definery { get; set; }
        public static Pager Pager { get; set; }
        public static Collection SelectedCollection { get; set; }
        ParameterSource ParamSource { get; set; }

        public MainWindow()
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            InitializeComponent();

            // Instantiate a new objects
            Definery = new Definery();
            Pager = new Pager();

            // Set current pagination fields
            Pager.CurrentPage = 0;
            Pager.ItemsPerPage = 50;
            Pager.Offset = 0;

            // Set up UI elements at launch of app
            AddToCollectionGrid.Visibility = Visibility.Hidden;  // The Add to Collection form
            NewParameterGrid.Visibility = Visibility.Hidden;  // The New Parameter form
            NewCollectionGrid.Visibility = Visibility.Hidden;  // The New Collection form
            BatchUploadGrid.Visibility = Visibility.Hidden;  // The Batch Upload form
            AddToCollectionButton.Visibility = Visibility.Collapsed;  // Add to Collection button
            RemoveFromCollectionButton.Visibility = Visibility.Collapsed;  // Remove from Collection button
            ExportCollectionButton.Visibility = Visibility.Collapsed;  // Export TXT button
            CloneParameterButton.Visibility = Visibility.Collapsed;  // Clone Parameter button
            ProgressGrid.Visibility = Visibility.Hidden;  // Main Progress Bar

            PagerPanel.Visibility = Visibility.Hidden;  // Pager
            PagerNextButton.IsEnabled = false;  // Pager
            PagerPreviousButton.IsEnabled = false;  // Pager

            PropertiesSideBar.Visibility = Visibility.Collapsed;

            ParamSource = ParameterSource.None;  // Make the ParameterSource none until there is some action

            if (string.IsNullOrEmpty(Definery.AuthCode) | string.IsNullOrEmpty(Definery.CsrfToken))
            {
                OverlayGrid.Visibility = Visibility.Visible;
                LoginGrid.Visibility = Visibility.Visible;

                UsernameTextBox.Focus();
            }
        }

        /// <summary>
        /// Main method to load all the data from Drupal
        /// </summary>
        private void LoadData()
        {
            if (!string.IsNullOrEmpty(Definery.CsrfToken))
            {
                // Load the data from Drupal
                Definery.Groups = Group.GetAll(Definery);
                Definery.DataTypes = DataType.GetAll(Definery);

                // Sort the lists for future use by UI
                Definery.DataTypes.Sort(delegate (DataType x, DataType y)
                {
                    if (x.Name == null && y.Name == null) return 0;
                    else if (x.Name == null) return -1;
                    else if (y.Name == null) return 1;
                    else return x.Name.CompareTo(y.Name);
                });

                // Pass the DataType list to the comboboxes and configure
                NewParamDataTypeCombo.ItemsSource = Definery.DataTypes;
                NewParamDataTypeCombo.DisplayMemberPath = "Name";  // Displays the name rather than object in the combobox
                NewParamDataTypeCombo.SelectedIndex = 0;  // Always select the default item so it cannot be left blank
                PropComboDataType.ItemsSource = Definery.DataTypes;
                PropComboDataType.DisplayMemberPath = "Name";  // Displays the name rather than object in the combobox

                // Display Collections in listboxes
                Definery.MyCollections = Collection.ByCurrentUser(Definery);
                CollectionsList.DisplayMemberPath = "Name";
                CollectionsList.ItemsSource = Definery.MyCollections;

                Definery.AllCollections = Collection.AllPublished(Definery);
                CollectionsList_Published.DisplayMemberPath = "Name";
                CollectionsList_Published.ItemsSource = Definery.AllCollections;

                // Update the main Pager object
                Pager.CurrentPage = 0;
                UpdatePager(Pager, 0);

                // Update the GUI anytime data is loaded
                RefreshUi();
            }
            else
            {
                // Do nothing for now.
            }
        }

        /// <summary>
        /// Helper method to update the UI for the pagination
        /// </summary>
        /// <param name="pager">The Pager object to update</param>
        /// <param name="incrementChange">The increment in which to update the page number (can be positive or negative). Note the first page number is 0.</param>
        public static void UpdatePager(Pager pager, int incrementChange)
        {
            // Get the current instance of the application window
            MainWindow mw = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();

            // Increment the current page
            pager.CurrentPage += incrementChange;

            // Set the Offset
            if (pager.CurrentPage == 0)
            {
                pager.IsFirstPage = true;
                pager.IsLastPage = false;

                // Set the new offset to 0
                pager.Offset = 0;

                // Toggle the UI
                mw.PagerNextButton.IsEnabled = true;
                mw.PagerPreviousButton.IsEnabled = false;
            }
            if (pager.CurrentPage >= 1)
            {
                pager.IsFirstPage = false;

                // Set the new offset based on the items per page and current page
                pager.Offset = pager.ItemsPerPage * pager.CurrentPage;
            }

            // Enable UI as needed
            if (pager.CurrentPage < pager.TotalPages - 1)
            {
                mw.PagerNextButton.IsEnabled = true;
            }
            if (pager.CurrentPage == pager.TotalPages - 1)
            {
                mw.PagerNextButton.IsEnabled = false;
                pager.IsLastPage = true;
            }

            if (pager.CurrentPage <= pager.TotalPages - 1 && pager.CurrentPage >= 0)
            {
                mw.PagerPreviousButton.IsEnabled = true;
            }
            if (pager.CurrentPage < 1)
            {
                mw.PagerPreviousButton.IsEnabled = false;
                pager.IsFirstPage = true;
            }

            // Disable the pager buttons if there is only one page
            if (pager.CurrentPage == 0 & pager.TotalPages == 1)
            {
                mw.PagerNextButton.IsEnabled = false;
                mw.PagerPreviousButton.IsEnabled = false;

                // First and last page are both true when there is only one page
                pager.IsFirstPage = true;
                pager.IsLastPage = true;
            }

            // Update the textbox
            mw.PagerTextBox.Text = string.Format("Page {0} of {1} (Total Parameters: {2})", pager.CurrentPage + 1, pager.TotalPages, pager.TotalItems);

            // Set the object from the updated Pager object
            Pager = pager;
        }

        /// <summary>
        /// Method to execute when the Upload button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void BttnUpload_Click(object sender, RoutedEventArgs e)
        {
            // Toggle the UI
            BatchUploadGrid.Visibility = Visibility.Hidden;
            ProgressGrid.Visibility = Visibility.Visible;

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

            try
            {
                // Parse the parameters string and cast each line to SharedParameter class
                using (StringReader stringReader = new StringReader(parameterTable))
                {
                    // Instantiate the progress
                    MainProgressBar.Maximum = parameterTable.Split('\n').Length;

                    var progress = new Progress<int>(value => MainProgressBar.Value = value);
                    var currentProgress = 0;

                    string line = string.Empty;
                    string headerLine = stringReader.ReadLine();

                    await Task.Run(() =>
                    {
                        do
                        {
                            line = stringReader.ReadLine();
                            if (line != null)
                            {
                                // Cast tab delimited line from shared parameter text file to SharedParameter object
                                var newParameter = SharedParameter.FromTxt(line);

                                // Update the UI
                                currentProgress += 1;
                                ((IProgress<int>)progress).Report(currentProgress);
                                Dispatcher.Invoke(() =>
                                {
                                    ProgressStatus.Text = "Uploading " + newParameter.Name + "...";
                                });

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

                                    Application.Current.Dispatcher.BeginInvoke(
                                      DispatcherPriority.Background,
                                      new Action(() =>
                                      {
                                          ProgressStatus.Text += " Already exists. Skipping.";
                                      }));
                                }
                                else
                                {
                                    newParameter.BatchId = batchId;

                                    // Instantiate the selected item as a Collection
                                    this.Dispatcher.Invoke(() =>
                                    {
                                        var sollection = BatchUploadCollectionCombo.SelectedItem as Collection;

                                        // Create the SharedParameter
                                        var response = SharedParameter.Create(Definery, newParameter, sollection.Id);

                                        Debug.WriteLine(response);
                                    });

                                    Application.Current.Dispatcher.BeginInvoke(
                                      DispatcherPriority.Background,
                                      new Action(() =>
                                      {
                                          ProgressStatus.Text += "Done.";
                                      }));
                                }
                            }

                        } while (line != null);
                    });
                }
            }
            catch (Exception ex)
            {
                // Toggle the UI
                ProgressGrid.Visibility = Visibility.Hidden;
                BatchUploadGrid.Visibility = Visibility.Visible;

                MessageBox.Show(ex.ToString());
            }

            // Update the UI
            ProgressGrid.Visibility = Visibility.Hidden;
            OverlayGrid.Visibility = Visibility.Hidden;
            MessageBox.Show("Batch upload complete.");

            // Reload the data
            // Get the parameters of the logged in user by default and display in the DataGrid
            Definery.Parameters = SharedParameter.ByUser(
                Definery, Definery.CurrentUser.Name, Pager.ItemsPerPage, Pager.Offset, true
                );

            // Update the GUI anytime data is loaded
            RefreshUi();
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
            // Reset page back to 1
            Pager.CurrentPage = 0;

            // Upate the pager data and UI
            UpdatePager(Pager, 0);

            // Load all of the things!!!
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
            UpdatePager(Pager, 1);

            // Load the data based on the current source and display in the DataGrid
            if (ParamSource == ParameterSource.Collection)
            {
                Definery.Parameters = SharedParameter.ByCollection(
                  Definery, SelectedCollection, Pager.ItemsPerPage, Pager.Offset, false
                  );
            }
            if (ParamSource == ParameterSource.Search)
            {
                Definery.Parameters = SharedParameter.Search(
                  Definery, SearchTxtBox.Text, Pager.ItemsPerPage, Pager.Offset, false
                  );
            }

            RefreshUi();
        }

        /// <summary>
        /// Method to execute when the Previous button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PagerPreviousButton_Click(object sender, RoutedEventArgs e)
        {
            // Upate the pager data and UI
            UpdatePager(Pager, -1);

            if (CollectionsList.SelectedItems.Count > 0 && ParamSource == ParameterSource.Collection)
            {
                // Load the data based on selected Collection and display in the DataGrid
                Definery.Parameters = SharedParameter.ByCollection(
                    Definery, SelectedCollection, Pager.ItemsPerPage, Pager.Offset, false
                    );
                DataGridParameters.ItemsSource = Definery.Parameters;
            }
            if (ParamSource == ParameterSource.Search)
            {
                Definery.Parameters = SharedParameter.Search(
                  Definery, SearchTxtBox.Text, Pager.ItemsPerPage, Pager.Offset, false
                  );
            }

            RefreshUi();
        }

        /// <summary>
        ///  Method to execute when the Batch Upload button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BatchUploadOverlayButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear form
            TxtBoxSpPath.Text = string.Empty;

            // Populate the Collections combo
            BatchUploadCollectionCombo.ItemsSource = Definery.MyCollections;
            BatchUploadCollectionCombo.DisplayMemberPath = "Name";
            BatchUploadCollectionCombo.SelectedIndex = 0;

            // Show the batch upload form
            OverlayGrid.Visibility = Visibility.Visible;
            BatchUploadGrid.Visibility = Visibility.Visible;
        }

        private void BatchUploadCancel_Click(object sender, RoutedEventArgs e)
        {
            // Hide the batch upload form
            OverlayGrid.Visibility = Visibility.Hidden;
            BatchUploadGrid.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// Method to execute when the Add to Collection button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddToCollectionButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if the current user has any Collections first
            if (Definery.MyCollections == null)
            {
                MessageBox.Show("You do not have any Collections yet. Once you have created a collection, you may add Shared Parameters to it.");
            }
            else
            {
                AddToCollectionCombo.DisplayMemberPath = "Name";  // Displays the Collection name rather than object in the Add to Collections combobox
                AddToCollectionCombo.SelectedIndex = 0;  // Always select the default item so it cannot be left blank
                
                if (DataGridParameters.SelectedItems.Count > 0)
                {
                    // Add the Collections to the list from the main Definery object
                    AddToCollectionCombo.ItemsSource = Definery.MyCollections;
                    AddToCollectionCombo.SelectedIndex = 0;

                    // Show the Add To Collection form
                    OverlayGrid.Visibility = Visibility.Visible;
                    AddToCollectionGrid.Visibility = Visibility.Visible;
                }
                else
                {
                    MessageBox.Show("Nothing is selected. Selected a Shared Parameter to add to a Collection.");
                }
            }
        }

        /// <summary>
        /// Helper method to catch when a selection changes in the DataGrid.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DataGridParameters_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Show the Add to Collection button if something is selected.
            if (DataGridParameters.SelectedItems.Count > 0)
            {
                AddToCollectionButton.Visibility = Visibility.Visible;

                // Toggle UI based on the ParameterSource
                if (DataGridParameters.SelectedItems.Count > 0 && ParamSource == ParameterSource.Collection)
                {
                    RemoveFromCollectionButton.Visibility = Visibility.Visible;
                }
                if (ParamSource == ParameterSource.Search)
                {
                    RemoveFromCollectionButton.Visibility = Visibility.Collapsed;
                }
            }
            // Toggle sidebar UI
            if (DataGridParameters.SelectedItems.Count == 1)
            {
                var selectedParam = DataGridParameters.SelectedItem as SharedParameter;

                CloneParameterButton.Visibility = Visibility.Visible;
                PropertiesSideBar.Visibility = Visibility.Visible;

                // Update Properties
                PropTextGuid.Text = selectedParam.Guid.ToString();
                
                var paramDataType = DataType.GetFromName(Definery.DataTypes, selectedParam.DataType);
                PropComboDataType.SelectedItem = paramDataType;

                if (selectedParam.Visible == "1")
                {
                    PropCheckVisible.IsChecked = true;
                }
                else
                {
                    PropCheckVisible.IsChecked = false;
                }
                
                if (selectedParam.UserModifiable == "1")
                {
                    PropCheckUserMod.IsChecked = true;
                }
                else
                {
                    PropCheckUserMod.IsChecked = false;
                }
            }
            if (DataGridParameters.SelectedItems.Count > 1)
            {
                PropertiesSideBar.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Helper method to refresh all UI elements after a new payload.
        /// </summary>
        public void RefreshUi()
        {
            // Update the data grid
            DataGridParameters.ItemsSource = Definery.Parameters;
            DataGridParameters.Items.Refresh();

            if (DataGridParameters.Items.Count > 0)
            {
                DataGridParameters.ScrollIntoView(DataGridParameters.Items[0]);
            }

            // Toggle UI based on DataGrid selection
            if (DataGridParameters.SelectedItems.Count == 0)
            {
                RemoveFromCollectionButton.Visibility = Visibility.Collapsed;
            }
            if (DataGridParameters.Items.Count == 1)
            {
                PropertiesSideBar.Visibility = Visibility.Visible;
            }
            if (DataGridParameters.Items.Count > 1)
            {
                PropertiesSideBar.Visibility = Visibility.Collapsed;
            }

            // Manage contextual UI
            if (ParamSource == ParameterSource.Search)
            {
                RemoveFromCollectionButton.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Method to execute when the Save button is clicked on the Add to Collection form.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddToCollectionFormButton_Click(object sender, RoutedEventArgs e)
        {
            // Get the selected Collection as a Collection object
            SelectedCollection = AddToCollectionCombo.SelectedItem as Collection;

            if (ParamSource == ParameterSource.Search | ParamSource == ParameterSource.Collection)
            {
                foreach (var p in DataGridParameters.SelectedItems)
                {
                    // Get current Shared Parameter as a SharedParameter object
                    var selectedParam = p as SharedParameter;

                    // Add the Shared Parameter to the Collection
                    SharedParameter.AddCollection(Definery, selectedParam, SelectedCollection.Id);
                }

                // Notify the user of the update
                MessageBox.Show("Added " + DataGridParameters.SelectedItems.Count + " parameters to " + SelectedCollection.Name + ".");
            }
            else  // Logic to execute if the current source is the Orphaned list
            {
                foreach (var p in DataGridParameters.SelectedItems)
                {
                    // Get current Shared Parameter as a SharedParameter object
                    var selectedParam = p as SharedParameter;

                    var response = SharedParameter.AddCollection(Definery, selectedParam, SelectedCollection.Id);
                }

                // Load the data based on selected Collection and display in the DataGrid
                Definery.Parameters = SharedParameter.GetOrphaned(
                    Definery, Pager.ItemsPerPage, Pager.Offset, true
                    );

                // Notify the user of the update
                MessageBox.Show("Added " + DataGridParameters.SelectedItems.Count + " parameters to " + SelectedCollection.Name + ".");

                RefreshUi();
            }

            // Hide the overlay
            AddToCollectionGrid.Visibility = Visibility.Hidden;
            OverlayGrid.Visibility = Visibility.Hidden;

        }

        /// <summary>
        /// Method to execute when the Cancel button is click on the Add to Collection form.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CancelAddToCollectionButton_Click(object sender, RoutedEventArgs e)
        {
            // Hide the Add To Collection form
            OverlayGrid.Visibility = Visibility.Hidden;
            AddToCollectionGrid.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// Method to execute when the Add Parameter button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NewParameterButton_Click(object sender, RoutedEventArgs e)
        {
            // Reset the form
            InitializeParamForm();

            // Pass the Collections list to the combobox and configure
            NewParamFormCombo.ItemsSource = Definery.MyCollections;
            NewParamFormCombo.DisplayMemberPath = "Name";  // Displays the Collection name rather than object in the combobox
            NewParamFormCombo.SelectedIndex = 0;  // Always select the default item so it cannot be left blank

            // Generate a GUID by default
            NewParamGuidTextBox.Text = Guid.NewGuid().ToString();

            // Clear all values
            NewParamNameTextBox.Text = "";
            NewParamDescTextBox.Text = "";
            NewParamVisibleCheck.IsChecked = true;
            NewParamUserModCheckbox.IsChecked = true;

            // Show the Add Parameter form
            OverlayGrid.Visibility = Visibility.Visible;
            NewParameterGrid.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Method to execute when the New Parameter form Cancel button is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CancelNewParamButton_Click(object sender, RoutedEventArgs e)
        {
            // Hide the overlay and form
            NewParameterGrid.Visibility = Visibility.Hidden;
            OverlayGrid.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// Method to execute when the Add Parameter button on form is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NewParamFormButton_Click(object sender, RoutedEventArgs e)
        {
            // Instantiate the data from the form inputs
            var collection = NewParamFormCombo.SelectedItem as Collection;
            var response = string.Empty;
            var dataType = NewParamDataTypeCombo.SelectedItem as DataType;

            var param = new SharedParameter();
            param.Description = NewParamDescTextBox.Text;
            param.DataType = dataType.Name;
            param.Visible = (NewParamVisibleCheck.IsChecked ?? false) ? "1" : "0";  // Reports out a 1 or 0 as a string
            param.UserModifiable = (NewParamUserModCheckbox.IsChecked ?? false) ? "1" : "0";

            // Only create parameter if the form validates
            if (NewParamNameTextBox.Text.Length < 4)
            {
                MessageBox.Show("The parameter name must be longer than four characters.");
            }
            else
            {
                // Assign the name from the form
                param.Name = NewParamNameTextBox.Text;

                // Check that the description has a value
                if (NewParamDescTextBox.Text.Length < 1)
                {
                    MessageBox.Show("The parameter description is required.");
                }
                else
                {
                    // Try to convert the string from the from into a GUID
                    try
                    {
                        // Assign the GUID
                        var guid = new Guid(NewParamGuidTextBox.Text);
                        param.Guid = guid;

                        // Finially create the parameter
                        response = SharedParameter.Create(Definery, param, collection.Id);

                        // Hide the overlay and form
                        NewParameterGrid.Visibility = Visibility.Hidden;
                        OverlayGrid.Visibility = Visibility.Hidden;

                        MessageBox.Show("The parameter has been successfully created.");

                        Debug.Write(response);

                        // Reset the form values and UI
                        InitializeParamForm();

                    }
                    // Display a message if the text cannot be cast to a GUID
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Method to execute when the Add to Collection Cancel button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CancelAddToCollectionButton_Click_1(object sender, RoutedEventArgs e)
        {
            // Hide the overlay
            AddToCollectionGrid.Visibility = Visibility.Hidden;
            OverlayGrid.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// Method to execute when the New Collection button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NewCollectionButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear the combobox in case it was previously canceled
            NewCollectionFormTextBox.Text = string.Empty;

            // Show the overlay
            NewCollectionGrid.Visibility = Visibility.Visible;
            OverlayGrid.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Method to execute when the New Collection Cancel button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NewCollectionFormCancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Hide the overlay
            NewCollectionGrid.Visibility = Visibility.Hidden;
            OverlayGrid.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// Method to execute when the New Collection Save button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NewCollectionFormSaveButton_Click(object sender, RoutedEventArgs e)
        {
            var response = Collection.Create(Definery, NewCollectionFormTextBox.Text, NewCollectionFormDesc.Text);

            // If the Collection was successfully created, refresh the Collections list
            if (response.StatusCode == System.Net.HttpStatusCode.Created)
            {
                MessageBox.Show("The collection was successfully created.");

                Definery.MyCollections = Collection.ByCurrentUser(Definery);

                // Refresh the sidebar list UI
                CollectionsList.ItemsSource = Definery.MyCollections;
            }

            // Hide the overlay
            NewCollectionGrid.Visibility = Visibility.Hidden;
            OverlayGrid.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// Method to execute when the My Collections selection changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CollectionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshCollectionParameters(CollectionsList);

            // Deselect the other ListBoxes
            CollectionsList_Published.SelectedItem = null;
            OrphanedList.SelectedItem = null;

            // Set enum for UI purposes
            ParamSource = ParameterSource.Collection;
        }

        /// <summary>
        /// Method to execute when the Published Collections selection changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CollectionsList_Published_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshCollectionParameters(CollectionsList_Published);

            // Deselect the other ListBoxes
            CollectionsList.SelectedItem = null;
            OrphanedList.SelectedItem = null;

            // Set enum for UI purposes
            ParamSource = ParameterSource.Collection;
        }

        /// <summary>
        /// Method to execute when the Orphaned item is selected.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OrphanedListBox_Selected(object sender, RoutedEventArgs e)
        {
            // Deselect the other ListBoxes
            CollectionsList_Published.SelectedItem = null;
            CollectionsList.SelectedItem = null;

            // Hide contextual UI since no parameters will be selected
            AddToCollectionButton.Visibility = Visibility.Collapsed;
            RemoveFromCollectionButton.Visibility = Visibility.Collapsed;
            CloneParameterButton.Visibility = Visibility.Collapsed;

            // Get the parameters
            Definery.Parameters = SharedParameter.GetOrphaned(Definery, Pager.ItemsPerPage, 0, true);

            // Force the pager to page 0 and update
            Pager.CurrentPage = 0;
            UpdatePager(Pager, 0);

            // Update the GUI anytime data is loaded
            PagerPanel.Visibility = Visibility.Visible;

            RefreshUi();

            // Hide the export button because this is not a Collection
            ExportCollectionButton.Visibility = Visibility.Collapsed;

            // Hide the add to collection button because nothing will be selected
            AddToCollectionButton.Visibility = Visibility.Hidden;

            // Set enum for UI purposes
            ParamSource = ParameterSource.Orphaned;
        }

        /// <summary>
        /// Method to execute when the Browse button is clicked on the Batch Upload form.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BrowseForTxtButton_Click(object sender, RoutedEventArgs e)
        {
            // Instantiate a file dialog
            OpenFileDialog openFileDialog = new OpenFileDialog();

            // Set the filter to only show text files
            openFileDialog.Filter = "Shared Parameter Text Files (*.txt)|*.txt";

            // Set the textbox path if a file was chosen
            if (openFileDialog.ShowDialog() == true)
                TxtBoxSpPath.Text = openFileDialog.FileName;
        }

        /// <summary>
        /// Method to execute when the Export Txt button on the Export Txt form is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ExportCollectionTxtButton_Click(object sender, RoutedEventArgs e)
        {
            // Reset pagination
            Pager.Offset = 0;
            Pager.CurrentPage = 0;
            Pager.IsFirstPage = true;
            Pager.IsLastPage = false;

            // Get all of the SharedParameters from the Collection
            var allParams = SharedParameter.GetAllFromCollection(Definery, SelectedCollection);

            // Generate the text file
            Exporter.ToRevitTxt(Definery, allParams.ToList());

            // Refresh the data
            Pager.CurrentPage = 0;
            Pager.Offset = 0;

            // Get the parameters of the logged in user by default and display in the DataGrid
            // Get the parameters of the logged in user by default and display in the DataGrid
            Definery.Parameters = SharedParameter.ByCollection(
                Definery, SelectedCollection, Pager.ItemsPerPage, Pager.Offset, true
                );

            // Update the GUI anytime data is loaded
            UpdatePager(Pager, 0);
            RefreshUi();
        }

        /// <summary>
        /// Helper method to update the DataTable based on ListBox selections.
        /// </summary>
        /// <param name="listBox"></param>
        private void RefreshCollectionParameters(ListBox listBox)
        {
            if (listBox.SelectedItems.Count > 0)
            {
                // Hide contextual UI since no parameters will be selected
                AddToCollectionButton.Visibility = Visibility.Collapsed;
                RemoveFromCollectionButton.Visibility = Visibility.Collapsed;
                CloneParameterButton.Visibility = Visibility.Collapsed;

                // Instantiate the selected item as a Collection object and assign it to the MainWindow for future reference
                SelectedCollection = listBox.SelectedItem as Collection;

                // Get the parameters
                Definery.Parameters = SharedParameter.ByCollection(Definery, SelectedCollection, Pager.ItemsPerPage, 0, true
                    );

                // Force the pager to page 0 and update
                Pager.CurrentPage = 0;
                UpdatePager(Pager, 0);

                // Update the GUI anytime data is loaded
                PagerPanel.Visibility = Visibility.Visible;
                RefreshUi();

                // Logic to execute if there are parameters within the collection
                if (Definery.Parameters.Count() > 0)
                {
                    // Show the export button
                    ExportCollectionButton.Visibility = Visibility.Visible;
                }
                else
                {
                    ExportCollectionButton.Visibility = Visibility.Collapsed;
                }
            }

            // Hide the add to collection button because nothing will be selected
            AddToCollectionButton.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// Method to execute when Clone Parameter Button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CloneParameterButton_Click(object sender, RoutedEventArgs e)
        {
            // Only execute if a single paramter is selected
            if (DataGridParameters.SelectedItems.Count == 1)
            {
                // Toggle the UI
                OverlayGrid.Visibility = Visibility.Visible;
                NewParameterGrid.Visibility = Visibility.Visible;

                // Add Collections to ComboBox
                NewParamFormCombo.ItemsSource = Definery.MyCollections;
                NewParamFormCombo.DisplayMemberPath = "Name";
                NewParamFormCombo.SelectedIndex = 0;

                // Disable editing of certain fields otherwise you are technically not cloning
                NewParamGuidTextBox.IsEnabled = false;
                NewParamDataTypeCombo.IsEnabled = false;
                NewParamVisibleCheck.IsEnabled = false;
                NewParamUserModCheckbox.IsEnabled = false;

                var selectedParam = DataGridParameters.SelectedItem as SharedParameter;

                // Prepopulate the fields based on the selected parameter
                NewParamNameTextBox.Text = selectedParam.Name;
                NewParamGuidTextBox.Text = selectedParam.Guid.ToString();
                NewParamDescTextBox.Text = selectedParam.Description;
                var paramDataType = DataType.GetFromName(Definery.DataTypes, selectedParam.DataType);
                NewParamDataTypeCombo.SelectedItem = paramDataType;

                if (selectedParam.Visible == "1")
                {
                    NewParamVisibleCheck.IsChecked = true;
                }
                else
                {
                    NewParamVisibleCheck.IsChecked = false;
                }

                if (selectedParam.UserModifiable == "1")
                {
                    NewParamUserModCheckbox.IsChecked = true;
                }
                else
                {
                    NewParamUserModCheckbox.IsChecked = false;
                }

            }
            if (DataGridParameters.SelectedItems.Count > 1)
            {
                MessageBox.Show("You may only clone one Shared Parameter at a time... For now.");
            }
        }

        /// <summary>
        /// Helper method to clear the New Parameter form.
        /// </summary>
        private void InitializeParamForm()
        {
            // Enable editing of certain fields just in case this method was triggered from cloning
            NewParamGuidTextBox.IsEnabled = true;
            NewParamDataTypeCombo.IsEnabled = true;
            NewParamVisibleCheck.IsEnabled = true;
            NewParamUserModCheckbox.IsEnabled = true;
        }

        /// <summary>
        /// Helper method to handle text links opening in an external browser.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HandleLinkClick(object sender, RoutedEventArgs e)
        {
            Hyperlink hl = (Hyperlink)sender;
            string navigateUri = hl.NavigateUri.ToString();
            Process.Start(new ProcessStartInfo(navigateUri));
            e.Handled = true;
        }

        /// <summary>
        /// Method to execute when Remove From Collection button is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveFromCollectionButton_Click(object sender, RoutedEventArgs e)
        {
            // Instantiate a list of SharedParameters
            var sharedParameters = new List<SharedParameter>();

            foreach (var i in DataGridParameters.SelectedItems)
            {
                var param = i as SharedParameter;

                sharedParameters.Add(param);
            }

            // Process selected items
            foreach (var param in sharedParameters)
            {
                var response = SharedParameter.RemoveCollection(Definery, param, SelectedCollection.Id);

                if (response)
                {
                    // Instantiate a new Pager
                    // TODO: Make it so that the user stays on the same page after deleting a SharedParameter
                    // from the Collection
                    Pager.CurrentPage = 0;

                    // Update the Pager UI
                    UpdatePager(Pager, 0);

                    // Load the data based on selected Collection and display in the DataGrid
                    Definery.Parameters = SharedParameter.ByCollection(
                        Definery, SelectedCollection, Pager.ItemsPerPage, Pager.Offset, true
                        );

                    // Toggle UI
                    CloneParameterButton.Visibility = Visibility.Collapsed;
                    AddToCollectionButton.Visibility = Visibility.Collapsed;

                    RefreshUi();
                }
            }
        }

        /// <summary>
        /// Method to execute when Search Button is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            // Hide contextual UI since no parameters will be selected
            AddToCollectionButton.Visibility = Visibility.Collapsed;
            RemoveFromCollectionButton.Visibility = Visibility.Collapsed;
            CloneParameterButton.Visibility = Visibility.Collapsed;

            // Get the parameters
            Definery.Parameters = SharedParameter.Search(Definery, SearchTxtBox.Text, Pager.ItemsPerPage, Pager.Offset, true);

            // Force the pager to page 0 and update
            Pager.CurrentPage = 0;
            UpdatePager(Pager, 0);

            // Set enum for UI purposes
            ParamSource = ParameterSource.Search;

            // Update the GUI anytime data is loaded
            PagerPanel.Visibility = Visibility.Visible;
            ExportCollectionButton.Visibility = Visibility.Collapsed;
            RefreshUi();

        }

        /// <summary>
        /// Helper method to catch when a user presses enter to search.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PressEnterToSearch(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                SearchButton_Click(sender, e);
            }
        }

        /// <summary>
        /// Identifies the source of the current list of SharedParameters
        /// </summary>
        enum ParameterSource
        {
            None,
            Collection,
            Search,
            Orphaned
        }
    }
}