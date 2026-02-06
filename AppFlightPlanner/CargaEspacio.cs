using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppFlightPlanner.Data.Repositorios;

namespace AppFlightPlanner
{
    public partial class CargaEspacio : Form
    {
        public CargaEspacio()
        {
            InitializeComponent();
            Text = "Vuelo interior";

        }

        private void button1_Click(object sender, EventArgs e)
        {
            var repo = new AppFlightPlanner.Data.Repositorios.RepoEspacios();
            var items = repo.GetAll();

            if (items.Count == 0)
            {
                MessageBox.Show(this, "Aún no se ha creado ningún espacio.", "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;

            }
            using var list = new IndoorListForm(items);
            var dr = list.ShowDialog(this);
            if (dr == DialogResult.OK && list.SelectedSpace != null)
            {
                var interior = new VueloInterior1(list.SelectedSpace);
                interior.Show();
                Close();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var editor = new IndoorEditorForm();
            editor.Show();
        }
    }



}

