using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using csDronLink2;
using AForge;
using AForge.Video;
using AForge.Video.DirectShow;

//PRUEBA PARA LA CAMARA

namespace AppFlightPlanner
{
    public partial class VueloExterior1 : Form
    {
        Camara cam = new Camara();
        public VueloExterior1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            MostrarCamarasDetectadas();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {

                if (!cam.StartCamera())
                {
                    MessageBox.Show("No se encontró ninguna cámara");
                    return;
                }

                string path = cam.TakePhoto(@"C:\Users\migue\OneDrive\Desktop\AppFlightPlanner\AppFlightPlanner\Media\Fotos");

                MessageBox.Show("Foto guardada en:\n" + path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            
        }

        private void MostrarCamarasDetectadas()
        {
            var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

            if (devices.Count == 0)
            {
                MessageBox.Show("No se detecta ninguna cámara.");
                return;
            }

            string msg = "Cámaras detectadas:\n\n";
            for (int i = 0; i < devices.Count; i++)
            {
                msg += $"{i}: {devices[i].Name}\n";
            }

            MessageBox.Show(msg);
        }
    }
}
