using AppFlightPlanner.Data.Modelos;
using AppFlightPlanner.Data.Repositorios;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppFlightPlanner
{
    public partial class FotosPlan : Form
    {
        private readonly RepoEspacios.SpaceRow _space;
        private readonly PlanDeVuelo _plan;
        private readonly FlightRunManifest _manifest;
        private readonly string _runFolder;
        

        private MetricCanvas _canvas;
        public FotosPlan(RepoEspacios.SpaceRow space, PlanDeVuelo plan, FlightRunManifest run, string runFolder)
        {
            InitializeComponent();
            _space = space;
            _plan = plan;
            _manifest = run;
            _runFolder = runFolder;
            Width = 990;
            Height = 1090;
            Text = $"Run: {_manifest.SpaceName} / {_manifest.PlanName} / {_manifest.StartedAt:yyyy-MM-dd HH:mm:ss}";

            _canvas = new MetricCanvas { Dock = DockStyle.Top, Size = new Size(990, 990) };

           
            

            try
            {
                string pathIcono = Path.Combine(Application.StartupPath, "Resources", "dron.png");
                _canvas.DronIcon = Properties.Resources.dron;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo cargar el icono del dron: {ex.Message}");
            }


            _canvas.Layers.Clear();
            foreach (var layer in _space.Layers)
            {
                _canvas.Layers.Add(new Layer
                {
                    Nombre = layer.Nombre,
                    AlturaInicio = layer.AlturaInicio,
                    AlturaFin = layer.AlturaFin,
                    Polygons = layer.Polygons.Select(poly => poly.Select(p => new PointF(p.X, p.Y)).ToList()).ToList(),
                    Circles = layer.Circles.Select(c => (new PointF(c.center.X, c.center.Y), c.r)).ToList(),
                    Areas = layer.Areas.Select(a => new Area
                    {
                        Tipo = a.Tipo,
                        EsExclusion = a.EsExclusion,
                        Vertices = a.Vertices?.Select(v => new PointF(v.X, v.Y)).ToList() ?? new List<PointF>(),
                        Centro = new PointF(a.Centro.X, a.Centro.Y),
                        Radio = a.Radio
                    }).ToList()
                });
            }

            _canvas.SetActiveLayer(0);

            _canvas.DronPosicionM = new PointF(_plan.x, _plan.y);
            _canvas.DronHeading = _plan.heading;

            _canvas.FijarInicioPlan(new PointF(_plan.x, _plan.y));

            _canvas.ActualizarWaypoints(_plan.Waypoints);

            _canvas.Invalidate();

            // permitir click SIEMPRE (es un viewer, no una ejecución)
            _canvas.WaypointClick += (s, wp) =>
            {
                MostrarFotosDelWaypoint(wp);
            };

            comboCapas.DropDownStyle = ComboBoxStyle.DropDownList;
            comboCapas.DataSource = _canvas.Layers;
            comboCapas.DisplayMember = "ToString";
            comboCapas.SelectedIndexChanged += ComboCapas_SelectedIndexChanged;
            Controls.Add(_canvas);
        }
        private void ComboCapas_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboCapas.SelectedIndex >= 0)
            {
                _canvas.SetActiveLayer(comboCapas.SelectedIndex);
                _canvas.Invalidate();
                label1.Text = "Capa " + comboCapas.SelectedIndex;
            }
        }

        private int FindWaypointIndexByPosition(WaypointPlan wp)
        {
            // Buscar el waypoint del plan más cercano al clicado (por si no es la misma instancia)
            int best = -1;
            double bestDist = double.MaxValue;

            for (int i = 0; i < _plan.Waypoints.Count; i++)
            {
                var p = _plan.Waypoints[i];
                double dx = p.x - wp.x;
                double dy = p.y - wp.y;
                double d = dx * dx + dy * dy;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }

            return best;
        }

        private void MostrarFotosDelWaypoint(WaypointPlan wp)
        {
            int idx = FindWaypointIndexByPosition(wp);
            if (idx < 0 || idx >= _manifest.Waypoints.Count)
            {
                MessageBox.Show("No se pudo identificar el waypoint en este vuelo.");
                return;
            }

            var fotos = _manifest.Waypoints[idx].Actions
                .Where(a => a.Type == WaypointActionType.TakePhoto && !string.IsNullOrWhiteSpace(a.OutputPath))
                .Select(a =>
                {
                    string p = a.OutputPath;
                    return Path.IsPathRooted(p) ? p : Path.Combine(_runFolder, p);
                })
                .Where(File.Exists)
                .ToList();

            if (fotos.Count == 0)
            {
                MessageBox.Show("Este waypoint no tiene fotos en este vuelo.");
                return;
            }

            using (var f = new VerWaypoints(fotos))
                f.ShowDialog(this);
        }
    }
}
