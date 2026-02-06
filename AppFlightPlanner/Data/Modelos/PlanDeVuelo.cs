using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppFlightPlanner.Data.Modelos
{
    public class PlanDeVuelo
    {
        public string Nombre { get; set; }
        public DateTime Fecha { get; set; }
        public List<WaypointPlan> Waypoints { get; set; } = new List<WaypointPlan>();
        public float x { get; set; }
        public float y { get; set; }
        public float heading { get; set; }
        public float AlturaDespegue { get; set; }

    }
}
