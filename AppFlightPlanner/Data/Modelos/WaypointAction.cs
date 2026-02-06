using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppFlightPlanner.Data.Modelos
{
    public class WaypointAction
    {
        public WaypointActionType Type { get; set; }

        public float? Heading { get; set; }

        public float? Altitude { get; set; }    

        public string Label { get; set; }
        public string OutputPath { get; set; } 
        public DateTime? ExecutedAt { get; set; }
    }
}
