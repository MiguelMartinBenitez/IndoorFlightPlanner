using AppFlightPlanner.Data.Modelos;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace AppFlightPlanner
{
    public class MetricCanvas : Control
    {
        public float PixelsPerMeter { get; private set; } = 50f;
        private PointF _origin = new PointF(50, 50);
        private bool _panning;
        private Point _panStart;

        public List<List<PointF>> Polygons { get; } = new List<List<PointF>>();
        public List<(PointF center, float r)> Circles { get; } = new List<(PointF center, float r)>();
        public List<Layer> Layers { get; } = new List<Layer>();
        public int ActiveLayerIndex { get; private set; } = 0;
        public float DronHeading { get; set; } = 0;
        public PointF? DronPosicionM { get; set; } = null;
        public Image? DronIcon { get; set; } = null;
        //public List<PointF> StartPoints { get; } = new();
        public List<(PointF pos, float heading)> StartPoints { get; } = new List<(PointF pos, float heading)>();
        public bool SeleccionarPuntoInicio { get; set; } = false;
        public event Action<PointF, float>? PuntoInicioSeleccionado;
        public List<Area> Areas { get; set; } = new List<Area>();
        public event EventHandler<(float x, float y)> PuntoWaypointSeleccionado;
        public event EventHandler<bool> ColocarWaypointCambiado;
        bool ColocarWaypoint = false;
        private List<WaypointPlan> waypoints = new List<WaypointPlan>();
        public event EventHandler<WaypointPlan> WaypointClick;
        private PointF? _inicioPlanM = null;
        private int _ultimoWaypointAlcanzado = -1;
        public event EventHandler<WaypointPlan> WaypointRightClick;


        public void SetActiveLayer(int index)
        {
            if (index < 0 || index >= Layers.Count)
                return;

            ActiveLayerIndex = index;
            Invalidate();
        }
        public Layer? ActiveLayer => Layers.Count > 0 ? Layers[ActiveLayerIndex] : null;

        public MetricCanvas()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            SetStyle(ControlStyles.ResizeRedraw, true);

            Polygons.Add(new List<PointF>());

            MouseWheel += MetricCanvas_MouseWheel;
            MouseDown += MetricCanvas_MouseDown;
            MouseMove += MetricCanvas_MouseMove;
            MouseUp += (s, e) => _panning = false;
        }

        public PointF M2S(PointF m) => new PointF(_origin.X + m.X * PixelsPerMeter,
                                           _origin.Y + m.Y * PixelsPerMeter);

        public PointF S2M(PointF s) => new PointF((s.X - _origin.X) / PixelsPerMeter,
                                           (s.Y - _origin.Y) / PixelsPerMeter);

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            DrawGrid(g);
            DrawAxes(g);
            if (ActiveLayer != null)
            {
                foreach (var area in ActiveLayer.Areas)
                {
                    if (area.Tipo == "polygon")
                    {
                        DibujarAreaPoligono(g, area);
                    }
                    else if (area.Tipo == "circle")
                    {
                        DibujarAreaCirculo(g, area);
                    }
                }
                DrawPolygons(g, ActiveLayer.Polygons);
                DrawCircles(g, ActiveLayer.Circles);
            }


            DrawStartPoints(g);
            DrawDron(g);
            if (waypoints.Count > 0)
            {
                for (int i = 0; i < waypoints.Count; i++)
                {
                    var waypoint = waypoints[i];
                    DibujarWaypoint(g, waypoint, i);
                    DibujarLineaEntreWaypoints(g, waypoints);
                }
            }


            //DrawAreas(g);
        }

        private void DibujarAreaPoligono(Graphics g, Area area)
        {
            if (area.Vertices == null || area.Vertices.Count < 3) return;

            // Pasar vertices (metros) -> pantalla
            var ptsScreen = area.Vertices.Select(v => M2S(v)).ToArray();

            // Área de exclusión:
            // - si EsExclusion == true pintamos rojo dentro
            // - si EsExclusion == false pintamos rojo fuera (inclusión válida = resto prohibido)
            if (area.EsExclusion)
            {
                using (Brush b = new SolidBrush(Color.FromArgb(80, Color.LightCoral)))
                    g.FillPolygon(b, ptsScreen);
            }
            else
            {
                // Rellenar TODO el canvas de rojo semitransparente menos el polígono (hueco)
                using (Brush bFondo = new SolidBrush(Color.FromArgb(80, Color.LightCoral)))
                using (Brush bHueco = new SolidBrush(Color.Transparent))
                using (Region regionExterior = new Region(new Rectangle(0, 0, Width, Height)))
                using (GraphicsPath pathPermitido = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    pathPermitido.AddPolygon(ptsScreen);
                    regionExterior.Exclude(pathPermitido);

                    g.FillRegion(bFondo, regionExterior);
                }
            }

            // contorno negro para que se vea
            using (var p = new Pen(Color.Black, 2))
                g.DrawPolygon(p, ptsScreen);
        }

        private void DibujarAreaCirculo(Graphics g, Area area)
        {
            // círculo siempre exclusión en tu lógica
            var centerScr = M2S(area.Centro);
            float rPx = area.Radio * PixelsPerMeter;
            var rect = new RectangleF(centerScr.X - rPx, centerScr.Y - rPx, rPx * 2, rPx * 2);

            using (Brush b = new SolidBrush(Color.FromArgb(80, Color.LightCoral)))
                g.FillEllipse(b, rect);

            using (var p = new Pen(Color.Black, 2))
                g.DrawEllipse(p, rect);
        }

        private void DrawAreas(Graphics g)
        {
            if (ActiveLayer == null) return;

            // 🔸 1. Dibujar el área de inclusión (zona válida)
            var inclusion = ActiveLayer.Areas.FirstOrDefault(a => !a.EsExclusion);
            if (inclusion != null && inclusion.Vertices.Count >= 3)
            {
                using (GraphicsPath path = new GraphicsPath())
                {
                    var pts = inclusion.Vertices.Select(M2S).ToArray();
                    path.AddPolygon(pts);

                    // Crear una región que cubre TODO el canvas
                    using (Region region = new Region(new Rectangle(0, 0, Width, Height)))
                    {
                        // Restar el área válida (de inclusión)
                        region.Exclude(path);

                        // Pintar todo lo que está fuera del polígono de rojo transparente
                        using (SolidBrush brush = new SolidBrush(Color.FromArgb(80, Color.Red)))
                        {
                            g.FillRegion(brush, region);
                        }

                        // Borde del área de inclusión
                        g.DrawPolygon(Pens.DarkGreen, pts);
                    }
                }
            }

            // 🔸 2. Dibujar áreas de exclusión internas (obstáculos)
            foreach (var area in ActiveLayer.Areas.Where(a => a.EsExclusion))
            {
                if (area.Vertices.Count < 3) continue;

                var pts = area.Vertices.Select(M2S).ToArray();
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(120, Color.DarkRed)))
                {
                    g.FillPolygon(brush, pts);
                }
                g.DrawPolygon(Pens.Black, pts);
            }
        }
        private void DrawStartPoints(Graphics g)
        {
            using var brush = new SolidBrush(Color.DeepSkyBlue);
            using var pen = new Pen(Color.Black, 1.5f);
            using var font = new Font("Arial", 7, FontStyle.Bold);
            var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };

            foreach (var (pos, heading) in StartPoints)
            {
                var P = M2S(pos);
                float size = 16f;

                // Triángulo base apuntando hacia arriba
                PointF[] tri = new PointF[]
                {
            new PointF(0, -size),
            new PointF(-size / 2f, size / 2f),
            new PointF(size / 2f, size / 2f)
                };

                // Aplicar rotación y traslación
                using (var m = new System.Drawing.Drawing2D.Matrix())
                {
                    m.Rotate(heading); // gira el triángulo en grados
                    m.Translate(P.X, P.Y, System.Drawing.Drawing2D.MatrixOrder.Append);
                    m.TransformPoints(tri);
                }

                // Dibuja triángulo
                g.FillPolygon(brush, tri);
                g.DrawPolygon(pen, tri);

                // Dibuja texto del heading
                string texto = $"{heading:0}°";
                g.DrawString(texto, font, Brushes.Black, P.X + 10, P.Y, sf);
            }
        }

        private void DrawGrid(Graphics g)
        {
            using var p = new Pen(Color.Gainsboro, 1);
            float step = PixelsPerMeter;
            var startX = _origin.X % step;
            var startY = _origin.Y % step;

            for (float x = startX; x < Width; x += step)
                g.DrawLine(p, x, 0, x, Height);
            for (float y = startY; y < Height; y += step)
                g.DrawLine(p, 0, y, Width, y);
        }

        private void DrawAxes(Graphics g)
        {
            using var axis = new Pen(Color.Black, 2);

            // Eje X
            g.DrawLine(axis, 0, _origin.Y, Width, _origin.Y);
            // Eje Y
            g.DrawLine(axis, _origin.X, 0, _origin.X, Height);

            using var f = new Font(FontFamily.GenericSansSerif, 8);
            using var b = new SolidBrush(Color.Black);

            for (int i = -100; i <= 100; i++)
            {
                var x = _origin.X + i * PixelsPerMeter;
                var y = _origin.Y + i * PixelsPerMeter;

                if (x >= 0 && x <= Width)
                {
                    g.DrawLine(Pens.Black, x, _origin.Y - 3, x, _origin.Y + 3);
                    if (i != 0) g.DrawString(i.ToString(), f, b, x + 2, _origin.Y + 4);
                }
                if (y >= 0 && y <= Height)
                {
                    g.DrawLine(Pens.Black, _origin.X - 3, y, _origin.X + 3, y);
                    if (i != 0) g.DrawString(i.ToString(), f, b, _origin.X + 4, y + 2);
                }
            }
        }

        private void DrawPolygons(Graphics g, List<List<PointF>> polygons)
        {
            using var p = new Pen(Color.Black, 2);

            foreach (var poly in polygons)
            {
                if (poly.Count < 2) continue;

                for (int i = 0; i < poly.Count - 1; i++)
                {
                    var a = M2S(poly[i]);
                    var b = M2S(poly[i + 1]);
                    g.DrawLine(p, a, b);
                    DrawSegmentLength(g, a, b);
                }

                // cerrar si último ≠ primero
                var first = M2S(poly[0]);
                var last = M2S(poly[poly.Count - 1]);
                if (Distance(first, last) > 1.0)
                {
                    g.DrawLine(p, last, first);
                    DrawSegmentLength(g, last, first);
                }
            }
        }

        private void DrawSegmentLength(Graphics g, PointF a, PointF b)
        {
            var mid = new PointF((a.X + b.X) / 2f, (a.Y + b.Y) / 2f);
            var lenMeters = Distance(a, b) / PixelsPerMeter;

            using var f = new Font(FontFamily.GenericSansSerif, 8);
            var text = $"{lenMeters:0.00} m";
            var sz = g.MeasureString(text, f);

            g.FillRectangle(Brushes.White, mid.X - sz.Width / 2, mid.Y - sz.Height / 2, sz.Width, sz.Height);
            g.DrawString(text, f, Brushes.Black, mid.X - sz.Width / 2, mid.Y - sz.Height / 2);
        }

        private void DrawCircles(Graphics g, List<(PointF center, float r)> circles)
        {
            using var p = new Pen(Color.Black, 2);
            using var f = new Font(FontFamily.GenericSansSerif, 8);

            foreach (var (c, r) in circles)
            {
                var C = M2S(c);
                var Rpx = r * PixelsPerMeter;
                g.DrawEllipse(p, C.X - Rpx, C.Y - Rpx, Rpx * 2, Rpx * 2);

                var label = $"{r:0.00} m";
                g.DrawString(label, f, Brushes.Black, C.X + 4, C.Y + 4);
            }
        }

        private static float Distance(PointF a, PointF b)
        {
            var dx = a.X - b.X; var dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private void MetricCanvas_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (e.Delta > 0) PixelsPerMeter = Math.Min(400, PixelsPerMeter * 1.1f);
            else PixelsPerMeter = Math.Max(10, PixelsPerMeter / 1.1f);

            var before = S2M(e.Location);
            var afterS = M2S(before);
            _origin.X += (e.Location.X - afterS.X);
            _origin.Y += (e.Location.Y - afterS.Y);
            Invalidate();
        }

        private void MetricCanvas_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                _panning = true;
                _panStart = e.Location;
            }
        }

        private void MetricCanvas_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_panning && e.Button == MouseButtons.Right)
            {
                var dx = e.X - _panStart.X;
                var dy = e.Y - _panStart.Y;
                _origin.X += dx;
                _origin.Y += dy;
                _panStart = e.Location;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            var clickM = S2M(e.Location); // click en METROS

            if (!ColocarWaypoint && e.Button == MouseButtons.Left)
            {
                foreach (var wp in waypoints)
                {
                    float dx = wp.x - clickM.X;
                    float dy = wp.y - clickM.Y;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                    if (dist < 0.3f) // 30 cm de tolerancia
                    {
                        WaypointClick?.Invoke(this, wp);
                        return;
                    }
                }
            }

            if (!ColocarWaypoint && e.Button == MouseButtons.Right)
            {
                foreach (var wp in waypoints)
                {
                    float dx = wp.x - clickM.X;
                    float dy = wp.y - clickM.Y;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                    if (dist < 0.3f) // 30 cm de tolerancia
                    {
                        WaypointRightClick?.Invoke(this, wp);
                        return;
                    }
                }
            }


            if (ColocarWaypoint)
            {
                var m = S2M(e.Location);
                PuntoWaypointSeleccionado?.Invoke(this, (m.X, m.Y));
            }


            if (!SeleccionarPuntoInicio) return;
            if (StartPoints.Count == 0) return;


            var click = e.Location;
            foreach (var (pos, heading) in StartPoints)
            {
                var P = M2S(pos);
                float distancia = (float)Math.Sqrt(Math.Pow(P.X - click.X, 2) + Math.Pow(P.Y - click.Y, 2));
                if (distancia < 15) // rango de clic en píxeles
                {
                    PuntoInicioSeleccionado?.Invoke(pos, heading);

                    StartPoints.Clear();

                    Invalidate();
                    break;
                }
            }

        }

        private void DrawDron(Graphics g)
        {
            if (DronPosicionM == null) return;

            var p = M2S(DronPosicionM.Value);
            int iconSize = 40;

            if (DronIcon != null)
            {
                using var rotated = new Bitmap(iconSize, iconSize);
                using (var g2 = Graphics.FromImage(rotated))
                {
                    g2.TranslateTransform(iconSize / 2f, iconSize / 2f);
                    g2.RotateTransform(DronHeading);
                    g2.TranslateTransform(-iconSize / 2f, -iconSize / 2f);
                    g2.DrawImage(DronIcon, 0, 0, iconSize, iconSize);
                }
                g.DrawImage(rotated, p.X - iconSize / 2, p.Y - iconSize / 2);
            }
            else
            {
                float size = 10;
                g.FillEllipse(Brushes.Red, p.X - size / 2, p.Y - size / 2, size, size);
                g.DrawEllipse(Pens.Black, p.X - size / 2, p.Y - size / 2, size, size);
            }

            double headingRad = DronHeading * Math.PI / 180.0;
            float longitudLinea = 50f;
            float x2 = p.X + (float)(Math.Sin(headingRad) * longitudLinea);
            float y2 = p.Y - (float)(Math.Cos(headingRad) * longitudLinea);

            using var pen = new Pen(Color.Red, 2);
            g.DrawLine(pen, p, new PointF(x2, y2));
        }


        public void DibujarWaypoint(Graphics g, WaypointPlan wp, int waypointIndex)
        {
            var p = M2S(new PointF(wp.x, wp.y));

            Color color;

            if (waypointIndex <= _ultimoWaypointAlcanzado)
                color = Color.Green;        // ✔ ya sobrevolado
            else
                color = Color.Red;

            using var brush = new SolidBrush(color);

            g.FillEllipse(brush, p.X - 5, p.Y - 5, 10, 10);
            g.DrawEllipse(Pens.Black, p.X - 5, p.Y - 5, 10, 10);

            string texto = $"WP {waypointIndex + 1} · {wp.altura} m";
            g.DrawString(texto, new Font("Arial", 8), Brushes.Black, p.X + 10, p.Y - 5);

            // 🔹 Dibujar acciones (iconos)
            DibujarAccionesWaypoint(g, wp, p);
        }

        private void DibujarAccionesWaypoint(Graphics g, WaypointPlan wp, PointF screenPos)
        {
            if (wp.Actions == null || wp.Actions.Count == 0)
                return;

            float offsetX = 0;

            foreach (var action in wp.Actions)
            {
                string icon = action.Type switch
                {
                    WaypointActionType.TakePhoto => "📷",
                    WaypointActionType.StartVideo => "🎥▶",
                    WaypointActionType.StopVideo => "⏹",
                    _ => "?"
                };

                using var font = new Font("Segoe UI Emoji", 10);
                g.DrawString(icon, font, Brushes.Black,
                    screenPos.X + offsetX,
                    screenPos.Y - 20);

                offsetX += 18;
            }
        }

        public void DibujarLineaEntreWaypoints(Graphics g, List<WaypointPlan> waypoints)
        {
            if (DronPosicionM == null || waypoints.Count == 0) return;

            using var pen = new Pen(Color.Green, 2);

            // Línea dron → primer waypoint
            //var dronP = M2S(DronPosicionM.Value);
            var first = M2S(new PointF(waypoints[0].x, waypoints[0].y));
            //g.DrawLine(pen, dronP, first);

            if (_inicioPlanM != null)
            {
                var inicioS = M2S(_inicioPlanM.Value);
                g.DrawLine(pen, inicioS.X, inicioS.Y, first.X, first.Y);
            }

            // Líneas entre waypoints
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                var a = M2S(new PointF(waypoints[i].x, waypoints[i].y));
                var b = M2S(new PointF(waypoints[i + 1].x, waypoints[i + 1].y));
                g.DrawLine(pen, a, b);
            }
        }

        public void EstablecerModoColocarWaypoint(bool activar)
        {
            ColocarWaypoint = activar;
            ColocarWaypointCambiado?.Invoke(this, ColocarWaypoint);
        }

        public void ActualizarWaypoints(List<WaypointPlan> waypoints)
        {
            this.waypoints = waypoints;
            Invalidate();

        }

        public void LimpiarPuntosInicio()
        {
            StartPoints.Clear();
            SeleccionarPuntoInicio = false;
            Invalidate();
        }

        public void FijarInicioPlan(PointF inicioM)
        {
            _inicioPlanM = inicioM;
            Invalidate();
        }

        public void LimpiarInicioPlan()
        {
            _inicioPlanM = null;
            Invalidate();
        }

        public void MarcarWaypointAlcanzado(int index)
        {
            _ultimoWaypointAlcanzado = index;
            Invalidate();
        }

        

    }
}
