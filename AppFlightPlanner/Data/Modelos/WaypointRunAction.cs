using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppFlightPlanner.Data.Modelos
{
    public class WaypointRunAction
    {
        public WaypointActionType Type { get; set; }
        public float? Heading { get; set; }

        public string OutputPath { get; set; }
    }
}
