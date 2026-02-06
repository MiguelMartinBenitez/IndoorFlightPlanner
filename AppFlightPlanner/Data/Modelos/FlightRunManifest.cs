using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppFlightPlanner.Data.Modelos
{
    public class FlightRunManifest
    {
        public int SpaceId { get; set; }
        public string SpaceName { get; set; }

        public int PlanId { get; set; }
        public string PlanName { get; set; }

        public DateTime StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }

        public string FolderPath { get; set; }

        public List<FlightRunWaypoint> Waypoints { get; set; } = new List<FlightRunWaypoint>();
    }
}
