using AppFlightPlanner.Data.Modelos;
using AppFlightPlanner.Data.Repositorios;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppFlightPlanner
{
    public partial class BuscadorVuelos : Form
    {
        private readonly string _mediaRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Media", "Fotos");

        private readonly RepoEspacios _repo = new RepoEspacios();
        private string _spaceFolder;
        private string _planFolder;
        private string _runFolder;

        private SpaceItem _selSpace;
        private PlanItem _selPlan;
        private RunItem _selRun;
        public BuscadorVuelos()
        {
            InitializeComponent();
            listBox1.DisplayMember = null;
            listBox2.DisplayMember = null;
            listBox3.DisplayMember = "RunFolderName";
            CargarEspacios();
        }

        private class SpaceItem
        {
            public string SpaceFolderName { get; set; } 
            public RepoEspacios.SpaceRow Space { get; set; } 
            public override string ToString() => SpaceFolderName;
        }

        private class PlanItem
        {
            public string PlanFolderName { get; set; } 
            public PlanDeVuelo Plan { get; set; }      
            public override string ToString() => PlanFolderName;
        }

        private class RunItem
        {
            public string RunFolderPath { get; set; }  
            public string RunFolderName { get; set; }
            public override string ToString()
            {
                return RunFolderName; // o lo que quieras mostrar
            }
            public FlightRunManifest Manifest { get; set; }
            //public override string ToString()
            //{
                // si el manifest tiene fecha, mejor
                //if (Manifest != null && Manifest.Fecha != default(DateTime))
                    //return $"{Manifest.Fecha:yyyy-MM-dd HH:mm:ss}";
                //return RunFolderName;
        }
        

        private void CargarEspacios()
        {
            Directory.CreateDirectory(_mediaRoot);
            listBox1.Items.Clear();

            var spacesDb = _repo.GetAll();

            foreach (var dir in Directory.GetDirectories(_mediaRoot))
            {
                var folderName = Path.GetFileName(dir);

                // Intento mapear folderName -> SpaceRow (por nombre)
                // Si tu carpeta usa otro formato, ajusta
                var match = spacesDb.FirstOrDefault(s =>
                    string.Equals(s.Nombre, folderName, StringComparison.OrdinalIgnoreCase));

                listBox1.Items.Add(new SpaceItem
                {
                    SpaceFolderName = folderName,
                    Space = match
                });
            }

            //foreach (var dir in Directory.GetDirectories(_mediaRoot))
            //listBox1.Items.Add(Path.GetFileName(dir));
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            _selSpace = listBox1.SelectedItem as SpaceItem;

            listBox2.Items.Clear();
            listBox3.Items.Clear();
            _selPlan = null;
            _selRun = null;

            if (_selSpace == null) return;

            var spacePath = Path.Combine(_mediaRoot, _selSpace.SpaceFolderName);
            if (!Directory.Exists(spacePath)) return;

            foreach (var dir in Directory.GetDirectories(spacePath))
            {
                var folderName = Path.GetFileName(dir);

                // Intento mapear PlanFolderName -> PlanDeVuelo
                // Si guardas PlanId en un plan.json, sería mejor (te lo recomiendo)
                PlanDeVuelo planMatch = null;

                if (_selSpace.Space?.FlightPlans != null)
                {
                    planMatch = _selSpace.Space.FlightPlans
                        .FirstOrDefault(p => string.Equals(p.Nombre, folderName, StringComparison.OrdinalIgnoreCase));
                }

                listBox2.Items.Add(new PlanItem
                {
                    PlanFolderName = folderName,
                    Plan = planMatch
                });
            }



            //_spaceFolder = Path.Combine(_mediaRoot, (string)listBox1.SelectedItem);
            //listBox2.Items.Clear();
            //listBox3.Items.Clear();

            //foreach (var dir in Directory.GetDirectories(_spaceFolder))
            //listBox2.Items.Add(Path.GetFileName(dir));
        }

        private void listBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            _selPlan = listBox2.SelectedItem as PlanItem;

            listBox3.Items.Clear();
            _selRun = null;

            if (_selSpace == null || _selPlan == null) return;

            var planPath = Path.Combine(_mediaRoot, _selSpace.SpaceFolderName, _selPlan.PlanFolderName);
            if (!Directory.Exists(planPath)) return;

            foreach (var dir in Directory.GetDirectories(planPath).OrderByDescending(d => d))
            {
                var runFolderName = Path.GetFileName(dir);
                var manifestPath = Path.Combine(dir, "run.json");

                FlightRunManifest manifest = null;
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        manifest = JsonConvert.DeserializeObject<FlightRunManifest>(File.ReadAllText(manifestPath));
                    }
                    catch
                    {
                        // si el json está corrupto, no rompas la app
                        manifest = null;
                    }
                }

                listBox3.Items.Add(new RunItem
                {
                    RunFolderName = runFolderName,
                    RunFolderPath = dir,
                    Manifest = manifest
                });
            }


            //_planFolder = Path.Combine(_spaceFolder, (string)listBox2.SelectedItem);
            //listBox3.Items.Clear();

            //foreach (var dir in Directory.GetDirectories(_planFolder).OrderByDescending(d => d))
            //listBox3.Items.Add(Path.GetFileName(dir));
        }

        private void listBox3_SelectedIndexChanged(object sender, EventArgs e)
        {
            _selRun = listBox3.SelectedItem as RunItem;
            //_runFolder = Path.Combine(_planFolder, (string)listBox3.SelectedItem);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (_selSpace == null || _selPlan == null || _selRun == null)
            {
                MessageBox.Show("Selecciona Espacio, Plan y Vuelo.");
                return;
            }
            if (!Directory.Exists(_selRun.RunFolderPath)) return;

            Process.Start("explorer.exe", _selRun.RunFolderPath);
            //if (string.IsNullOrWhiteSpace(_runFolder) || !Directory.Exists(_runFolder)) return;
            //Process.Start("explorer.exe", _runFolder);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (_selSpace == null || _selPlan == null || _selRun == null)
            {
                MessageBox.Show("Selecciona Espacio, Plan y Vuelo.");
                return;
            }

            if (_selRun.Manifest == null)
            {
                MessageBox.Show("No se pudo cargar run.json de este vuelo.");
                return;
            }

            // ⚠️ Lo ideal: que el manifest tenga PlanId y SpaceId.
            // Si NO lo tiene, y _selPlan.Plan es null, no podrás dibujar el plan “real”.
            // (porque no tienes los waypoints ni acciones desde DB)
            if (_selSpace.Space == null)
            {
                MessageBox.Show("No se pudo mapear el espacio de la carpeta a la base de datos (SpaceRow).");
                return;
            }

            if (_selPlan.Plan == null)
            {
                MessageBox.Show("No se pudo mapear el plan de la carpeta a la base de datos (PlanDeVuelo). " +
                                "Recomendado: guardar PlanId en run.json.");
                return;
            }

            // Aquí le pasas TODO al visor
            using (var f = new FotosPlan(_selSpace.Space, _selPlan.Plan, _selRun.Manifest, _selRun.RunFolderPath))
            {
                f.ShowDialog(this);
            }



            //if (string.IsNullOrWhiteSpace(_runFolder) || !Directory.Exists(_runFolder)) return;

            //string manifestPath = Path.Combine(_runFolder, "run.json");
            //if (!File.Exists(manifestPath))
            //{
            //MessageBox.Show("No se encontró run.json en este vuelo.");
            //return;
            //}

            //var manifest = CargarManifest(manifestPath);

            //using (var f = new FotosPlan(manifest))
            //f.ShowDialog(this);
        }

        private FlightRunManifest CargarManifest(string path)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<FlightRunManifest>(File.ReadAllText(path));
        }
    }
}
