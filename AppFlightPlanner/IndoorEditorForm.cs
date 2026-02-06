using AppFlightPlanner.Data.Modelos;
using AppFlightPlanner.Data.Repositorios;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace AppFlightPlanner
{
    public partial class IndoorEditorForm : Form
    {
        private enum ToolMode { None, Line, Circle }
        private ToolMode _mode = ToolMode.None;
        private bool _circlePending = false;
        private PointF _circleCenterM;
        private readonly Stack<string> _undo = new Stack<string>();
        private const double CLOSE_EPS = 0.001;
        private bool _snapEnabled = false;
        private const float SNAP_STEP = 0.5f;
        private List<PointF>? _poligonoBase = null;
        private List<PointF> StartPoints = new List<PointF>();
        private bool seleccionandoPuntoInicio = false;
        private MetricCanvas canvas = new MetricCanvas();
        

        public IndoorEditorForm()
        {
            InitializeComponent();

            this.canvas = new AppFlightPlanner.MetricCanvas();
            this.canvas.Dock = DockStyle.Left;
            this.canvas.Size = new Size(946, 864);
            this.canvas.Name = "canvas";
            this.Controls.Add(this.canvas);


            canvas.Layers.Clear();
            canvas.Layers.Add(new Layer { Nombre = "Capa 0", AlturaInicio = 0, AlturaFin = 2 });

            comboCapas.DataSource = null;
            comboCapas.DataSource = canvas.Layers;
            comboCapas.DisplayMember = "ToString";
            comboCapas.SelectedIndex = 0;
            comboCapas.Text = canvas.Layers[0].ToString();

            numAlturaCapa.Value = (decimal)canvas.Layers[0].AlturaFin;

            comboCapas.SelectedIndexChanged += (s, e) =>
            {
                if (comboCapas.SelectedIndex >= 0)
                {
                    canvas.SetActiveLayer(comboCapas.SelectedIndex);
                    numAlturaCapa.Value = (decimal)canvas.Layers[comboCapas.SelectedIndex].AlturaFin;
                    comboCapas.Text = canvas.Layers[comboCapas.SelectedIndex].ToString();
                }
            };

            // Cambiar altura final de la capa actual
            numAlturaCapa.ValueChanged += (s, e) =>
            {
                var idx = comboCapas.SelectedIndex;
                if (idx >= 0 && idx < canvas.Layers.Count)
                {
                    canvas.Layers[idx].AlturaFin = (float)numAlturaCapa.Value;
                    comboCapas.Text = canvas.Layers[idx].ToString();

                    // Si existe una capa siguiente, su AlturaInicio se ajusta automáticamente
                    if (idx + 1 < canvas.Layers.Count)
                    {
                        canvas.Layers[idx + 1].AlturaInicio = canvas.Layers[idx].AlturaFin;
                        comboCapas.Refresh();
                    }
                }
            };

            // Añadir nueva capa manualmente
            btnAgregarCapa.Click += (s, e) =>
            {
                if (_poligonoBase == null)
                {
                    MessageBox.Show("Debes definir el polígono base con cerrar polígono.",
                                    "Falta polígono base", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                var ultima = canvas.Layers.Last();
                var nueva = new Layer
                {
                    Nombre = $"Capa {canvas.Layers.Count}",
                    AlturaInicio = ultima.AlturaFin,
                    AlturaFin = ultima.AlturaFin + 2f // por defecto +2 m más alta
                };


                nueva.Polygons.Add(new List<PointF>(_poligonoBase));
                canvas.Layers.Add(nueva);

                comboCapas.DataSource = null;
                comboCapas.DataSource = canvas.Layers;
                comboCapas.DisplayMember = "ToString";
                comboCapas.SelectedIndex = canvas.Layers.Count - 1;
                numAlturaCapa.Value = (decimal)nueva.AlturaFin;
                comboCapas.Text = nueva.ToString();
                var pts = nueva.Polygons[nueva.Polygons.Count - 1];
                if (pts.Count < 3)
                {
                    MessageBox.Show("Necesitas al menos 3 puntos para cerrar el contorno.");
                    return;
                }

                var first = pts[0];
                var last = pts[pts.Count - 1];
                var dist = Math.Sqrt(Math.Pow(first.X - last.X, 2) + Math.Pow(first.Y - last.Y, 2));

                if (dist > CLOSE_EPS)
                    pts.Add(first);

                // Guardar el primer polígono de la capa 0 como área de inclusión
                // y los siguientes como áreas de exclusión.
                bool esExclusion = !(nueva.Areas.Count == 0);

                var nuevaArea = new Area
                {
                    Vertices = new List<PointF>(pts),
                    EsExclusion = esExclusion
                };

                nueva.Areas.Add(nuevaArea);

                _undo.Push("close");
                nueva.Polygons.Add(new List<PointF>());
                canvas.Invalidate();

            };

            canvas.MouseClick += canvas_MouseClick;
            canvas.MouseMove += canvas_MouseMove;
        }

        private static float Distance(PointF a, PointF b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private PointF ApplySnap(PointF m)
        {
            if (!_snapEnabled) return m;

            float x = (float)Math.Round(m.X / SNAP_STEP) * SNAP_STEP;
            float y = (float)Math.Round(m.Y / SNAP_STEP) * SNAP_STEP;
            return new PointF(x, y);
        }

        // GUARDAR
        private void button4_Click(object sender, EventArgs e)
        {
            var nombre = textBox1.Text.Trim();

            if (string.IsNullOrEmpty(nombre))
            {
                MessageBox.Show("Debes darle un nombre al espacio antes de guardarlo.",
                                "Nombre requerido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                textBox1.Focus();
                return;
            }

            if (!canvas.Layers.Any(l => l.Polygons.Any(p => p.Count >= 3) || l.Circles.Any()))
            {
                MessageBox.Show("Debes dibujar al menos un polígono o círculo antes de guardar.",
                                "Falta contenido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (canvas.StartPoints.Count == 0)
            {
                MessageBox.Show("Debes declarar al menos un punto de inicio de vuelo.",
                                "Falta contenido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var repo = new RepoEspacios();
            var id = repo.Insert(nombre, canvas.Layers, canvas.StartPoints.Select(sp => new StartPoint(sp.pos.X, sp.pos.Y, sp.heading)).ToList());

            MessageBox.Show($"Espacio '{nombre}' guardado (Id {id}).",
                            "Guardado", MessageBoxButtons.OK, MessageBoxIcon.Information);

            string basePath = Path.Combine(Application.StartupPath, "Media", "Fotos");

            // Limpieza básica de nombres
            string nombreEspacioSeguro = string.Concat(
                nombre.Split(Path.GetInvalidFileNameChars())
            );

            

            string nombreCarpeta = $"{nombreEspacioSeguro}";

            string _carpetaEjecucion = Path.Combine(basePath, nombreCarpeta);

            Directory.CreateDirectory(_carpetaEjecucion);

            string basePath2 = Path.Combine(Application.StartupPath, "Media", "Fotos", nombreCarpeta);
            string _carpetaEjecucion2 = Path.Combine(basePath2, "Vuelos Manuales");
            Directory.CreateDirectory(_carpetaEjecucion2);

            DialogResult = DialogResult.OK;
            Close();
        }

        // CANCELAR 
        private void button6_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        // MODO LÍNEA
        private void button1_Click(object sender, EventArgs e)
        {
            _mode = ToolMode.Line;
            _circlePending = false;
            Cursor = Cursors.Cross;
            label3.Text = "Modo: Línea";
        }

        // MODO CÍRCULO
        private void button2_Click(object sender, EventArgs e)
        {
            _mode = ToolMode.Circle;
            _circlePending = false;
            Cursor = Cursors.Cross;
            label3.Text = "Modo: Círculo";
        }

        // CLICK EN CANVAS
        private void canvas_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            var m = canvas.S2M(e.Location);
            m = ApplySnap(m);

            if (seleccionandoPuntoInicio && canvas.StartPoints.Count < 3)
            {
                string input = Microsoft.VisualBasic.Interaction.InputBox("Introduce el heading inicial (0–360°):", "Heading inicial", "0");

                if (float.TryParse(input, out float heading) && heading >= 0 && heading < 360)
                {
                    canvas.StartPoints.Add((m, heading));

                    canvas.Invalidate();
                    if (canvas.StartPoints.Count == 3)
                    {
                        seleccionandoPuntoInicio = false;
                        Cursor = Cursors.Default;
                        button9.BackColor = SystemColors.Control;
                        return;
                    }
                    return;
                }
                else
                {
                    MessageBox.Show("Heading inválido. Debe estar entre 0 y 360.");
                }

            }


            var layer = canvas.ActiveLayer;
            if (layer == null)
            {
                MessageBox.Show("No hay ninguna capa activa.");
                return;
            }

            if (_mode == ToolMode.Line)
            {
                if (layer.Polygons.Count == 0)
                    layer.Polygons.Add(new List<PointF>());

                layer.Polygons[layer.Polygons.Count - 1].Add(m);
                _undo.Push("vertex");
                canvas.Invalidate();
            }
            else if (_mode == ToolMode.Circle)
            {
                if (!_circlePending)
                {
                    _circlePending = true;
                    _circleCenterM = m;
                }
                else
                {
                    var r = Distance(canvas.M2S(_circleCenterM), e.Location) / canvas.PixelsPerMeter;
                    if (r < 0.05f)
                    {
                        MessageBox.Show("El radio debe ser mayor de 0.05 m.");
                        return;
                    }

                    var areaCirc = new Area
                    {
                        EsExclusion = true,         // siempre exclusión
                        Tipo = "circle",            // -> añade este campo a Area (lo vemos abajo)
                        Centro = _circleCenterM,    // idem
                        Radio = r
                    };

                    layer.Circles.Add((_circleCenterM, r));
                    layer.Areas.Add(areaCirc);
                    _circlePending = false;
                    _undo.Push("circle");
                    canvas.Invalidate();

                    MessageBox.Show("🟥 Se ha creado un área de exclusión circular.",
                        "Área registrada",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
        }

        // MOVER RATÓN 
        private void canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_mode == ToolMode.Line && canvas.ActiveLayer?.Polygons.Count > 0 && canvas.ActiveLayer.Polygons[canvas.ActiveLayer.Polygons.Count - 1].Count > 0)
            {
                var lastPolygon = canvas.ActiveLayer.Polygons[canvas.ActiveLayer.Polygons.Count - 1
];

                var lastPoint = lastPolygon[lastPolygon.Count - 1];
                var lastScreen = canvas.M2S(lastPoint);
                var currentM = ApplySnap(canvas.S2M(e.Location));
                var currentScreen = canvas.M2S(currentM);

                canvas.Invalidate();
                using var g = canvas.CreateGraphics();
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                g.DrawLine(Pens.LightSlateGray, lastScreen, currentScreen);
                var lenM = Distance(lastScreen, currentScreen) / canvas.PixelsPerMeter;
                var midX = (lastScreen.X + currentScreen.X) / 2;
                var midY = (lastScreen.Y + currentScreen.Y) / 2;
                g.DrawString($"{lenM:0.00} m", this.Font, Brushes.Black, midX + 5, midY);
            }

            if (_mode == ToolMode.Circle && _circlePending)
            {
                var C = canvas.M2S(_circleCenterM);
                var currentM = ApplySnap(canvas.S2M(e.Location));
                var currentScreen = canvas.M2S(currentM);
                var rpx = Distance(C, currentScreen);

                canvas.Invalidate();
                using var g = canvas.CreateGraphics();
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.DrawEllipse(Pens.LightSalmon, C.X - rpx, C.Y - rpx, rpx * 2, rpx * 2);

                var rMeters = rpx / canvas.PixelsPerMeter;
                g.DrawString($"{rMeters:0.00} m", this.Font, Brushes.Black, C.X + rpx + 5, C.Y);
            }
        }

        // CERRAR POLÍGONO 
        private void button3_Click(object sender, EventArgs e)
        {
            if (canvas.ActiveLayer == null)
            {
                MessageBox.Show("No hay ninguna capa activa.");
                return;
            }

            var pts = canvas.ActiveLayer.Polygons[canvas.ActiveLayer.Polygons.Count - 1];
            if (pts.Count < 3)
            {
                MessageBox.Show("Necesitas al menos 3 puntos para cerrar el contorno.");
                return;
            }

            var first = pts[0];
            var last = pts[pts.Count - 1];
            var dist = Math.Sqrt(Math.Pow(first.X - last.X, 2) + Math.Pow(first.Y - last.Y, 2));

            if (dist > CLOSE_EPS)
                pts.Add(first);

            // Guardar el primer polígono de la capa 0 como área de inclusión
            // y los siguientes como áreas de exclusión.
            bool esExclusion = !(canvas.ActiveLayer.Areas.Count == 0);

            var nuevaArea = new Area
            {
                Vertices = new List<PointF>(pts),
                EsExclusion = esExclusion
            };

            canvas.ActiveLayer.Areas.Add(nuevaArea);

            if (!esExclusion)
            {
                _poligonoBase = new List<PointF>(pts);
                MessageBox.Show("🟩 Se ha creado el área base de inclusión (zona válida de vuelo).",
                                "Área de inclusión registrada", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("🟥 Se ha creado un área de exclusión (obstáculo o zona prohibida).",
                                "Área de exclusión registrada", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            _undo.Push("close");
            canvas.ActiveLayer.Polygons.Add(new List<PointF>());
            canvas.Invalidate();
        }

        // DESHACER 
        private void button5_Click(object sender, EventArgs e)
        {
            if (canvas.ActiveLayer == null) return;

            if (_mode == ToolMode.Circle && _circlePending)
            {
                _circlePending = false;
                canvas.Invalidate();
                return;
            }

            if (_undo.Count == 0)
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            var last = _undo.Pop();
            switch (last)
            {
                case "vertex":
                    if (canvas.ActiveLayer.Polygons.Count > 0 &&
                        canvas.ActiveLayer.Polygons[canvas.ActiveLayer.Polygons.Count - 1].Count > 0)
                        canvas.ActiveLayer.Polygons[canvas.ActiveLayer.Polygons.Count - 1].RemoveAt(canvas.ActiveLayer.Polygons[canvas.ActiveLayer.Polygons.Count - 1].Count - 1);
                    break;

                case "circle":
                    if (canvas.ActiveLayer.Circles.Count > 0)
                        canvas.ActiveLayer.Circles.RemoveAt(canvas.ActiveLayer.Circles.Count - 1);
                    break;

                case "close":
                    if (canvas.ActiveLayer.Polygons.Count > 1 &&
                        canvas.ActiveLayer.Polygons[canvas.ActiveLayer.Polygons.Count - 1].Count == 0)
                        canvas.ActiveLayer.Polygons.RemoveAt(canvas.ActiveLayer.Polygons.Count - 1);
                    break;
            }

            canvas.Invalidate();
        }

        // BORRAR TODO
        private void button7_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this, "¿Seguro que quieres borrar todo?", "Confirmar",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                if (canvas.ActiveLayer == null) return;

                canvas.ActiveLayer.Polygons.Clear();
                canvas.ActiveLayer.Polygons.Add(new List<PointF>());
                canvas.ActiveLayer.Circles.Clear();

                _circlePending = false;
                _undo.Clear();
                canvas.Invalidate();
            }
        }

        // SNAP
        private void button8_Click(object sender, EventArgs e)
        {
            _snapEnabled = !_snapEnabled;
            button8.BackColor = _snapEnabled ? Color.LightGreen : SystemColors.Control;
        }

        private void comboExclusionMode_SelectedIndexChanged(object sender, EventArgs e)
        {
        }

        private void button9_Click(object sender, EventArgs e)
        {
            seleccionandoPuntoInicio = !seleccionandoPuntoInicio;
            if (canvas.StartPoints.Count >= 3)
            {
                MessageBox.Show("Solo puedes definir hasta 3 puntos de inicio.");
                seleccionandoPuntoInicio = false;
                Cursor = Cursors.Default;
                button9.BackColor = SystemColors.Control;
                return;
            }
            if (seleccionandoPuntoInicio)
            {
                Cursor = Cursors.Cross;
                button9.BackColor = Color.LightBlue;
                MessageBox.Show("Haz clic en el canvas para colocar hasta 3 puntos de inicio del dron (azules).");
            }
            if (!seleccionandoPuntoInicio)
            {
                button9.BackColor = SystemColors.Control;
                Cursor = Cursors.Default;
            }


        }
    }
}
