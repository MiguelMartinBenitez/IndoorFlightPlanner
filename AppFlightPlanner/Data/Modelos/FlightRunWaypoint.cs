using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppFlightPlanner.Data.Modelos
{
    public class FlightRunWaypoint
    {
        public int Index { get; set; }                // índice del waypoint en el plan
        public float X { get; set; }
        public float Y { get; set; }
        public float Altura { get; set; }

        public List<WaypointRunAction> Actions { get; set; } = new List<WaypointRunAction>();
    }
}
