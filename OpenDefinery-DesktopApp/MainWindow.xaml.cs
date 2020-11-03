using OpenDefinery;
using RestSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
        bool AllParamsLoaded { get; set; }

        public MainWindow()
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight;
            MaxWidth = SystemParameters.MaximizedPrimaryScreenWidth;

            InitializeComponent();

            // Instantiate a new objects
            Definery = new Definery();
            Pager = new Pager();

            // Set current pagination fields
            Pager.CurrentPage = 0;
            Pager.ItemsPerPage = 100;
            Pager.Offset = 0;

            // Set up UI elements at launch of app
            AddToCollectionGrid.Visibility = Visibility.Hidden;  // The Add to Collection form
            NewParameterGrid.Visibility = Visibility.Hidden;  // The New Parameter form
            NewCollectionGrid.Visibility = Visibility.Hidden;  // The New Collection form
            BatchUploadGrid.Visibility = Visibility.Hidden;  // The Batch Upload form
            AddToCollectionButton.Visibility = Visibility.Collapsed;  // Add to Collection button
            RemoveFromCollectionButton.Visibility = Visibility.Collapsed;  // Remove from Collection button
            ExportCollectionButton.Visibility = Visibility.Collapsed;  // Export TXT button
            ForkParameterButton.Visibility = Visibility.Collapsed;  // Fork Parameter button
            DeleteCollectionButton.Visibility = Visibility.Collapsed;  // The Collection delete button
            ProgressGrid.Visibility = Visibility.Hidden;  // Main Progress Bar

            PagerPanel.Visibility = Visibility.Hidden;  // Pager
            PagerNextButton.IsEnabled = false;  // Pager
            PagerPreviousButton.IsEnabled = false;  // Pager

            AllParamsLoaded = false;

            PropertiesSideBar.Visibility = Visibility.Collapsed; // Sidebar should be collapsed by default

            MainBrowserGrid.Visibility = Visibility.Hidden; // Hide the main UI until logged in

            ParamSource = ParameterSource.None;  // Make the ParameterSource none until there is some action

            // Show the login modal
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
                Definery.DataCategories = DataCategory.GetAll(Definery);

                // Clean up Data Category names
                foreach (var cat in Definery.DataCategories)
                {
                    var splitName = cat.Name.Split('_');
                    cat.Name = splitName[1];
                }

                // Sort the lists for future use by UI
                Definery.DataTypes.Sort(delegate (DataType x, DataType y)
                {
                    if (x.Name == null && y.Name == null) return 0;
                    else if (x.Name == null) return -1;
                    else if (y.Name == null) return 1;
                    else return x.Name.CompareTo(y.Name);
                });

                Definery.DataCategories.Sort(delegate (DataCategory x, DataCategory y)
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
                NewParamDataCatCombo.ItemsSource = Definery.DataCategories;
                NewParamDataCatCombo.DisplayMemberPath = "Name";
                NewParamDataCatCombo.SelectedItem = null;
                PropComboDataType.ItemsSource = Definery.DataTypes;
                PropComboDataType.DisplayMemberPath = "Name";  // Displays the name rather than object in the combobox
                PropComboDataCategory.ItemsSource = Definery.DataCategories;
                PropComboDataCategory.DisplayMemberPath = "Name";

                // Display Collections in listboxes
                Definery.MyCollections = Collection.ByCurrentUser(Definery);
                CollectionsList.DisplayMemberPath = "Name";
                CollectionsList.ItemsSource = Definery.MyCollections;

                Definery.PublishedCollections = Collection.GetPublished(Definery);
                CollectionsList_Published.DisplayMemberPath = "Name";
                CollectionsList_Published.ItemsSource = Definery.PublishedCollections;

                // Set all Collections
                Definery.AllCollections = new List<Collection>();
                Definery.AllCollections.AddRange(Definery.MyCollections);
                Definery.AllCollections.AddRange(Definery.PublishedCollections);

                // Update the main Pager object
                Pager.CurrentPage = 0;
                UpdatePager(Pager, 0);

                // If there are more than one page, enable the Load All button
                if (Pager.TotalPages == 1)
                {
                    AllParamsLoaded = true;
                }
                else
                {
                    AllParamsLoaded = false;
                }

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
        /// <param name="loadingAll">Specify if the update is executed by the Load All method</param>
        public void UpdatePager(Pager pager, int incrementChange, bool loadingAll = false)
        {
            this.Dispatcher.Invoke(() =>
            {
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

                    PagerNextButton.IsEnabled = true;
                    PagerPreviousButton.IsEnabled = false;
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
                    PagerNextButton.IsEnabled = true;
                }
                if (pager.CurrentPage == pager.TotalPages - 1)
                {
                    PagerNextButton.IsEnabled = false;
                    pager.IsLastPage = true;
                }

                if (pager.CurrentPage <= pager.TotalPages - 1 && pager.CurrentPage >= 0)
                {
                    PagerPreviousButton.IsEnabled = true;
                }
                if (pager.CurrentPage < 1)
                {
                    PagerPreviousButton.IsEnabled = false;
                    pager.IsFirstPage = true;
                }

                // Disable the pager buttons if there is only one page
                if (pager.CurrentPage == 0 & pager.TotalPages == 1)
                {
                    PagerNextButton.IsEnabled = false;
                    PagerPreviousButton.IsEnabled = false;

                    // First and last page are both true when there is only one page
                    pager.IsFirstPage = true;
                    pager.IsLastPage = true;
                }

                // Update the textbox
                if (!loadingAll)
                {
                    PagerTextBox.Text = string.Format("Page {0} of {1}    |    Total Parameters: {2}", pager.CurrentPage + 1, pager.TotalPages, pager.TotalItems);
                }
                else
                {
                    PagerTextBox.Text = string.Format("Loading page {0} of {1}    |    Total Parameters: {2}", pager.CurrentPage + 1, pager.TotalPages, pager.TotalItems);
                }

                // Set the object from the updated Pager object
                Pager = pager;
            });
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
                                var newParameter = SharedParameter.FromTxt(Definery, line);

                                if (newParameter != null)
                                {
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
                                        SharedParameter.Create(Definery, newParameter, sollection.Id);
                                        });

                                        Application.Current.Dispatcher.BeginInvoke(
                                          DispatcherPriority.Background,
                                          new Action(() =>
                                          {
                                              ProgressStatus.Text += "Done.";
                                          }));
                                    }
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

                // Load all of the things!!!
                LoadData();

                // Hide login form
                OverlayGrid.Visibility = Visibility.Hidden;
                LoginGrid.Visibility = Visibility.Hidden;
            }
            else
            {
                MessageBox.Show("There was an error logging in. Please try again.");
            }
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
        /// Note that this method should not call the RefreshUI method as it requires one-off logic.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DataGridParameters_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridParameters.SelectedItems.Count == 0)
            {
                AddToCollectionButton.Visibility = Visibility.Collapsed;
                RemoveFromCollectionButton.Visibility = Visibility.Collapsed;
                ForkParameterButton.Visibility = Visibility.Collapsed;
            }
            if (DataGridParameters.SelectedItems.Count == 1)
            {
                var selectedParam = DataGridParameters.SelectedItem as SharedParameter;

                if (ParamSource == ParameterSource.Collection)
                {
                    // Toggle UI based on permissions
                    if (selectedParam.Author == Definery.CurrentUser.Id)
                    {
                        AddToCollectionButton.Visibility = Visibility.Visible;
                        RemoveFromCollectionButton.Visibility = Visibility.Visible;
                        ForkParameterButton.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        AddToCollectionButton.Visibility = Visibility.Collapsed;
                        RemoveFromCollectionButton.Visibility = Visibility.Collapsed;
                        ForkParameterButton.Visibility = Visibility.Visible;
                    }
                }
                if (ParamSource == ParameterSource.Orphaned)
                {
                    AddToCollectionButton.Visibility = Visibility.Visible;
                    ForkParameterButton.Visibility = Visibility.Visible;
                }
                if (ParamSource == ParameterSource.Search)
                {
                    ForkParameterButton.Visibility = Visibility.Visible;

                    // Only allow the user to add the Parameter to a Collection if they are the author
                    if (selectedParam.Author == Definery.CurrentUser.Id)
                    {
                        AddToCollectionButton.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        AddToCollectionButton.Visibility = Visibility.Collapsed;
                    }
                }

                // Toggle UI
                PropertiesSideBar.Visibility = Visibility.Visible;
                PropTxtRowCopied.Visibility = Visibility.Collapsed;  

                // Update GUID field
                PropTextGuid.Text = selectedParam.Guid.ToString();

                // Update Data type field and select the item in the ComboBox
                var paramDataType = DataType.GetFromName(Definery.DataTypes, selectedParam.DataType);
                PropComboDataType.SelectedItem = paramDataType;

                // Update Data Category field and select the item in the ComboBox
                if (!string.IsNullOrEmpty(selectedParam.DataCategoryHashcode))
                {
                    // Toggle UI
                    PropComboLabelDataCategory.Visibility = Visibility.Visible;
                    PropComboDataCategory.Visibility = Visibility.Visible;

                    // A rather roundabout way to displaying the current DataCategory in the combobox
                    var paramDataCat = DataCategory.GetByHashcode(Definery, selectedParam.DataCategoryHashcode);
                    var foundDataCats = Definery.DataCategories.Where(o => o.Hashcode == paramDataCat.Hashcode);
                    var dataCategory = foundDataCats.FirstOrDefault();

                    PropComboDataCategory.SelectedItem = dataCategory;
                }
                else
                {
                    PropComboLabelDataCategory.Visibility = Visibility.Collapsed;
                    PropComboDataCategory.Visibility = Visibility.Collapsed;
                }

                // Update boolean fields
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

                // Update copy/paste textbox
                PropTxtRow.Text = "PARAM\t" +
                    selectedParam.Guid.ToString() + "\t" +
                    selectedParam.Name + "\t" +
                    selectedParam.DataType + "\t" +
                    selectedParam.DataCategoryHashcode + "\t" +
                    "1" + "\t" +  // Hardcode the Default Group until Groups are properly implemented
                    selectedParam.Visible + "\t" +
                    selectedParam.Description + "\t" +
                    selectedParam.UserModifiable;
            }
            if (DataGridParameters.SelectedItems.Count > 1)
            {
                PropertiesSideBar.Visibility = Visibility.Collapsed;
                ForkParameterButton.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Helper method to refresh all UI elements after a new payload. This should execute anytime there is a change to the UI.
        /// </summary>
        /// <param name="loadingAll">Specify is the the refresh is called by the Load All method</param>
        public void RefreshUi(bool loadingAll = false)
        {
            // Manage the main UI first to handle the datagrid and home screen
            this.Dispatcher.Invoke(() =>
            {
                if (ParamSource != ParameterSource.None)
                {
                    MainBrowserGrid.Visibility = Visibility.Visible;
                    DashboardGrid.Visibility = Visibility.Collapsed;

                    DataGridParameters.ItemsSource = Definery.Parameters;
                    DataGridParameters.Items.Refresh();
                }
                if (ParamSource == ParameterSource.None)
                {
                    MainBrowserGrid.Visibility = Visibility.Collapsed;
                    DashboardGrid.Visibility = Visibility.Visible;

                    PagerPanel.Visibility = Visibility.Hidden;
                }
                if (SelectedCollection == null)
                {
                    ExportCollectionButton.Visibility = Visibility.Collapsed;
                    DeleteCollectionButton.Visibility = Visibility.Collapsed;
                }
            });

            // Handle the rest of the UI elements
            this.Dispatcher.Invoke(() =>
            {
                // Toggle UI based on Parameter results
                if (Definery.Parameters != null && Definery.Parameters.Count() > 0 & ParamSource == ParameterSource.Collection)
                {
                    // Show the export button
                    ExportCollectionButton.Visibility = Visibility.Visible;
                }
                if (Definery.Parameters != null && Definery.Parameters.Count() < 1)
                {
                    ExportCollectionButton.Visibility = Visibility.Collapsed;
                }

                // Toggle UI based on the Load All button
                if (DataGridParameters.Items.Count > 0 & !loadingAll)
                {
                    DataGridParameters.ScrollIntoView(DataGridParameters.Items[0]);
                }
                if (loadingAll)
                {
                    PagerNextButton.Visibility = Visibility.Collapsed;
                    PagerPreviousButton.Visibility = Visibility.Collapsed;
                    PagerLoadAllButton.IsEnabled = false;
                }

                // Toggle UI based on DataGrid selection
                if (DataGridParameters.SelectedItems.Count == 0)
                {
                    RemoveFromCollectionButton.Visibility = Visibility.Collapsed;
                    AddToCollectionButton.Visibility = Visibility.Collapsed;
                    ForkParameterButton.Visibility = Visibility.Collapsed;
                    PropertiesSideBar.Visibility = Visibility.Collapsed;
                }
                if (DataGridParameters.SelectedItems.Count == 1)
                {
                    ForkParameterButton.Visibility = Visibility.Collapsed;
                    PropertiesSideBar.Visibility = Visibility.Visible;
                }
                if (DataGridParameters.SelectedItems.Count > 1)
                {
                    PropertiesSideBar.Visibility = Visibility.Collapsed;
                }

                if (ParamSource == ParameterSource.Search)
                {
                    CollectionsColumn.Visibility = Visibility.Visible;
                    RemoveFromCollectionButton.Visibility = Visibility.Collapsed;
                    PagerLoadAllButton.Visibility = Visibility.Collapsed;
                    PagerPanel.Visibility = Visibility.Visible;
                    ExportCollectionButton.Visibility = Visibility.Collapsed;
                    DeleteCollectionButton.Visibility = Visibility.Collapsed;
                }
                if (ParamSource == ParameterSource.Collection)
                {
                    CollectionsColumn.Visibility = Visibility.Collapsed;
                    PagerLoadAllButton.Visibility = Visibility.Visible;

                    // Show UI if current user is the Collection author
                    if (SelectedCollection.Author == Definery.CurrentUser.Id)
                    {
                        DeleteCollectionButton.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        DeleteCollectionButton.Visibility = Visibility.Collapsed;
                    }
                }
                if (ParamSource == ParameterSource.Orphaned)
                {
                    CollectionsColumn.Visibility = Visibility.Collapsed;
                    PagerLoadAllButton.Visibility = Visibility.Collapsed;
                    ExportCollectionButton.Visibility = Visibility.Collapsed;
                    RemoveFromCollectionButton.Visibility = Visibility.Collapsed;
                    DeleteCollectionButton.Visibility = Visibility.Collapsed;
                }

                // Toggle the Pager visibility
                if (Pager.TotalPages == 1)
                {
                    AllParamsLoaded = true;
                }
                if (Pager.TotalPages > 1 && !loadingAll)
                {
                    PagerPanel.Visibility = Visibility.Visible;

                    AllParamsLoaded = false;
                }
                if (Pager.TotalItems > 1 && loadingAll)
                {
                    PagerPanel.Visibility = Visibility.Visible;
                    PagerPreviousButton.Visibility = Visibility.Collapsed;
                    PagerNextButton.Visibility = Visibility.Collapsed;
                    PagerLoadAllButton.Visibility = Visibility.Collapsed;
                    
                    AllParamsLoaded = true;
                }
                if (Pager.TotalItems < 2)
                {
                    PagerLoadAllButton.Visibility = Visibility.Collapsed;

                    AllParamsLoaded = true;
                }

                // Enable UI for Parameter lists that have more than one page
                if (AllParamsLoaded == false)
                {
                    PagerNextButton.Visibility = Visibility.Visible;
                    PagerPreviousButton.Visibility = Visibility.Visible;

                    // Hide the Load All button for search results
                    if (ParamSource == ParameterSource.Search | ParamSource == ParameterSource.Orphaned)
                    {
                        PagerLoadAllButton.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    PagerNextButton.Visibility = Visibility.Collapsed;
                    PagerPreviousButton.Visibility = Visibility.Collapsed;
                    PagerLoadAllButton.Visibility = Visibility.Collapsed;
                }

                // Toggle loading all
                if (loadingAll)
                {
                }
            });
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

                    // Don't allow adding to a Collection if the user isn't the author because Drupal permissions won't allow it
                    if (selectedParam.Author != Definery.CurrentUser.Id)
                    {
                        // Do nothing because the UI should force the user to Fork the Parameter instead
                    }
                    else
                    {
                        // Add the Shared Parameter to the Collection
                        SharedParameter.AddCollection(Definery, selectedParam, SelectedCollection.Id);
                    }
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
            NewParamDataTypeCombo.SelectedIndex = 0;
            NewParamDataCatCombo.SelectedItem = null;

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
            // Set the DataCategory if selected
            var dataCategory = new DataCategory();
            if (NewParamDataCatCombo.SelectedItem != null)
            {
                dataCategory = NewParamDataCatCombo.SelectedItem as DataCategory;
            }

            // TODO: Refactor this logic using the new constructor 
            var param = new SharedParameter();
            param.Description = NewParamDescTextBox.Text;
            param.DataType = dataType.Name;
            param.DataCategoryHashcode = dataCategory.Hashcode;
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
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                    }

                    // Finally create the parameter
                    try
                    {
                        // Pass the ID of the Parameter that is being forked if provided
                        if (!string.IsNullOrEmpty(ForkedParamIdTextBox.Text))
                        {
                            SharedParameter.Create(Definery, param, collection.Id, Convert.ToInt32(ForkedParamIdTextBox.Text));
                        }
                        else
                        {
                            SharedParameter.Create(Definery, param, collection.Id);
                        }

                        // Hide the overlay and form
                        NewParameterGrid.Visibility = Visibility.Hidden;
                        OverlayGrid.Visibility = Visibility.Hidden;

                        // TODO: Validate that the Shared Parameter was actually created
                        MessageBox.Show("The parameter has been successfully created.");

                        // Reset the form values and UI
                        InitializeParamForm();
                    }
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
            // Clear the form
            NewCollectionFormTextBox.Text = string.Empty;
            NewCollectionFormDesc.Text = string.Empty;

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
            var newCollection = Collection.Create(Definery, NewCollectionFormTextBox.Text, NewCollectionFormDesc.Text);

            // If the Collection was successfully created, refresh the Collections list
            if (newCollection != null)
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
            // Set enum for UI purposes
            if (CollectionsList.SelectedItem != null)
            {
                ParamSource = ParameterSource.Collection;

                // Retrieve a page of Parameters from the selected Collection
                RefreshCollectionParameters(CollectionsList);

                // Force the pager to page 0 and update
                Pager.CurrentPage = 0;
                UpdatePager(Pager, 0);

                // Deselect the other ListBoxes
                CollectionsList_Published.SelectedItem = null;
                OrphanedList.SelectedItem = null;

                RefreshUi();
            }
            else
            {
                // Do nothing
            }
        }

        /// <summary>
        /// Method to execute when the Published Collections selection changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CollectionsList_Published_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Set enum for UI purposes
            if (CollectionsList_Published.SelectedItem != null)
            {
                ParamSource = ParameterSource.Collection;

                RefreshCollectionParameters(CollectionsList_Published);

                // Force the pager to page 0 and update
                Pager.CurrentPage = 0;
                UpdatePager(Pager, 0);

                // Deselect the other ListBoxes
                CollectionsList.SelectedItem = null;
                OrphanedList.SelectedItem = null;

                RefreshUi();
            }
            else
            {
                // Do nothing
            }
        }

        /// <summary>
        /// Method to execute when the Orphaned item is selected.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OrphanedListBox_Selected(object sender, RoutedEventArgs e)
        {
            if (OrphanedListItem.IsSelected == true)
            {
                // Deselect the other ListBoxes
                CollectionsList_Published.SelectedItem = null;
                CollectionsList.SelectedItem = null;

                // Get the parameters
                Definery.Parameters = SharedParameter.GetOrphaned(Definery, Pager.ItemsPerPage, 0, true);

                // Force the pager to page 0 and update
                Pager.CurrentPage = 0;
                UpdatePager(Pager, 0);

                // Update the GUI anytime data is loaded
                //PagerPanel.Visibility = Visibility.Visible;

                // Set enum for UI purposes
                ParamSource = ParameterSource.Orphaned;

                RefreshUi();
            }
            else
            {
                // Do nothing
            }
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

            // Get the first page of SharedParameters
            var allParams = SharedParameter.ByCollection(Definery, SelectedCollection, MainWindow.Pager.ItemsPerPage, MainWindow.Pager.Offset, true).ToList();

            // Update the pager since it is not the last page
            UpdatePager(Pager, 1);

            // Loop through all of the pages
            do
            {
                // Get all SharedParameters of the current page
                allParams.AddRange(SharedParameter.ByCollection(
                    Definery, SelectedCollection, Pager.ItemsPerPage, Pager.Offset, false));

                // Update the pager since it is not the last page
                UpdatePager(Pager, 1);

            } while (Pager.CurrentPage < Pager.TotalPages);

            Debug.WriteLine(string.Format("Found {0} parameters to export from {1}.", allParams.Count().ToString(), SelectedCollection.Name));

            // Generate the text file
            Exporter.ToRevitTxt(Definery, allParams.ToList());

            // Refresh the data
            Pager.CurrentPage = 0;
            Pager.Offset = 0;

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
            if (listBox.SelectedItems.Count == 1)
            {
                // Instantiate the selected item as a Collection object and assign it to the MainWindow for future reference
                SelectedCollection = listBox.SelectedItem as Collection;

                // Get the parameters
                Definery.Parameters = SharedParameter.ByCollection(Definery, SelectedCollection, Pager.ItemsPerPage, 0, true
                    );

                // Force the pager to page 0 and update
                Pager.CurrentPage = 0;
                UpdatePager(Pager, 0);

                RefreshUi();
            }
        }

        /// <summary>
        /// Method to execute when Fork Parameter Button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ForkParameterButton_Click(object sender, RoutedEventArgs e)
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

                // Disable editing of certain fields otherwise you are technically not forking
                NewParamGuidTextBox.IsEnabled = false;
                NewParamDataTypeCombo.IsEnabled = false;
                NewParamVisibleCheck.IsEnabled = false;
                NewParamUserModCheckbox.IsEnabled = false;

                var selectedParam = DataGridParameters.SelectedItem as SharedParameter;

                // Set ID to track which parameter it was forked from
                // This is the Drupal node ID, not the GUID
                ForkedParamIdTextBox.Text = selectedParam.Id.ToString();

                // Prepopulate the fields based on the selected parameter
                NewParamNameTextBox.Text = selectedParam.Name;
                NewParamGuidTextBox.Text = selectedParam.Guid.ToString();
                NewParamDescTextBox.Text = selectedParam.Description;
                var paramDataType = DataType.GetFromName(Definery.DataTypes, selectedParam.DataType);
                NewParamDataTypeCombo.SelectedItem = paramDataType;

                // Set the DataCategory if the DataType is a FamilyType
                if (paramDataType.Name == "FAMILYTYPE")
                {
                    var existingDataCat = DataCategory.GetByHashcode(Definery, selectedParam.DataCategoryHashcode);
                    NewParamDataCatCombo.SelectedItem = existingDataCat;
                    NewParamDataCatCombo.Visibility = Visibility.Visible;
                    NewParamDataCatCombo.IsEnabled = false;
                }


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
                MessageBox.Show("You may only fork one Shared Parameter at a time... For now.");
            }
        }

        /// <summary>
        /// Helper method to clear the New Parameter form.
        /// </summary>
        private void InitializeParamForm()
        {
            // Enable editing of certain fields just in case this method was triggered from forking
            NewParamGuidTextBox.IsEnabled = true;
            NewParamDataTypeCombo.IsEnabled = true;
            NewParamVisibleCheck.IsEnabled = true;
            NewParamUserModCheckbox.IsEnabled = true;
            ForkedParamIdTextBox.Text = string.Empty;
            NewParamDataCatCombo.SelectedItem = null;
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
                    ForkParameterButton.Visibility = Visibility.Collapsed;
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
            // Get the parameters
            Definery.Parameters = SharedParameter.Search(Definery, SearchTxtBox.Text, Pager.ItemsPerPage, Pager.Offset, true);

            // Force the pager to page 0 and update
            Pager.CurrentPage = 0;
            UpdatePager(Pager, 0);

            // Set enum for UI purposes
            ParamSource = ParameterSource.Search;

            // Refresh UI
            SelectedCollection = null;
            CollectionsList.SelectedItem = null;
            CollectionsList_Published.SelectedItem = null;
            OrphanedList.SelectedItem = null;

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

        /// <summary>
        /// Method to execute when copy/paste button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PropCopyPasteRowButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(PropTxtRow.Text);

            // Display confirmation message
            PropTxtRowCopied.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Helper method to allow mouse scrolling in the Collections side bar
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollViewer scv = (ScrollViewer)sender;
            scv.ScrollToVerticalOffset(scv.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        /// <summary>
        /// Method to execute when the Dashboard button is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            // Deselect Listboxes
            CollectionsList.SelectedItem = null;
            CollectionsList_Published.SelectedItem = null;
            OrphanedList.SelectedItem = null;

            // Reset pager and update Pager
            Pager = Pager.Reset();
            UpdatePager(Pager, 0);

            ParamSource = ParameterSource.None;

            RefreshUi();
        }

        private void TextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start("https://github.com/jmerlan/OpenDefinery-DesktopApp/issues");
        }

        /// <summary>
        /// Method to execute when the DataType Combo changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NewParamDataTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NewParamDataTypeCombo.SelectedItem != null)
            {
                var selectedDataType = NewParamDataTypeCombo.SelectedItem as DataType;

                if (selectedDataType.Name == "FAMILYTYPE")
                {
                    NewParamDataCatLabel.Visibility = Visibility.Visible;
                    NewParamDataCatCombo.Visibility = Visibility.Visible;
                    NewParamDataCatCombo.IsEnabled = true;
                    NewParamDataCatCombo.SelectedIndex = 0;
                }
                else
                {
                    NewParamDataCatLabel.Visibility = Visibility.Collapsed;
                    NewParamDataCatCombo.Visibility = Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// Method to execute when Load All button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void PagerLoadAllButton_Click(object sender, RoutedEventArgs e)
        {
            // Prevent clicking more than once and hide button
            PagerLoadAllButton.IsEnabled = false;
            PagerLoadAllButton.Visibility = Visibility.Collapsed;

            // Prevent clicking another Collection while loading is occuring
            CollectionsList.IsEnabled = false;
            CollectionsList_Published.IsEnabled = false;
            OrphanedList.IsEnabled = false;

            // Force the pager to page 0 and update
            Pager.CurrentPage = 0;
            UpdatePager(Pager, 0);

            // Instantiate a new list of Parameters
            var allParams = new ObservableCollection<SharedParameter>();

            if (ParamSource == ParameterSource.Collection)
            {
                await Task.Run(() =>
                {
                    // Loop through all of the pages
                    do
                      {
                        // Get all SharedParameters of the current page
                        var currentPage = SharedParameter.ByCollection(
                            Definery, SelectedCollection, Pager.ItemsPerPage, Pager.Offset, false);

                        this.Dispatcher.Invoke(() =>
                        {
                            // Add them to the main list
                            foreach (var p in currentPage)
                            {
                                allParams.Add(p);
                            }

                            PagerLoadAllButton.IsEnabled = false;
                            PagerNextButton.Visibility = Visibility.Collapsed;
                            PagerPreviousButton.Visibility = Visibility.Collapsed;
                        });

                        // Update the pager since it is not the last page
                        UpdatePager(Pager, 1, true);

                        // Set the current list of Parameters
                        Definery.Parameters = allParams;

                        // Refresh the UI
                        RefreshUi(true);

                  } while (Pager.CurrentPage < Pager.TotalPages);
              });

            AllParamsLoaded = true;

            // Refresh the UI
            RefreshUi(true);

            // Allow users to select another collection
            CollectionsList.IsEnabled = true;
            CollectionsList_Published.IsEnabled = true;
            OrphanedList.IsEnabled = true;

            // Toggle the Pager UI
            PagerTextBox.Text = string.Format("Loaded all {0} parameters", allParams.Count().ToString());
            }
            else
            {
                // Do nothing for now.
                MessageBox.Show("Can't load all during search.");
            }
        }

        /// <summary>
        /// Method to execute when Delete Collection button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteCollectionButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult messageBoxResult = 
                MessageBox.Show(
                    "Are you sure you want to delete the collection? This action cannot be undone.\n\n" +
                    "Note: Any Shared Parameters currently in this Collection will be orphaned unless they belong to another Collection.", 
                    "Delete Collection", 
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            // Delete the Collection if the use confirms
            if (messageBoxResult == MessageBoxResult.Yes)
            {
                Collection.Delete(Definery, SelectedCollection.Id);

                // Refresh the UI
                Definery.MyCollections = Collection.ByCurrentUser(Definery);
                CollectionsList.ItemsSource = Definery.MyCollections;
                SelectedCollection = null;
                ParamSource = ParameterSource.None;
                RefreshUi();
            }
            else
            {
                MessageBox.Show("Deletion canceled.");
            }
        }
    }
}