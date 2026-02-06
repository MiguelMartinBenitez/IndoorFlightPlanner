using AppFlightPlanner.Data.Modelos;
using AppFlightPlanner.Data.Repositorios;
using csDronLink2;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace AppFlightPlanner
{
    public partial class CrearPlan : Form
    {
        private readonly RepoEspacios.SpaceRow _space;
        private MetricCanvas _canvas;

        private Dron dron = new Dron();
        private Bitmap iconoPersonalizado;
        private List<(float lat, float lon)> mision;
        int num = 0;
        private bool modoManualActivo = false;
        private PointF? posicionDron = null;
        private bool esperandoPosicionInicial = false;
        private bool _enAire = false;
        double latInicial;
        double lonInicial;
        double headingReal;
        double headingInicial;
        double alturaInicial;
        bool primeraTelemetria = true;
        double X = 0;
        double Y = 0;
        double Xinicial;
        double Yinicial;
        double heading;
        double altura;
        double headingdroninicial;
        private bool dronPosicionDefinida = false;
        
        List<List<List<(float x, float y)>>> scenarios = new List<List<List<(float x, float y)>>>();
        float alturaMax;
        private List<WaypointPlan> waypoints = new List<WaypointPlan>();
        int a = 0;
        private bool ColocarWaypoint = false;
        private WaypointPlan? _waypointSeleccionado;
        private ContextMenuStrip _menuWaypoint;

        public CrearPlan(RepoEspacios.SpaceRow space)
        {
            InitializeComponent();
            _space = space;
            StartPosition = FormStartPosition.CenterScreen;
            Text = $"Vuelo interior — {_space.Nombre}";
            _canvas = new MetricCanvas { Dock = DockStyle.Left, Size = new Size(990, 990) };
            Controls.Add(_canvas);
            _canvas.PuntoWaypointSeleccionado += _canvas_PuntoWaypointSeleccionado;
            _canvas.ColocarWaypointCambiado += _canvas_ColocarWaypointCambiado;
            _canvas.WaypointRightClick += Canvas_WaypointRightClick;
            alturaMax = _space.Layers.Max(l => l.AlturaFin);
            try
            {
                string pathIcono = Path.Combine(Application.StartupPath, "Resources", "dron.png");
                _canvas.DronIcon = Properties.Resources.dron;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo cargar el icono del dron: {ex.Message}");
            }
            comboCapas.DropDownStyle = ComboBoxStyle.DropDownList;

            if (_space.Layers != null && _space.Layers.Count > 0)
            {
                _canvas.Layers.Clear();

                alturaMax = _space.Layers.Max(l => l.AlturaFin);

                foreach (var layer in _space.Layers)
                {
                    var newLayer = new Layer
                    {
                        Nombre = layer.Nombre,
                        AlturaInicio = layer.AlturaInicio,
                        AlturaFin = layer.AlturaFin,
                        Polygons = layer.Polygons
                            .Select(poly => poly.Select(p => new PointF(p.X, p.Y)).ToList())
                            .ToList(),
                        Circles = layer.Circles
                            .Select(c => (new PointF(c.center.X, c.center.Y), c.r))
                            .ToList(),
                        Areas = layer.Areas
                            .Select(a => new Area
                            {
                                Tipo = a.Tipo,
                                EsExclusion = a.EsExclusion,
                                Vertices = a.Vertices?.Select(v => new PointF(v.X, v.Y)).ToList() ?? new List<PointF>(),
                                Centro = new PointF(a.Centro.X, a.Centro.Y),
                                Radio = a.Radio
                            })
                            .ToList()
                    };

                    _canvas.Layers.Add(newLayer);
                }

                comboCapas.DataSource = _canvas.Layers;
                comboCapas.DisplayMember = "ToString";
                comboCapas.SelectedIndexChanged += ComboCapas_SelectedIndexChanged;

                _canvas.SetActiveLayer(0);
                _canvas.Invalidate();
                trackBar1.Maximum = (int)alturaMax - 1;
                trackBar3.Maximum = (int)alturaMax - 1;





            }
            else
            {
                MessageBox.Show("Este espacio no contiene capas o datos válidos.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            if (_space.StartPoints != null)
            {
                foreach (var sp in _space.StartPoints)
                    _canvas.StartPoints.Add((new PointF(sp.x, sp.y), sp.heading));
            }
            _canvas.Invalidate();

            _canvas.PuntoInicioSeleccionado += (pos, headingSel) =>
            {
                _canvas.DronPosicionM = pos;
                _canvas.DronHeading = headingSel;
                _canvas.FijarInicioPlan(new PointF(pos.X, pos.Y));
                _canvas.Invalidate();

                dronPosicionDefinida = true;
                MessageBox.Show($"Dron colocado en ({pos.X:0.00}m, {pos.Y:0.00}m) con heading {headingSel:0}°");

                X = pos.X;
                Y = pos.Y;
                headingdroninicial = headingSel;

            };


            _menuWaypoint = new ContextMenuStrip();

            _menuWaypoint.Items.Add("📷 Añadir foto", null, (_, __) =>
            {
                if (_waypointSeleccionado == null) return;

                float? headingFoto = null;
                float? alturaFoto = _waypointSeleccionado.altura;

                var r = MessageBox.Show(
                    "¿Quieres fijar un heading específico para la foto?",
                    "Foto en waypoint",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (r == DialogResult.Yes)
                {
                    using var f = new Form
                    {
                        Text = "Heading de la foto",
                        Width = 250,
                        Height = 130,
                        StartPosition = FormStartPosition.CenterParent,
                        FormBorderStyle = FormBorderStyle.FixedDialog,
                        MaximizeBox = false,
                        MinimizeBox = false
                    };

                    var num = new NumericUpDown
                    {
                        Minimum = 0,
                        Maximum = 359,
                        Value = 0, 
                        Dock = DockStyle.Top
                    };

                    var btn = new Button
                    {
                        Text = "Aceptar",
                        Dock = DockStyle.Bottom,
                        DialogResult = DialogResult.OK
                    };

                    f.Controls.Add(num);
                    f.Controls.Add(btn);
                    f.AcceptButton = btn;

                    if (f.ShowDialog(this) == DialogResult.OK && (int)num.Value < 360)
                        headingFoto = (float)num.Value;
                }

                var rAltura = MessageBox.Show("¿Quieres fijar una altura específica para esta foto?", "Foto en waypoint", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (rAltura == DialogResult.Yes)
                {
                    using var fAltura = new Form
                    {
                        Text = "Altura de la foto (m)",
                        Width = 250,
                        Height = 130,
                        StartPosition = FormStartPosition.CenterParent,
                        FormBorderStyle = FormBorderStyle.FixedDialog,
                        MaximizeBox = false,
                        MinimizeBox = false
                    };

                    var numAltura = new NumericUpDown
                    {
                        Minimum = 1,
                        Maximum = (decimal)alturaMax,          
                        DecimalPlaces = 0,
                        Value = (decimal)_waypointSeleccionado.altura,
                        Dock = DockStyle.Top
                    };

                    var btnAltura = new Button
                    {
                        Text = "Aceptar",
                        Dock = DockStyle.Bottom,
                        DialogResult = DialogResult.OK
                    };

                    fAltura.Controls.Add(numAltura);
                    fAltura.Controls.Add(btnAltura);
                    fAltura.AcceptButton = btnAltura;

                    if (fAltura.ShowDialog(this) == DialogResult.OK && (int)numAltura.Value < alturaMax)
                        alturaFoto = (float)numAltura.Value;
                }

                _waypointSeleccionado.Actions.Add(new WaypointAction
                {
                    Type = WaypointActionType.TakePhoto,
                    Heading = headingFoto,
                    Altitude = alturaFoto,
                    Label = "Foto"
                });

                _canvas.Invalidate();
            });

            _menuWaypoint.Items.Add("🎥 Iniciar vídeo", null, (_, __) =>
            {
                if (_waypointSeleccionado == null) return;

                _waypointSeleccionado.Actions.Add(new WaypointAction
                {
                    Type = WaypointActionType.StartVideo
                });

                _canvas.Invalidate();
            });

            _menuWaypoint.Items.Add("⏹ Parar vídeo", null, (_, __) =>
            {
                if (_waypointSeleccionado == null) return;

                _waypointSeleccionado.Actions.Add(new WaypointAction
                {
                    Type = WaypointActionType.StopVideo
                });

                _canvas.Invalidate();
            });




            _canvas.WaypointClick += (s, wp) =>
            {
                _waypointSeleccionado = wp;
                _menuWaypoint.Show(Cursor.Position);
            };

            _canvas.MouseDown -= null;
            _canvas.MouseMove -= null;
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

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                esperandoPosicionInicial = true;
                _canvas.SeleccionarPuntoInicio = true;
                MessageBox.Show("Haz clic en un punto azul para colocar el dron.");
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (dronPosicionDefinida)
            {
                if (ColocarWaypoint == false)
                {
                    ColocarWaypoint = true;
                    button1.BackColor = Color.LightBlue;
                    _canvas.EstablecerModoColocarWaypoint(true);
                    return;
                }
                if (ColocarWaypoint == true)
                {
                    ColocarWaypoint = false;
                    button1.BackColor = SystemColors.Control;
                    _canvas.EstablecerModoColocarWaypoint(false);
                    return;
                }
            }
            else
            {
                MessageBox.Show("Debes colocar el dron en su posición inicial.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }


        }

        private void _canvas_PuntoWaypointSeleccionado(object sender, (float x, float y) e)
        {
            if (waypoints.Count < 5)
            {
                float altura = float.Parse(label8.Text);

                if (altura < 1)
                {
                    MessageBox.Show("La altura debe ser superior a 0 metros.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var wp = new WaypointPlan
                {
                    x = e.x,
                    y = e.y,
                    altura = altura,
                    Actions = new List<WaypointAction>() 
                };

                waypoints.Add(wp);



                //_canvas.DibujarWaypoint(e.x, e.y, waypoints.Count, altura);


                //_canvas.DibujarLineaEntreWaypoints(waypoints);

                _canvas.ActualizarWaypoints(waypoints);


            }
            else
            {
                MessageBox.Show("Solo puedes agregar hasta 5 waypoints.");
            }
        }

        private void _canvas_ColocarWaypointCambiado(object sender, bool activar)
        {
            ColocarWaypoint = activar;
            if (ColocarWaypoint)
            {
                MessageBox.Show("Modo 'Colocar Waypoint' activado. Haz clic en el canvas para colocar waypoints.");
            }
            else
            {
                MessageBox.Show("Modo 'Colocar Waypoint' desactivado.");
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (waypoints.Count > 0)
            {

                waypoints.RemoveAt(waypoints.Count - 1);
                _canvas.Invalidate();
                _canvas.ActualizarWaypoints(waypoints);





            }
            else
            {
                MessageBox.Show("No hay waypoints para eliminar.");
            }
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            int n = trackBar1.Value;
            label8.Text = n.ToString();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (!dronPosicionDefinida)
            {
                MessageBox.Show("Debes colocar la posición inicial del dron.");
                return;
            }

            if (waypoints.Count == 0)
            {
                MessageBox.Show("Debes definir por lo menos un waypoint.");
                return;
            }

            if (trackBar3.Value == 0)
            {
                MessageBox.Show("Debes introducir una altura de despegue.");
                return;
            }

            string nombrePlan = textBox1.Text.Trim();
            if (string.IsNullOrWhiteSpace(nombrePlan))
            {
                MessageBox.Show("Debes introducir un nombre para el plan de vuelo.");
                return;
            }

            float altura = (float)trackBar3.Value;

            var plan = new PlanDeVuelo
            {
                Nombre = textBox1.Text,
                x = (float)X,
                y = (float)Y,
                heading = (float)headingdroninicial,
                Waypoints = waypoints,
                Fecha = DateTime.UtcNow,
                AlturaDespegue = altura
            };

            var repo = new RepoEspacios();

            var espacio = repo.GetById(_space.Id);
            if (espacio == null)
            {
                MessageBox.Show("Espacio no encontrado");
                return;
            }

            espacio.FlightPlans.Add(plan);

            repo.UpdatePlanes(espacio.Id, espacio.FlightPlans);

            MessageBox.Show("Plan de vuelo guardado correctamente.");

            string basePath = Path.Combine(Application.StartupPath, "Media", "Fotos", _space.Nombre);


            // Limpieza básica de nombres
            string nombrePlanSeguro = string.Concat(
                plan.Nombre.Split(Path.GetInvalidFileNameChars())
            );

            string nombreCarpeta = $"{nombrePlanSeguro}";

            string _carpetaEjecucion = Path.Combine(basePath, nombreCarpeta);

            Directory.CreateDirectory(_carpetaEjecucion);

            this.Close();
        }

        private void trackBar3_Scroll(object sender, EventArgs e)
        {
            int n = trackBar3.Value;
            label11.Text = n.ToString();
        }

        private void Canvas_WaypointRightClick(object sender, WaypointPlan wp)
        {
            if (wp.Actions.Count == 0)
            {
                MessageBox.Show("Este waypoint no tiene acciones.");
                return;
            }

            var menu = new ContextMenuStrip();

            foreach (var action in wp.Actions.ToList())
            {
                string texto = action.Type switch
                {
                    WaypointActionType.TakePhoto => $"📸 Foto ({action.Heading ?? 0}°, {action.Altitude ?? 0}m)",
                    WaypointActionType.StartVideo => "🎥 Iniciar vídeo",
                    WaypointActionType.StopVideo => "🟥 Parar vídeo",
                    _ => "Acción"
                };

                var item = new ToolStripMenuItem(texto);
                item.Click += (_, __) =>
                {
                    wp.Actions.Remove(action);
                    _canvas.Invalidate();
                };

                menu.Items.Add(item);
            }

            menu.Show(Cursor.Position);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Una vez colocados los waypoints, haga click izquierdo sobre uno de los waypoints para seleccionar si tomar una foto, iniciar un vídeo o pararlo. Haga click derecho para ver las acciones a realizar y eliminar las que se deseen.", "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
