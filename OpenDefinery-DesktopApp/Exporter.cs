using Microsoft.Win32;
using OpenDefinery;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OpenDefinery_DesktopApp
{
    class Exporter
    {
        /// <summary>
        /// Convert a list of Shared Parameters to a shared parameter text file usable by Revit.
        /// </summary>
        /// <param name="definery">The main Definery object</param>
        /// <param name="parameters">The List of SharedParameters to convert</param>
        /// <returns>The full shared parameter text file contents</returns>
        public static string ToRevitTxt (Definery definery, List<SharedParameter> parameters)
        {
            // Instantiate parts of the txt file which are organized in tables separated by an asterisk per Revit standards

            // Create header text for introduction
            var header =
                "# This is a Revit shared parameter file.\n" +
                "# These parameters are part of an ongoing collaborative effort by OpenDefinery.\n" +
                "# If you would like to add, remove, or modify any parameters in this file, " +
                "please join us: http://opendefinery.com.\n";

            // Create the table for metadata
            var metaTable =
                "*META\tVERSION\tMINVERSION\n" +
                "META\t2\t1\n";

            // Create the table for groups
            // As of now, we are setting all parameters to a single Group
            var groupTable = 
                "*GROUP\tID\tNAME\n" +
                "GROUP\t1\tDefault Group\n";

            // Create the parameters
            var parameterTable = SharedParameter.CreateParamTable(parameters);

            // Output a concatenated string
            var output = header + metaTable + groupTable + parameterTable;
            //Debug.WriteLine(output);

            // Save the file
            Stream stream;
            SaveFileDialog saveDialog = new SaveFileDialog();

            saveDialog.Filter = "txt files (*.txt)|*.txt";
            saveDialog.RestoreDirectory = true;
            saveDialog.FileName = "SharedParameters_" + MainWindow.SelectedCollection.Name + ".txt";

            if (saveDialog.ShowDialog() == true)
            {
                if ((stream = saveDialog.OpenFile()) != null)
                {
                    // Write the entire output string to the selected txt file
                    using (StreamWriter streamWriter = new StreamWriter(stream))
                    {
                        streamWriter.Write(output);
                    }

                    MessageBox.Show(string.Format("The shared parameter txt file has been saved."));
                }
            }

            // Return the string to be used by other methods
            return output;
        }
    }
}
