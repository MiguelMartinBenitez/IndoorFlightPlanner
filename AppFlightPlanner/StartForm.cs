using AppFlightPlanner;
using System;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;

namespace AppFlightPlanner
{
    public partial class StartForm : Form
    {
        public StartForm()
        {
            InitializeComponent();
            Text = "FlightPlanner";
        }
        private void button1_Click(object sender, EventArgs e)
        {
            var f = new CargaEspacio();
            this.Hide();
            f.Show();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var f = new VueloExterior1();
            this.Hide();
            f.FormClosed += (s, args) => this.Show();
            f.Show();
        }
    }


}
