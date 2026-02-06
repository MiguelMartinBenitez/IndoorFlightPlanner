using AppFlightPlanner;
using AppFlightPlanner.Data.Modelos;
using AppFlightPlanner.Data.Repositorios;
using csDronLink2;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Windows.Forms;
using static AppFlightPlanner.VueloInterior1;
using static System.Windows.Forms.AxHost;

namespace AppFlightPlanner
{
    public partial class VueloInterior1 : Form
    {
        private readonly RepoEspacios.SpaceRow _space;
        private MetricCanvas _canvas;
        private Dron dron = new Dron();
        private List<WaypointPlan> waypoints;
        private List<WaypointPlan> waypointsM;
        private List<WaypointNED> _waypointsNED;
        float alturadespegue;
        private int _indiceWaypoint = 0;
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
        bool conectado = false;
        private bool dronPosicionDefinida = false;
        MiJoystick joystick = new MiJoystick();
        private Mando _mando;
        private bool _mandoactivo = false;
        float alturaMax;
        Camara cam = new Camara();
        string camino;
        string camino2;
        private string _carpetaEjecucion;
        private string _carpetaManual;
        private bool _cancelarEjecucion = false;
        private bool _planFinalizado = false;
        private FlightRunManifest _manifest;
        int j = 0;
        int k = 0;
        string path2;
        int a = 0;
        int b = 0;

        public struct WaypointNED
        {
            public double N;
            public double E;
            public double D;

            public WaypointNED(double n, double e, double d)
            {
                N = n;
                E = e;
                D = d;
            }
        }


        public VueloInterior1(RepoEspacios.SpaceRow space)
        {
            InitializeComponent();
            _space = space;
            StartPosition = FormStartPosition.CenterScreen;
            Text = $"Vuelo interior — {_space.Nombre}";
            label1.Text = "Vuelo interior : " + _space.Nombre;

            //CANVAS
            _canvas = new MetricCanvas { Dock = DockStyle.Left, Size = new Size(990, 990) };
            Controls.Add(_canvas);
            _canvas.MouseClick += Canvas_MouseClick;


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

            //CAPAS
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
            }
            else
            {
                MessageBox.Show("Este espacio no contiene capas o datos válidos.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // === PUNTOS DE INICIO ===
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
                _canvas.Invalidate();

                dronPosicionDefinida = true;
                MessageBox.Show($"Dron colocado en ({pos.X:0.00}m, {pos.Y:0.00}m) con heading {headingSel:0}°");

                X = pos.X;
                Y = pos.Y;
                headingdroninicial = headingSel;
                dron.EnviarDatosTelemetria(ProcesarTelemetria);
                //int n = mando.Start();


            };

            _canvas.WaypointClick += (s, wp) =>
            {
                if (!_planFinalizado) return; 
                MostrarFotosDelWaypoint(wp);
            };

            _canvas.MouseDown -= null;
            _canvas.MouseMove -= null;

            CMB_comport.DropDown += CMB_comport_DropDown;

            button6.Tag = "Forward";
            button7.Tag = "Back";
            button8.Tag = "Right";
            button9.Tag = "Left";
            button11.Tag = "Up";
            button10.Tag = "Down";

            button6.Click += movButton_Click;
            button7.Click += movButton_Click;
            button8.Click += movButton_Click;
            button9.Click += movButton_Click;
            button10.Click += movButton_Click;
            button11.Click += movButton_Click;

            //mando.ButtonPressed += Mando_ButtonPressed;
            //joystick.ButtonDown += Joystick_ButtonDown;




        }
        


        private static double Normalize360(double a)
        {
            a %= 360.0;
            if (a < 0) a += 360.0;
            return a;
        }

        private static double Normalize2PI(double a)
        {
            a %= 2 * Math.PI;
            if (a < 0) a += 2 * Math.PI;
            return a;
        }

        

        private double RealToSala(double realHeading)
        {
            double delta = headingdroninicial - headingInicial;
            return Normalize360(realHeading + delta);
        }

        private double SalaToReal(double salaHeading)
        {
            double delta = headingdroninicial - headingInicial;
            return Normalize360(salaHeading - delta);
        }



        private (double Xr, double Yr) RotateToRoom(double posX, double posY)
        {
            double deltaRad = (headingdroninicial - headingInicial) * Math.PI / 180.0;
            double xr = posX * Math.Cos(deltaRad) + posY * Math.Sin(deltaRad);
            double yr = -posX * Math.Sin(deltaRad) + posY * Math.Cos(deltaRad);
            return (xr, yr);
        }

        private (double x, double y) RotateFromRoom(double X, double Y)
        {
            double deltaRad = (headingdroninicial - headingInicial) * Math.PI / 180.0;
            double x = X * Math.Cos(deltaRad) - Y * Math.Sin(deltaRad);  
            double y = X * Math.Sin(deltaRad) + Y * Math.Cos(deltaRad); 

            return (x, y);
        }

