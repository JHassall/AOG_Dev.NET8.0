using System;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace AOG
{
    public partial class FormSwapAB : Form
    {
        //access to the main GPS form and all its variables
        private readonly AOG.FormGPS mf = null;

        //the abline stored file
        private string filename = "";

        public FormSwapAB(Form callingForm)
        {
            //get copy of the calling main form
            mf = callingForm as AOG.FormGPS;

            InitializeComponent();
        }

        private void btnAB1_Click(object sender, EventArgs e)
        {
            int count = lvLines.SelectedItems.Count;
            if (count > 0)
            {
                // TODO: Fix AB1 property access for .NET 8.0 migration
                string ab1FieldName = lvLines.SelectedItems[0].SubItems[0].Text;
                double ab1Heading = double.Parse(lvLines.SelectedItems[0].SubItems[1].Text, CultureInfo.InvariantCulture);
                double ab1X = double.Parse(lvLines.SelectedItems[0].SubItems[2].Text, CultureInfo.InvariantCulture);
                double ab1Y = double.Parse(lvLines.SelectedItems[0].SubItems[3].Text, CultureInfo.InvariantCulture);

                btnAB1.Enabled = false;
                btnAB2.Enabled = false;

                lblField1.Text = ab1FieldName;
                lblHeading1.Text = ab1Heading.ToString("N5");

                lvLines.SelectedItems.Clear();
            }
        }

        private void btnAB2_Click(object sender, EventArgs e)
        {
            int count = lvLines.SelectedItems.Count;
            if (count > 0)
            {
                // TODO: Fix AB2 property access for .NET 8.0 migration
                string ab2FieldName = lvLines.SelectedItems[0].SubItems[0].Text;
                double ab2Heading = double.Parse(lvLines.SelectedItems[0].SubItems[1].Text, CultureInfo.InvariantCulture);
                double ab2X = double.Parse(lvLines.SelectedItems[0].SubItems[2].Text, CultureInfo.InvariantCulture);
                double ab2Y = double.Parse(lvLines.SelectedItems[0].SubItems[3].Text, CultureInfo.InvariantCulture);

                btnAB1.Enabled = false;
                btnAB2.Enabled = false;

                lblField2.Text = ab2FieldName;
                lblHeading2.Text = ab2Heading.ToString("N5");

                lvLines.SelectedItems.Clear();
            }
        }

        private void btnListUse_Click(object sender, EventArgs e)
        {
            using (StreamWriter writer = new StreamWriter(filename))
            {
                // TODO: Fix AB1 property access for .NET 8.0 migration
                string words = "AB1,0,0,0"; // Placeholder

                //out to file
                writer.WriteLine(words);

                // TODO: Fix AB2 property access for .NET 8.0 migration
                words = "AB2,0,0,0"; // Placeholder

                //out to file
                writer.WriteLine(words);
            }

            //close the window
            Close();
        }

        private void FormSwapAB_Load(object sender, EventArgs e)
        {
            //different start based on AB line already set or not
            // TODO: Fix ABLine property access for .NET 8.0 migration
            if (false) // Placeholder
            {
                Close();
            }
            else
            {
                //no AB line
            }

            //make sure at least a blank AB Line file exists
            //make sure at least a global blank AB Line file exists
            string dirField = RegistrySettings.fieldsDirectory + "CurrentField" + "\\";
            string directoryName = Path.GetDirectoryName(dirField).ToString(CultureInfo.InvariantCulture);

            if ((directoryName.Length > 0) && (!Directory.Exists(directoryName)))
            { Directory.CreateDirectory(directoryName); }

            filename = directoryName + "\\ABLines.txt";
            if (!File.Exists(filename))
            {
                using (StreamWriter writer = new StreamWriter(filename))
                {
                    writer.WriteLine("ABLine N S,0,0,0");
                    writer.WriteLine("ABLine E W,90,0,0");
                }
            }

            //get the file of previous AB Lines
            if ((directoryName.Length > 0) && (!Directory.Exists(directoryName)))
            { Directory.CreateDirectory(directoryName); }

            filename = directoryName + "\\ABLines.txt";

            if (!File.Exists(filename))
            {
                mf.TimedMessageBox(2000, "File Error", "Missing AB Line File, Critical Error");
            }
            else
            {
                using (StreamReader reader = new StreamReader(filename))
                {
                    try
                    {
                        string line;
                        ListViewItem itm;

                        //read all the lines
                        while (!reader.EndOfStream)
                        {
                            line = reader.ReadLine();
                            string[] words = line.Split(',');
                            //listboxLines.Items.Add(line);
                            itm = new ListViewItem(words);
                            lvLines.Items.Add(itm);
                        }
                    }
                    catch (Exception er)
                    {
                        var form = new FormTimedMessage(2000, "ABLine File is Corrupt", "Please delete it!!!");
                        form.Show();
                        Log.EventWriter("FormSwapAB: " + er.ToString()); 
                    }
                }

                // go to bottom of list - if there is a bottom
                if (lvLines.Items.Count > 0) lvLines.Items[lvLines.Items.Count - 1].EnsureVisible();
            }

            //make sure at least a blank quickAB file exists
            string directoryNameQuickAB = RegistrySettings.fieldsDirectory + "\\CurrentField\\";
            if ((directoryNameQuickAB.Length > 0) && (!Directory.Exists(directoryNameQuickAB)))
            { Directory.CreateDirectory(directoryNameQuickAB); }
            filename = directoryNameQuickAB + "QuickAB.txt";
            if (!File.Exists(filename))
            {
                using (StreamWriter writer = new StreamWriter(filename))
                {
                    writer.WriteLine("ABLine N S,0,0,0");
                    writer.WriteLine("ABLine E W,90,0,0");
                }
            }

            //get the file of previous AB Lines
            if ((directoryName.Length > 0) && (!Directory.Exists(directoryName)))
            { Directory.CreateDirectory(directoryName); }

            filename = directoryName + "QuickAB.txt";

            if (!File.Exists(filename))
            {
                Log.EventWriter("FormSwapAB: Missing QuickAB File, Critical Error");
            }
            else
            {
                using (StreamReader reader = new StreamReader(filename))
                {
                    try
                    {
                        string line;

                        //read all the lines
                        {
                            line = reader.ReadLine();
                            string[] words = line.Split(',');
                            // TODO: Fix AB1 property access for .NET 8.0 migration
                            string ab1FieldName = words[0];
                            double ab1Heading = double.Parse(words[1], CultureInfo.InvariantCulture);
                            double ab1X = double.Parse(words[2], CultureInfo.InvariantCulture);
                            double ab1Y = double.Parse(words[3], CultureInfo.InvariantCulture);

                            lblField1.Text = ab1FieldName;
                            lblHeading1.Text = ab1Heading.ToString("N5");

                            line = reader.ReadLine();
                            words = line.Split(',');
                            // TODO: Fix AB2 property access for .NET 8.0 migration
                            string ab2FieldName = words[0];
                            double ab2Heading = double.Parse(words[1], CultureInfo.InvariantCulture);
                            double ab2X = double.Parse(words[2], CultureInfo.InvariantCulture);
                            double ab2Y = double.Parse(words[3], CultureInfo.InvariantCulture);

                            lblField2.Text = ab2FieldName;
                            lblHeading2.Text = ab2Heading.ToString("N5");
                        }
                    }
                    catch (Exception er)
                    {
                        var form = new FormTimedMessage(2000, "QuickAB File is Corrupt", "Please delete it!!!");
                        form.Show();
                        Log.EventWriter("FormSwapAB: " + er.ToString()); 
                    }
                }
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            int count = lvLines.SelectedItems.Count;
            if (count > 0)
            {
                btnAB1.Enabled = true;
                btnAB2.Enabled = true;
            }
            else
            {
                btnAB1.Enabled = false;
                btnAB2.Enabled = false;
            }
        }
    }
}