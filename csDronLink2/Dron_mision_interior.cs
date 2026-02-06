using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace csDronLink2
{
    public partial class Dron
    {
        private List<(float X, float Y)> misionNED = new List<(float X, float Y)>();
        private bool misionActiva = false;


        public void CargarMisionLocalNED(List<(float Xned, float Yned)> puntosNED)
        {
            misionNED = puntosNED;
        }



        private void IrAPuntoNED(float Xned, float Yned, float Zned)
        {
            var cmd = new MAVLink.mavlink_set_position_target_local_ned_t
            {
                target_system = 1,
                target_component = 1,

                coordinate_frame = (byte)MAVLink.MAV_FRAME.LOCAL_NED,

                type_mask = 0b_0000110111111000,

                x = Xned,
                y = Yned,
                z = Zned,

                vx = 0,
                vy = 0,
                vz = 0,
                afx = 0,
                afy = 0,
                afz = 0,
                yaw = 0,
                yaw_rate = 0
            };

            byte[] packet = mavlink.GenerateMAVLinkPacket10(
                MAVLink.MAVLINK_MSG_ID.SET_POSITION_TARGET_LOCAL_NED, cmd);

            EnviarMensaje(packet);
        }



        private void EsperarLlegadaNED(
            Func<(float X, float Y, float Z)> ObtenerPosicionNED,
            float destX, float destY,
            float tolerancia = 0.25f)
        {
            while (misionActiva)
            {
                var pos = ObtenerPosicionNED();

                float dx = pos.X - destX;
                float dy = pos.Y - destY;

                if (Math.Sqrt(dx * dx + dy * dy) <= tolerancia)
                    break;

                Thread.Sleep(100);
            }
        }



        public void EjecutarMisionLocalNED(
            Func<(float X, float Y, float Z)> ObtenerNED,
            float alturaNED)
        {
            if (misionNED.Count == 0)
                return;

            misionActiva = true;

            foreach (var wp in misionNED)
            {
                if (!misionActiva) break;

                IrAPuntoNED(wp.X, wp.Y, alturaNED);

                EsperarLlegadaNED(ObtenerNED, wp.X, wp.Y);
            }

            misionActiva = false;
        }



        public void DetenerMisionLocalNED()
        {
            misionActiva = false;
            Parar();
        }
    }
}
