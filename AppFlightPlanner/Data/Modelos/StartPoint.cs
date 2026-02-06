using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppFlightPlanner.Data.Modelos
{
    public class StartPoint
    {
        public float x { get; set; }
        public float y { get; set; }
        public float heading { get; set; } // grados 0–360

        public StartPoint() { }
        public StartPoint(float x, float y, float heading)
        {
            this.x = x; this.y = y; this.heading = heading;
        }
    }
}
