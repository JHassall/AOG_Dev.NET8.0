using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AOG;
using AOG.Classes;

namespace AOG
{
    public partial class FormEnvPicker : Form
    {
        //class variables
        private readonly FormGPS mf = null;

        public FormEnvPicker(Form callingForm)
        {
            //get copy of the calling main form
            mf = callingForm as FormGPS;
            InitializeComponent();

            //this.bntOK.Text = gStr.gsForNow;
            //this.btnSave.Text = gStr.gsToFile;

            this.Text = gStr.Get(gs.gsLoadEnvironment);
        }

        private void FormFlags_Load(object sender, EventArgs e)
        {
            lblLast.Text = gStr.Get(gs.gsCurrent) + " " + gStr.Get(gs.gsNone) + RegistrySettings.envFileName;
            DirectoryInfo dinfo = new DirectoryInfo(RegistrySettings.envDirectory);
            FileInfo[] Files = dinfo.GetFiles("*.txt");
            if (Files.Length == 0)
            {
                DialogResult = DialogResult.Ignore;
                Close();
                mf.YesMessageBox(gStr.Get(gs.gsNoEnvironmentSaved) + " " + gStr.Get(gs.gsSaveAnEnvironmentFirst));
            }

            else
            {
                cboxEnv.DataSource = Directory.GetFiles(RegistrySettings.envDirectory, "*.xml").Select(Path.GetFileNameWithoutExtension).ToArray();
            }
        }

        private void cboxVeh_SelectedIndexChanged(object sender, EventArgs e)
        {
            // TODO: Fix FileOpenEnvironment method reference for .NET 8.0 migration
            // mf.FileOpenEnvironment(RegistrySettings.envDirectory + cboxEnv.SelectedItem.ToString() + ".txt");

            DialogResult resul = DialogResult.Ignore; // Added this line to avoid compilation error

            if (resul == DialogResult.OK)
            {
                DialogResult = DialogResult.OK;
            }
            else if (resul == DialogResult.Abort)
            {
                DialogResult = DialogResult.Abort;
            }
            else
            {
                DialogResult = DialogResult.Cancel;
            }
            Close();
        }
    }
}