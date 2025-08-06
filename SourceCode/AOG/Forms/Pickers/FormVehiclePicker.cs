using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using AOG.Classes;

namespace AOG
{
    public partial class FormVehiclePicker : Form
    {
        //class variables
        private readonly AOG.FormGPS mf = null;

        public FormVehiclePicker(Form callingForm)
        {
            //get copy of the calling main form
            mf = callingForm as AOG.FormGPS;
            InitializeComponent();

            //this.bntOK.Text = gStr.gsForNow;
            //this.btnSave.Text = gStr.gsToFile;

            this.Text = gStr.Get(gs.gsLoadVehicle);
        }

        private void FormFlags_Load(object sender, EventArgs e)
        {
            lblLast.Text = gStr.Get(gs.gsCurrent) + RegistrySettings.vehicleFileName;
            DirectoryInfo dinfo = new DirectoryInfo(RegistrySettings.vehiclesDirectory);
            FileInfo[] Files = dinfo.GetFiles("*.xml");
            if (Files.Length == 0)
            {
                Close();
                mf.YesMessageBox(gStr.Get(gs.gsNoVehiclesSaved) + " " + gStr.Get(gs.gsSaveAVehicleFirst));
                var form = new FormTimedMessage(2000, gStr.Get(gs.gsNoVehiclesSaved), gStr.Get(gs.gsSaveAVehicleFirst));
                form.Show();

            }

            foreach (FileInfo file in Files)
            {
                cboxVeh.Items.Add(Path.GetFileNameWithoutExtension(file.Name));
            }
        }

        private void cboxVeh_SelectedIndexChanged(object sender, EventArgs e)
        {
            //mf.FileOpenVehicle(mf.vehiclesDirectory + cboxVeh.SelectedItem.ToString() + ".xml");
            Settings.Vehicle.Load(); // Load vehicle settings

            mf.LoadSettings();
            Close();
        }
    }
}