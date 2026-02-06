using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppFlightPlanner.Data.Modelos
{
    public class WaypointPlan
    {
        public float x { get; set; }      // metros (sala)
        public float y { get; set; }
        public float altura { get; set; }

        public List<WaypointAction> Actions { get; set; } = new List<WaypointAction>();
    }
}
