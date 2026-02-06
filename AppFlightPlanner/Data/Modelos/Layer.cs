using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace AppFlightPlanner.Data.Modelos
{
    public class Layer
    {
        public string Nombre { get; set; } = "";
        public float AlturaInicio { get; set; } = 0f;
        public float AlturaFin { get; set; } = 0f;

        public List<List<PointF>> Polygons { get; set; } = new List<List<PointF>>();
        public List<(PointF center, float r)> Circles { get; set; } = new List<(PointF center, float r)>();

        public override string ToString() => $"{Nombre} — {AlturaInicio:0.0}-{AlturaFin:0.0} m";
        public List<Area> Areas { get; set; } = new List<Area>();
    }

    public class Area
    {
        public string Tipo { get; set; } = "polygon";

        // Para polígonos
        public List<PointF> Vertices { get; set; } = new List<PointF>();

        // Para círculos
        public PointF Centro { get; set; }
        public float Radio { get; set; }
        public bool EsExclusion { get; set; } // true = exclusión, false = inclusión
    }
}
