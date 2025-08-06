using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using AOG.Classes;

namespace AOG
{
    public partial class FormToolPicker : Form
    {
        //class variables
        private AOG.FormGPS mf = null;

        public FormToolPicker(Form callingForm)
        {
            //get copy of the calling main form
            mf = callingForm as AOG.FormGPS;
            InitializeComponent();

            this.Text = gStr.Get(gs.gsLoadTool);
        }

        private void FormFlags_Load(object sender, EventArgs e)
        {
            lblLast.Text = gStr.Get(gs.gsCurrent) + RegistrySettings.toolFileName;

            DirectoryInfo dinfo = new DirectoryInfo(RegistrySettings.toolsDirectory);
            FileInfo[] Files = dinfo.GetFiles("*.txt");
            if (Files.Length == 0)
            {
                Close();
                mf.YesMessageBox(gStr.Get(gs.gsNoToolSaved) + " " + gStr.Get(gs.gsSaveAToolFirst));

            }

            foreach (FileInfo file in Files)
            {
                cboxTool.Items.Add(Path.GetFileNameWithoutExtension(file.Name));
            }
        }

        private void cboxVeh_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.Tool.Load(); // Load tool settings
            Close();
        }
    }
}