        private void ComboCapas_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboCapas.SelectedIndex >= 0)
            {
                _canvas.SetActiveLayer(comboCapas.SelectedIndex);
                _canvas.Invalidate();
                label5.Text = "Capa " + comboCapas.SelectedIndex;
            }
        }

        private void Canvas_MouseClick(object sender, MouseEventArgs e)
        {
            if (modoManualActivo && esperandoPosicionInicial)
                esperandoPosicionInicial = false;
        }

        private void ProcesarTelemetria(List<(string nombre, float valor)> telemetria)
        {
            double lat = ((double)telemetria[1].valor) / 0.1E+8;
            double lon = ((double)telemetria[2].valor) / 0.1E+8;
            headingReal = ((double)telemetria[3].valor) / 100.0;
            double posY = ((double)telemetria[4].valor);
            double posX = ((double)telemetria[5].valor);

            if (primeraTelemetria)
            {
                latInicial = lat;
                lonInicial = lon;
                alturaInicial = 0;
                headingInicial = headingReal;
                Xinicial = X;
                Yinicial = Y;
                primeraTelemetria = false;
                Leer_parametros();
            }

            heading = RealToSala(headingReal);
            var (Xr, Yr) = RotateToRoom(posX, posY);

            X = Xinicial + Xr;
            Y = Yinicial - Yr;
            altura = -telemetria[6].valor;

            //label4.Text = $"Posición incial= X:{Xinicial:0.00}m, Y:{Yinicial:0.00}m";
            //label14.Text = $"Posición actual= X:{X:0.00}m, Y:{Y:0.00}m";
            //label12.Text = $"Heading: {heading:0.00}º";
            //label13.Text = $"Altura: {altura:0.00}m";

            label19.Text = $"X: {X:0.00}m";
            label20.Text = $"Y: {Y:0.00}m";
            label21.Text = $"Heading: {heading:0.00}º";
            label22.Text = $"Altura: {altura:0.00}m";

            int hUI = (int)Math.Round(heading);
            if (hUI < 0) hUI += 360; if (hUI >= 360) hUI -= 360;
            headingTrackBar.Value = hUI;
            alturaTrackBar.Value = (int)Math.Round(altura);

            label8.Text = altura.ToString("0");
            label10.Text = hUI.ToString("0");

            _canvas.DronHeading = (float)heading;
            _canvas.DronPosicionM = new PointF((float)X, (float)Y);
            _canvas.Invalidate();


            // Determinar la capa actual según la altura
            if (_canvas.Layers != null && _canvas.Layers.Count > 0)
            {
                // Buscar capa donde la altura actual esté dentro de su rango
                var capaActual = _canvas.Layers
                    .Select((capa, index) => new { capa, index })
                    .FirstOrDefault(x => altura >= x.capa.AlturaInicio && altura < x.capa.AlturaFin);

                if (capaActual != null && _canvas.ActiveLayerIndex != capaActual.index)
                {
                    // Cambiar la capa activa en el canvas
                    _canvas.SetActiveLayer(capaActual.index);
                    _canvas.Invalidate();

                    // Actualizar el ComboBox
                    comboCapas.SelectedIndexChanged -= ComboCapas_SelectedIndexChanged; // evitar bucle de eventos
                    comboCapas.SelectedIndex = capaActual.index;
                    comboCapas.SelectedIndexChanged += ComboCapas_SelectedIndexChanged;

                    label5.Text = "Capa " + comboCapas.SelectedIndex;
                }
            }

            





            


        }

        private void EnAire(object param)
        {
            _enAire = true;
            MessageBox.Show("El dron ha despegado correctamente.");
        }

        private void CMB_comport_DropDown(object sender, EventArgs e)
        {
            try { CMB_comport.DataSource = SerialPort.GetPortNames(); }
            catch { MessageBox.Show("No se pueden listar los puertos COM en este entorno."); }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (radioButton3.Checked)
            {
                try
                {
                    dron.Conectar("produccion", CMB_comport.Text);
                    button1.BackColor = Color.Green;
                    button1.ForeColor = Color.White;
                    button1.Text = "Conectado";
                    conectado = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al conectar: {ex.Message}");
                }
            }
            else
            {
                dron.Conectar("simulacion");
                button1.BackColor = Color.Green;
                button1.ForeColor = Color.White;
                button1.Text = "Conectado";
                conectado = true;
                label23.Text = "En tierra";
            }
        }

        

        private void movButton_Click(object sender, EventArgs e)
        {
            if (!_enAire)
            {
                MessageBox.Show("El dron debe estar en el aire para moverse.");
                return;
            }

            Button b = (Button)sender;
            string direccion = b.Tag.ToString();
            int distancia = Convert.ToInt32(label2.Text);

            var dronPos = new PointF((float)X, (float)Y);

            // Calcular el destino (punto final) del movimiento
            var destino = CalcularDestino(dronPos, direccion, distancia, heading);

            if (VerificarColisionesEnTrayecto(dronPos, destino, 0.2f))
            {
                MessageBox.Show("⚠️ El dron no puede moverse porque el trayecto cruza una zona roja (exclusión o borde de inclusión).",
                                "Seguridad", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }

            // Heading actual en sala:
            double headingSalaActual = heading; // ya calculado en ProcesarTelemetria con RealToSala()

            double headingSalaObjetivo = headingSalaActual;

            switch (direccion)
            {
                case "Forward":
                    headingSalaObjetivo = headingSalaActual;
                    break;

                case "Back":
                    headingSalaObjetivo = Normalize360(headingSalaActual + 180);
                    break;

                case "Left":
                    headingSalaObjetivo = Normalize360(headingSalaActual - 90);
                    break;

                case "Right":
                    headingSalaObjetivo = Normalize360(headingSalaActual + 90);
                    break;

                case "Up":
                    double nuevaAltura = altura + distancia;

                    if (HayObstaculoVertical((float)altura, (float)nuevaAltura, new PointF((float)X, (float)Y), 0.2f))
                    {
                        MessageBox.Show("⚠️ No puedes ascender: obstáculo en la capa superior justo encima del dron.");
                        return;
                    }
                    if (nuevaAltura >= alturaMax - 0.2)
                    {
                        MessageBox.Show("⚠️ No puedes ascender: el dron va a impactar contra el techo.");
                        return;
                    }
                    dron.Mover("Up", distancia, bloquear: false);
                    return;


                case "Down":
                    double nuevaAltura2 = altura - distancia;

                    if (HayObstaculoVertical((float)altura, (float)nuevaAltura2, new PointF((float)X, (float)Y), 0.2f))
                    {
                        MessageBox.Show("⚠️ No puedes descender: obstáculo en la capa inferior justo debajo del dron.");
                        return;
                    }
                    dron.Mover("Down", distancia, bloquear: false);
                    return;
            }

            // Convertimos ese heading sala a heading real físico
            double headingRealObjetivo = SalaToReal(headingSalaObjetivo);

            dron.CambiarHeading((float)headingRealObjetivo, bloquear: true);
            dron.Mover("Forward", distancia, bloquear: false);

        }

        private void button12_Click(object sender, EventArgs e)
        {
            if (num == 0)
            {
                if (label8.Text == "0" || label8.Text == "-0")
                {
                    MessageBox.Show("Seleccione una altura de despegue.");
                }
                else
                {
                    button12.Text = "Despegando";
                    label23.Text = "Despegando";
                    dron.Despegar(int.Parse(label8.Text), bloquear: false, EnAire, "Volando");
                    button12.BackColor = Color.Yellow;
                    button12.ForeColor = Color.Black;
                    button12.Text = "Aterrizar";
                    num = 1;
                    dron.EnviarDatosTelemetria(ProcesarTelemetria);
                    label23.Text = "Volando";
                    return;
                }
            }
            if (num == 1)
            {
                button12.Text = "Aterrizando";
                label23.Text = "Aterrizando";
                dron.Aterrizar(bloquear: false, EnTierra, "Aterrizaje");
                label23.Text = "En tierra";
                button12.BackColor = Color.Chocolate;
                button12.ForeColor = Color.Snow;
                button12.Text = "Despegar";
                num = 0;
            }
        }

        private void EnTierra(object mensaje)
        {
            _enAire = false;
            MessageBox.Show("El dron ha aterrizado.");
            if (k == 1)
            {
                cam.StopVideo();
                MessageBox.Show("Vídeo guardado en:\n" + camino2);
                button16.Text = "▶";
                button16.BackColor = Color.Chocolate;
                k = 0;
            }
        }

        private void button13_Click(object sender, EventArgs e)
        {
            if (_enAire)
            {
                if (trackBar1.Value >= alturaMax)
                {
                    MessageBox.Show("La altura de RTL es superior a la del espacio. Por favor, reducir la altura");
                    return;
                }
                if (trackBar1.Value < 2)
                {
                    MessageBox.Show("La altura de RTL debe tener un valor mínimo de 2. Por favor, aumentar la altura");
                    return;
                }
                button12.BackColor = Color.Chocolate;
                button12.ForeColor = Color.Snow;
                button12.Text = "Despegar";
                button13.BackColor = Color.Yellow;
                button13.ForeColor = Color.Black;
                button13.Text = "Volviendo";
                label23.Text = "RTL";
                dron.RTL(bloquear: false, EnTierra, "RTL");
                num = 0;
                button13.BackColor = Color.Chocolate;
                button13.ForeColor = Color.Snow;
                button13.Text = "RTL";
                label23.Text = "En tierra";
            }
            else
            {
                MessageBox.Show("El dron no se encuentra en el aire", "RTL no disponible", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void velocidadTrackBar_Scroll(object sender, EventArgs e)
        {
            int n = velocidadTrackBar.Value;
            label6.Text = n.ToString();
        }

        private void alturaTrackBar_Scroll(object sender, EventArgs e)
        {
            dron.DetenerDatosTelemetria();
            int n = alturaTrackBar.Value;
            label8.Text = n.ToString();
        }

        private void velocidadTrackBar_MouseUp(object sender, MouseEventArgs e)
        {
            int valorSeleccionado = velocidadTrackBar.Value;
            dron.CambiaVelocidad(valorSeleccionado);
        }

        private void stepTrackBar_Scroll(object sender, EventArgs e)
        {
            int n = stepTrackBar.Value;
            label2.Text = n.ToString();
        }

        private void headingTrackBar_Scroll(object sender, EventArgs e)
        {
            dron.DetenerDatosTelemetria();
            int n = headingTrackBar.Value;
            label10.Text = n.ToString();
        }

        private void headingTrackBar_MouseUp(object sender, MouseEventArgs e)
        {
            // El usuario ha elegido un heading en la sala
            double headingSalaDeseado = headingTrackBar.Value;

            // Lo pasamos a mundo real
            double headingRealDeseado = SalaToReal(headingSalaDeseado);

            dron.CambiarHeading((float)headingRealDeseado, bloquear: false);
            dron.EnviarDatosTelemetria(ProcesarTelemetria);
        }

        private void alturaTrackBar_MouseUp(object sender, MouseEventArgs e)
        {
            if (!_enAire)
            {
                MessageBox.Show("El dron aún no ha despegado. Lo que se está intentando cambiar es la altura de despegue",
                    "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int nuevaAltura = alturaTrackBar.Value;

            // Verificar si se excede la altura máxima permitida
            if (_canvas.Layers != null && _canvas.Layers.Count > 0)
            {
                float alturaMax = _canvas.Layers.Max(l => l.AlturaFin);

                if (nuevaAltura > alturaMax)
                {
                    MessageBox.Show(
                        $"⚠️ No puedes subir más: la altura máxima del espacio es de {alturaMax:0.00} m.",
                        "Altura máxima alcanzada",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    alturaTrackBar.Value = (int)Math.Round(altura);
                    label8.Text = altura.ToString("0");
                    dron.EnviarDatosTelemetria(ProcesarTelemetria);
                    return;
                }

                if (HayObstaculoVertical((float)altura, nuevaAltura, new PointF((float)X, (float)Y), 0.2f))
                {
                    MessageBox.Show("⚠️ No se puede ejecutar la orden de cambio de altura porque hay un obstáculo en la altura seleccionada.", "Obstáculo detectado",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    alturaTrackBar.Value = (int)Math.Round(altura);
                    label8.Text = altura.ToString("0");
                    dron.EnviarDatosTelemetria(ProcesarTelemetria);
                    return;
                }
            }

            // Comprobar si el cambio de altura implica cambiar de capa
            var capaActual = _canvas.Layers
                .Select((capa, index) => new { capa, index })
                .FirstOrDefault(x => altura >= x.capa.AlturaInicio && altura < x.capa.AlturaFin);

            var capaDestino = _canvas.Layers
                .Select((capa, index) => new { capa, index })
                .FirstOrDefault(x => nuevaAltura >= x.capa.AlturaInicio && nuevaAltura < x.capa.AlturaFin);

            if (capaDestino != null && capaActual != null && capaDestino.index != capaActual.index)
            {
                // El dron intenta pasar a otra capa
                var respuesta = MessageBox.Show(
                    $"Vas a cambiar de la capa {capaActual.capa.Nombre} ({capaActual.capa.AlturaInicio}-{capaActual.capa.AlturaFin} m) " +
                    $"a la capa {capaDestino.capa.Nombre} ({capaDestino.capa.AlturaInicio}-{capaDestino.capa.AlturaFin} m).\n\n" +
                    $"¿Deseas continuar?",
                    "Cambio de capa",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (respuesta == DialogResult.No)
                {
                    alturaTrackBar.Value = (int)Math.Round(altura);
                    label8.Text = altura.ToString("0");
                    dron.EnviarDatosTelemetria(ProcesarTelemetria);
                    return;
                }
            }

            int distancia = nuevaAltura - (int)Math.Round(altura);
            string direction = "Up";

            if (distancia == 0)
            {
                dron.EnviarDatosTelemetria(ProcesarTelemetria);
                return;
            }
            if (distancia < 0)
            {
                direction = "Down";
                distancia *= -1;
            }

            dron.Mover(direction, distancia, bloquear: false);
            dron.EnviarDatosTelemetria(ProcesarTelemetria);
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (conectado == false)
            {
                if (radioButton1.Checked == true)
                {
                    MessageBox.Show("Debes establecer conexión previamente");
                    radioButton1.Checked = false;
                    return;
                }
            }
            if (radioButton1.Checked)
            {
                modoManualActivo = true;
                esperandoPosicionInicial = true;
                _canvas.SeleccionarPuntoInicio = true;
                MessageBox.Show("Haz clic en un punto azul para colocar el dron.");
            }
        }



        







        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            //mando.Stop(); // Detenemos el mando cuando cerramos la aplicación
            cam.StopCamera();
            if (_mandoactivo)
            {
                _mando.PararJoystick();
                _mandoactivo = false;
            }

        }
















        private bool HayColisionProxima(PointF posicionDron, float distanciaSeguridad = 0.5f)
        {
            if (_canvas?.ActiveLayer == null) return false;

            var layer = _canvas.ActiveLayer;

            // Recorremos las áreas (polígonos y círculos) de la capa activa
            foreach (var area in layer.Areas)
            {
                // Si es un área de exclusión
                if (area.EsExclusion)
                {
                    if (area.Tipo == "polygon" && area.Vertices.Count >= 3)
                    {
                        // 1️⃣ Comprobamos si la posición del dron está cerca de un polígono de exclusión
                        if (DistPointToPolygon(posicionDron, area.Vertices) <= distanciaSeguridad)
                        {
                            return true; // El dron está cerca de una zona de exclusión
                        }
                    }
                    else if (area.Tipo == "circle")
                    {
                        // 2️⃣ Comprobamos si la posición del dron está cerca de un círculo de exclusión
                        if (Distance(posicionDron, area.Centro) <= area.Radio + distanciaSeguridad)
                        {
                            return true; // El dron está cerca de una zona de exclusión
                        }
                    }
                }
                // Si es un área de inclusión
                else
                {
                    if (area.Tipo == "polygon" && area.Vertices.Count >= 3)
                    {
                        // 3️⃣ Comprobamos si la posición del dron está cerca del borde de un polígono de inclusión
                        if (DistPointToPolygon(posicionDron, area.Vertices) <= distanciaSeguridad)
                        {
                            return true; // El dron está cerca del límite del área de inclusión
                        }
                    }
                    else if (area.Tipo == "circle")
                    {
                        // 4️⃣ Si la posición del dron está cerca del borde de un círculo de inclusión
                        float dist = Distance(posicionDron, area.Centro);
                        if (dist >= area.Radio - distanciaSeguridad && dist <= area.Radio + distanciaSeguridad)
                        {
                            return true; // El dron está cerca del borde del círculo de inclusión
                        }
                    }
                }
            }

            return false; // No hay colisión
        }

        // Calcula la distancia mínima entre un punto y un polígono
        private float DistPointToPolygon(PointF punto, List<PointF> polygon)
        {
            float minDist = float.MaxValue;

            for (int i = 0; i < polygon.Count - 1; i++)
            {
                var p1 = polygon[i];
                var p2 = polygon[i + 1];

                minDist = Math.Min(minDist, DistPointToSegment(p1, p2, punto));
            }

            // También comprobar el último segmento entre el último y el primer punto
            minDist = Math.Min(minDist, DistPointToSegment(polygon[polygon.Count - 1], polygon[0], punto));

            return minDist;
        }

        private static float DistPointToSegment(PointF a, PointF b, PointF p)
        {
            // Calcula la distancia del punto p al segmento (a, b)
            float vx = b.X - a.X, vy = b.Y - a.Y;
            float wx = p.X - a.X, wy = p.Y - a.Y;
            float c1 = vx * wx + vy * wy;
            if (c1 <= 0) return Distance(a, p);
            float c2 = vx * vx + vy * vy;
            if (c2 <= c1) return Distance(b, p);
            float t = c1 / c2;
            PointF proj = new PointF(a.X + t * vx, a.Y + t * vy);
            return Distance(proj, p);
        }

        private static float Distance(PointF a, PointF b)
        {
            // Calcula la distancia entre dos puntos
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private PointF CalcularDestino(PointF posicionDron, string direccion, int distancia, double heading)
        {
            // Convertir el heading de grados a radianes
            double headingRad = heading * Math.PI / 180.0;
            if (direccion == "Back")
            {
                headingRad = Normalize2PI(headingRad + Math.PI);
            }
            if (direccion == "Left")
            {
                headingRad = Normalize2PI(headingRad - Math.PI / 2);
            }
            if (direccion == "Right")
            {
                headingRad = Normalize2PI(headingRad + Math.PI / 2);
            }

            double deltaX = distancia * Math.Sin(headingRad);
            double deltaY = distancia * Math.Cos(headingRad);




            float nuevoX = posicionDron.X + (float)deltaX;
            float nuevoY = posicionDron.Y - (float)deltaY;

            return new PointF(nuevoX, nuevoY);
        }

        private bool EstaEnZonaRoja(PointF destino, float distanciaSeguridad = 0.2f)
        {
            if (_canvas?.ActiveLayer == null) return false;

            var layer = _canvas.ActiveLayer;

            // Verificar zonas de exclusión
            foreach (var area in layer.Areas)
            {
                if (area.EsExclusion) // Si es una zona de exclusión
                {
                    if (area.Tipo == "polygon" && area.Vertices.Count >= 3)
                    {
                        // Verificamos si el destino está dentro de un polígono de exclusión
                        if (DistPointToPolygon(destino, area.Vertices) <= distanciaSeguridad)
                        {
                            return true; // El destino está dentro de una zona de exclusión
                        }
                    }
                    else if (area.Tipo == "circle")
                    {
                        // Verificamos si el destino está dentro de un círculo de exclusión
                        if (Distance(destino, area.Centro) <= area.Radio + distanciaSeguridad)
                        {
                            return true; // El destino está dentro de una zona de exclusión
                        }
                    }
                }
                else // Si es un área de inclusión, comprobamos si está cerca del borde
                {
                    if (area.Tipo == "polygon" && area.Vertices.Count >= 3)
                    {
                        // Verificamos si el destino está cerca del borde de un polígono de inclusión
                        if (DistPointToPolygon(destino, area.Vertices) <= distanciaSeguridad)
                        {
                            return true; // El destino está cerca del borde del área de inclusión (zona roja)
                        }
                    }
                    else if (area.Tipo == "circle")
                    {
                        // Verificamos si la posición está cerca del borde de un círculo de inclusión
                        float dist = Distance(destino, area.Centro);
                        if (dist >= area.Radio - distanciaSeguridad && dist <= area.Radio + distanciaSeguridad)
                        {
                            return true; // El destino está cerca del borde del círculo de inclusión (zona roja)
                        }
                    }
                }
            }

            return false; // El destino no está dentro de ninguna zona roja
        }

        private List<PointF> GenerarPuntosIntermedios(PointF posicionDron, PointF destino, int numPuntos)
        {
            List<PointF> puntosIntermedios = new List<PointF>();

            float deltaX = destino.X - posicionDron.X;
            float deltaY = destino.Y - posicionDron.Y;

            for (int i = 1; i <= numPuntos; i++)
            {
                // Calculamos los puntos intermedios entre la posición inicial y el destino
                float factor = i / (float)(numPuntos + 1);
                float x = posicionDron.X + factor * deltaX;
                float y = posicionDron.Y + factor * deltaY;

                puntosIntermedios.Add(new PointF(x, y));
            }

            return puntosIntermedios;
        }

        private bool VerificarColisionesEnTrayecto(PointF posicionDron, PointF destino, float distanciaSeguridad = 0.2f)
        {
            // Generamos puntos intermedios entre la posición actual del dron y el destino
            int numPuntos = 10; // Dividimos el trayecto en 10 puntos intermedios (ajustable)
            var puntosIntermedios = GenerarPuntosIntermedios(posicionDron, destino, numPuntos);

            // Verificamos si alguno de los puntos intermedios está dentro de una zona roja
            foreach (var punto in puntosIntermedios)
            {
                if (EstaEnZonaRoja(punto, distanciaSeguridad)) // Usamos la misma función que ya tenías
                {
                    return true;  // Si un punto intermedio está en una zona roja, detengo el movimiento
                }
            }

            return false;  // Si ningún punto está en una zona roja, el trayecto es seguro
        }





        private bool PointInPolygon(PointF p, List<PointF> poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                bool intersect = ((poly[i].Y > p.Y) != (poly[j].Y > p.Y)) &&
                                 (p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y + 0.000001f) + poly[i].X);
                if (intersect) inside = !inside;
            }
            return inside;
        }

        private float DistanciaMinimaAZonaRoja(PointF pos, float margen = 0.2f)
        {
            if (_canvas?.ActiveLayer == null) return float.PositiveInfinity;

            var layer = _canvas.ActiveLayer;
            float min = float.PositiveInfinity;

            foreach (var area in layer.Areas)
            {
                if (area.Tipo == "polygon" && area.Vertices?.Count >= 3)
                {
                    float distBorde = DistPointToPolygon(pos, area.Vertices);

                    if (area.EsExclusion)
                    {
                        // si está dentro del obstáculo => “colisión”
                        if (PointInPolygon(pos, area.Vertices))
                            return 0f; // ya estás dentro

                        // si no, distancia hasta tocar obstáculo (con margen)
                        min = Math.Min(min, distBorde - margen);
                    }
                    else
                    {
                        // Área de inclusión: lo rojo es acercarte al borde o salirte
                        bool dentro = PointInPolygon(pos, area.Vertices);
                        if (!dentro) return 0f; // ya estás fuera del área válida

                        min = Math.Min(min, distBorde - margen);
                    }
                }
                else if (area.Tipo == "circle")
                {
                    float distCentro = Distance(pos, area.Centro);

                    if (area.EsExclusion)
                    {
                        // dentro del círculo = colisión
                        if (distCentro <= area.Radio) return 0f;

                        min = Math.Min(min, (distCentro - area.Radio) - margen);
                    }
                    else
                    {
                        // inclusión circular: tienes que estar dentro
                        if (distCentro > area.Radio) return 0f;

                        min = Math.Min(min, (area.Radio - distCentro) - margen);
                        // ojo: esto mide cuanto te queda hasta el borde desde dentro
                    }
                }
            }

            return min;
        }






        private void Leer_parametros()
        {

            List<string> list = new List<string>();
            list.Add("RTL_ALT");
            List<float> resultado = dron.LeerParametros(list);
            float altura = resultado[0] / 100;
            label15.Text = altura.ToString();

            trackBar1.Value = (int)altura;

        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            int n = trackBar1.Value;
            label15.Text = n.ToString();
        }

        private void trackBar1_MouseUp(object sender, MouseEventArgs e)
        {
            List<(string parametro, float valor)> parametros = new List<(string parametro, float valor)>();
            double v = Convert.ToDouble(label15.Text) * 100;
            parametros.Add(("RTL_ALT", (float)v));
            dron.EscribirParametros(parametros);
        }







        private Layer GetCapaDestino(float alturaActual, float cambioAltura)
        {
            float alturaFinal = alturaActual + cambioAltura;

            return _space.Layers
                .FirstOrDefault(l => alturaFinal >= l.AlturaInicio && alturaFinal < l.AlturaFin);
        }


        private bool HayObstaculoVertical(float alturaActual, float nuevaAltura, PointF posicion, float distanciaSeguridad)
        {
            Layer capaDestino;
            if (nuevaAltura - alturaActual >= 0)
            {
                capaDestino = GetCapaDestino(alturaActual, nuevaAltura - alturaActual + distanciaSeguridad);
            }
            else
            {
                capaDestino = GetCapaDestino(alturaActual, nuevaAltura - alturaActual - distanciaSeguridad);
            }

            if (capaDestino == null) return false;

            // Recorremos las áreas de la capa destino
            foreach (var area in capaDestino.Areas)
            {
                // Polígono de exclusión
                if (area.EsExclusion && area.Tipo == "polygon" && area.Vertices.Count >= 3)
                {
                    if (DistPointToPolygon(posicion, area.Vertices) <= distanciaSeguridad)
                        return true;
                }

                // Círculo de exclusión
                if (area.EsExclusion && area.Tipo == "circle")
                {
                    if (Distance(posicion, area.Centro) <= area.Radio + distanciaSeguridad)
                        return true;
                }
            }

            return false;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var f = new CrearPlan(_space);
            f.Show();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (radioButton2.Checked)
            {
                var repo = new RepoEspacios();
                var spaceFresh = repo.GetById(_space.Id);

                if (spaceFresh == null)
                {
                    MessageBox.Show("No se pudo recargar el espacio desde la base de datos.");
                    return;
                }

                var lista = new ListaPlanesVuelo(spaceFresh);

                if (lista.ShowDialog() == DialogResult.OK && lista.PlanSeleccionado != null)
                {
                    var plan = lista.PlanSeleccionado;

                    string basePath = Path.Combine(Application.StartupPath, "Media", "Fotos", _space.Nombre, plan.Nombre);

                    string fecha = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

                    

                    string nombreCarpeta = $"{fecha}";

                    _carpetaEjecucion = Path.Combine(basePath, nombreCarpeta);

                    Directory.CreateDirectory(_carpetaEjecucion);

                    _manifest = new FlightRunManifest
                    {
                        SpaceId = _space.Id,
                        SpaceName = _space.Nombre,
                        PlanName = plan.Nombre,
                        StartedAt = DateTime.Now,
                        FolderPath = _carpetaEjecucion
                    };

                    _manifest.Waypoints = plan.Waypoints
                        .Select((wp, i) => new FlightRunWaypoint
                        {
                            Index = i,
                            X = wp.x,
                            Y = wp.y,
                            Altura = wp.altura,
                            Actions = (wp.Actions ?? new List<WaypointAction>())
                                .Select(a => new WaypointRunAction
                                {
                                    Type = a.Type,
                                    Heading = a.Heading,
                                    OutputPath = null
                                }).ToList()
                        })
                        .ToList();

                    GuardarRunJson();

                    _canvas.DronPosicionM = new PointF(plan.x, plan.y);
                    _canvas.DronHeading = plan.heading;
                    _canvas.FijarInicioPlan(new PointF(plan.x, plan.y));
                    X = plan.x;
                    Y = plan.y;
                    headingdroninicial = plan.heading;
                    alturadespegue = plan.AlturaDespegue;

                    waypoints = plan.Waypoints;
                    //waypointsM = WaypointsScreenAMetros(waypoints, _canvas);
                    _canvas.ActualizarWaypoints(waypoints);
                    _canvas.LimpiarPuntosInicio();


                    MessageBox.Show("Plan cargado correctamente.");
                    dronPosicionDefinida = true;
                    _canvas.Invalidate();
                    dron.EnviarDatosTelemetria(ProcesarTelemetria);





                }
            }
            else
            {
                MessageBox.Show("Debes seleccionar el modo automático.", "Modo automático", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }


        }



        public static List<(float x, float y, float altura)> WaypointsScreenAMetros(List<(float x, float y, float altura)> waypointsPx, MetricCanvas canvas)
        {
            var resultado = new List<(float x, float y, float altura)>();

            foreach (var wp in waypointsPx)
            {
                PointF pMetros = canvas.S2M(new PointF(wp.x, wp.y));

                resultado.Add((
                    pMetros.X,
                    pMetros.Y,
                    wp.altura
                ));
            }

            return resultado;
        }
        public static List<WaypointNED> ConvertirWaypointsANED(List<WaypointPlan> waypoints, float startX, float startY, float headingSala, float headingReal)
        {
            var resultado = new List<WaypointNED>();


            double deltaRad = (headingSala - headingReal) * Math.PI / 180.0;

            foreach (var wp in waypoints)
            {
                // Vector relativo en SALA
                double dxSala = wp.x - startX;
                double dySala = startY - wp.y;

                // Rotar SALA → REAL
                double dxReal = dxSala * Math.Cos(deltaRad) - dySala * Math.Sin(deltaRad);
                double dyReal = dxSala * Math.Sin(deltaRad) + dySala * Math.Cos(deltaRad);

                // REAL → NED
                double N = dyReal;
                double E = dxReal;
                double D = -wp.altura;

                resultado.Add(new WaypointNED(N, E, D));
                //MessageBox.Show("Dx = " + dxSala + "Dy = " + dySala);
            }

            return resultado;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            

            if (waypoints == null)
            {
                MessageBox.Show("No hay waypoints cargados.");
                return;
            }
            if (_canvas.DronPosicionM == null)
            {
                MessageBox.Show("El dron no tiene posición inicial.");
                return;
            }
            _waypointsNED = ConvertirWaypointsANED(waypoints, (float)X, (float)Y, (float)headingdroninicial, (float)headingInicial);

            _indiceWaypoint = 0;

            button4.Text = "Ejecutando";
            button4.BackColor = Color.Green;
            button4.ForeColor = Color.White;


            if (!_enAire)
            {
                dron.Despegar((int)alturadespegue, bloquear: true);
                _enAire = true;
            }

            _planFinalizado = false;
            _cancelarEjecucion = false;
            EjecutarSiguienteWaypoint();



        }

        private void EjecutarSiguienteWaypoint()
        {
            if (_cancelarEjecucion)
            {
                return; 
            }

            if (_indiceWaypoint >= _waypointsNED.Count)
            {
                BeginInvoke(new Action(() =>
                {
                    dron.Aterrizar(bloquear: false, EnTierra, "Aterrizaje");
                    MessageBox.Show("✅ Plan de vuelo completado. Pulsa sobre los waypoints para ver las imágenes");
                    button4.Text = "Ejecutar";
                    button4.BackColor = Color.Chocolate;
                    button4.ForeColor = Color.Snow;
                    cam.StopCamera();
                    _planFinalizado = true;
                    _manifest.FinishedAt = DateTime.UtcNow;
                    GuardarRunJson();

                }));
                return;
            }

            var wp = _waypointsNED[_indiceWaypoint];
            var wpPlan = waypoints[_indiceWaypoint];

            int AltDest = (int)wp.D * -1;
            int distancia = AltDest - (int)Math.Round(altura);
            string direction = "Up";

            if (distancia < 0)
            {
                direction = "Down";
                distancia *= -1;
            }

            if (distancia != 0)
            {
                dron.Mover(direction, distancia, bloquear: true);
            }

            dron.IrAlPuntoNED((float)wp.N, (float)wp.E, (float)wp.D, callback: _ =>
            {
                BeginInvoke(new Action(() =>
                {
                    _canvas.MarcarWaypointAlcanzado(_indiceWaypoint);
                    EjecutarAccionesWaypoint(wpPlan, () =>
                    {
                        _indiceWaypoint++;
                        EjecutarSiguienteWaypoint();
                    });
                }));
            }
            );
        }

        private void EjecutarAccionesWaypoint(WaypointPlan wp, Action onFinish)
        {
            if (wp.Actions == null || wp.Actions.Count == 0)
            {
                onFinish();
                return;
            }

            EjecutarAccion(wp.Actions, 0, onFinish);
        }

        private void EjecutarAccion(List<WaypointAction> actions, int index, Action onFinish)
        {
            if (index >= actions.Count)
            {
                onFinish();
                return;
            }

            var action = actions[index];

            switch (action.Type)
            {
                case WaypointActionType.TakePhoto:
                    EjecutarFoto(action, () =>
                        EjecutarAccion(actions, index + 1, onFinish));
                    break;

                case WaypointActionType.StartVideo:
                    EjecutarStartVideo(() =>
                        EjecutarAccion(actions, index + 1, onFinish));
                    break;

                case WaypointActionType.StopVideo:
                    EjecutarStopVideo(() =>
                        EjecutarAccion(actions, index + 1, onFinish));
                    break;
            }
        }


        private void EjecutarFoto(WaypointAction action, Action onDone)
        {
            if (action.Altitude.HasValue)
            {


                int distancia = (int)action.Altitude.Value - (int)Math.Round(altura);
                string direction = "Up";

                
                if (distancia < 0)
                {
                    direction = "Down";
                    distancia *= -1;
                }

                if (distancia != 0)
                {
                    dron.Mover(direction, distancia, bloquear: true);
                }

                
            }
            



            if (action.Heading.HasValue)
            {
                double headingSalaDeseado = action.Heading.Value;

                // Lo pasamos a mundo real
                double headingRealDeseado = SalaToReal(headingSalaDeseado);

                dron.CambiarHeading((float)headingRealDeseado, bloquear: true);
            }

            try
            {

                if (!cam.StartCamera())
                {
                    MessageBox.Show("No se encontró ninguna cámara");
                    return;
                }

                string path = cam.TakePhoto(_carpetaEjecucion);

                action.OutputPath = path;
                action.ExecutedAt = DateTime.Now;
                string fileRel = Path.GetFileName(path);  // o ruta relativa que prefieras

                var runWp = _manifest.Waypoints[_indiceWaypoint];
                var runAction = runWp.Actions.FirstOrDefault(a => a.Type == WaypointActionType.TakePhoto && string.IsNullOrWhiteSpace(a.OutputPath));
                if (runAction != null)
                    runAction.OutputPath = fileRel;
                GuardarRunJson();

                MessageBox.Show("Foto guardada en:\n" + path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            Thread.Sleep(500);         

            onDone();
        }

        private void EjecutarStartVideo(Action onDone)
        {
            try
            {

                if (!cam.StartCamera())
                {
                    MessageBox.Show("No se encontró ninguna cámara");
                    return;
                }

                camino = cam.StartVideo(_carpetaEjecucion);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            Thread.Sleep(500);         

            onDone();
        }

        private void EjecutarStopVideo(Action onDone)
        {
            cam.StopVideo();
            MessageBox.Show("Vídeo guardado en:\n" + camino);
            onDone();
        }




        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if (conectado == false)
            {

                MessageBox.Show("Debes establecer conexión previamente");
                radioButton2.Checked = false;
                return;

            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (!_enAire)
            {
                MessageBox.Show("El dron no está en vuelo.");
                return;
            }

            _cancelarEjecucion = true;

            button4.Text = "Ejecutar";
            button4.BackColor = Color.Chocolate;
            button4.ForeColor = Color.Snow;

            try
            {
                cam?.StopVideo();
                cam?.StopCamera();
            }
            catch { }

            dron.Aterrizar(bloquear: true, EnTierra, "Aterrizaje forzado");

            MessageBox.Show("🛑 Plan detenido. Aterrizando dron.");
        }

        private void button14_Click(object sender, EventArgs e)
        {
            var f = new BuscadorVuelos();
            f.Show();
            //var folder = Path.Combine(Application.StartupPath, "Media", "Fotos");
            //Directory.CreateDirectory(folder);
            //Process.Start("explorer.exe", folder);
        }

        private void MostrarFotosDelWaypoint(WaypointPlan wp)
        {
            var fotos = wp.Actions
                .Where(a => a.Type == WaypointActionType.TakePhoto && !string.IsNullOrWhiteSpace(a.OutputPath) && File.Exists(a.OutputPath))
                .Select(a => a.OutputPath)
                .ToList();

            if (fotos.Count == 0)
            {
                MessageBox.Show("Este waypoint no tiene fotos.");
                return;
            }

            using (var f = new VerWaypoints(fotos))
                f.ShowDialog(this);
        }

        private void GuardarRunJson()
        {
            string path = Path.Combine(_carpetaEjecucion, "run.json");

            File.WriteAllText(
                path,
                JsonConvert.SerializeObject(_manifest, Formatting.Indented)
            );
        }

        private void button15_Click(object sender, EventArgs e)
        {
            if (radioButton1.Checked == false)
            {
                MessageBox.Show("Esta acción solo está habilitada para vuelos manuales");
                return;
            }

            if (!_enAire)
            {
                MessageBox.Show("El dron debe estar en el aire");
                return;
            }
            try
            {

                if (!cam.StartCamera())
                {
                    MessageBox.Show("No se encontró ninguna cámara");
                    return;
                }

                


                if (j == 0)
                {
                    string basePath = Path.Combine(Application.StartupPath, "Media", "Fotos", _space.Nombre, "Vuelos Manuales");
                    string fecha = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                    string nombreCarpeta = $"{fecha}";
                    _carpetaManual = Path.Combine(basePath, nombreCarpeta);
                    Directory.CreateDirectory(_carpetaManual);
                    
                    j = 1;
                }

                path2 = cam.TakePhoto(_carpetaManual);


                MessageBox.Show("Foto guardada en:\n" + path2);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            Thread.Sleep(500);


        }

        private void button16_Click(object sender, EventArgs e)
        {
            if (radioButton1.Checked == false)
            {
                MessageBox.Show("Esta acción solo está habilitada para vuelos manuales");
                return;
            }

            if (!_enAire)
            {
                MessageBox.Show("El dron debe estar en el aire");
                return;
            }
            if (k == 0)
            {
                try
                {

                    if (!cam.StartCamera())
                    {
                        MessageBox.Show("No se encontró ninguna cámara");
                        return;
                    }

                    if (j == 0)
                    {
                        string basePath = Path.Combine(Application.StartupPath, "Media", "Fotos", _space.Nombre, "Vuelos Manuales");
                        string fecha = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                        string nombreCarpeta = $"{fecha}";
                        _carpetaManual = Path.Combine(basePath, nombreCarpeta);
                        Directory.CreateDirectory(_carpetaManual);

                        j = 1;
                        
                    }

                    camino2 = cam.StartVideo(_carpetaManual);
                    button16.BackColor = Color.Red;
                    button16.Text = "⏹️";
                    k = 1;


                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
                Thread.Sleep(500);
                return;

            }
            if (k == 1)
            {
                cam.StopVideo();
                MessageBox.Show("Vídeo guardado en:\n" + camino2);
                button16.Text = "▶";
                button16.BackColor = Color.Chocolate;
                k = 0;
            }

        }

        private void button17_Click(object sender, EventArgs e)
        {
            if (b == 0)
            {
                int n = joystick.Conectar();
                if (!_mandoactivo && n == 1)
                {
                    _mando = new Mando(dron, getPosM: () => _canvas.DronPosicionM, getHeadingDeg: () => _canvas.DronHeading, distToRed: (p) => DistanciaMinimaAZonaRoja(p, margen: 0.25f), margenSeguridad: 0.25f);
                    _mandoactivo = true;
                }

                if (n == 0)
                {
                    MessageBox.Show("⚠️ No se encontró ningún mando conectado.");
                    Console.WriteLine(n.ToString());
                    return;
                }
                if (n == 1)
                {
                    MessageBox.Show("🎮 Mando PS4 conectado y listo para usar.");
                    button17.BackColor = Color.Green;
                    using var f = new MapaBotones();
                    f.ShowDialog();
                }
                dron.EnviarDatosTelemetria(ProcesarTelemetria);
                b = 1;
                return;
            }
            if (b == 1)
            {
                _mando.PararJoystick();
                MessageBox.Show("🎮 Mando PS4 desconectado.");
                button17.BackColor = Color.Chocolate;
                b = 0;
            }
        }
    }
}