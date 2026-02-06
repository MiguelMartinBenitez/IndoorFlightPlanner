using GMap.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace csDronLink2
{
    public partial class Dron
    {
        public void EstableceEscenarioInterior(List<List<(float x, float y)>> scenario)
        {
            // Primero obtenemos el escenario de inclusión (polígono principal)
            List<(float x, float y)> waypoints = scenario[0];
            List<MAVLink.mavlink_mission_item_int_t> wploader = new List<MAVLink.mavlink_mission_item_int_t>();
            int seq = 0;

            // Convertimos las coordenadas locales de los waypoints a MAVLink
            foreach (var wp in waypoints)
            {
                wploader.Add(new MAVLink.mavlink_mission_item_int_t()
                {
                    target_system = 1,
                    target_component = 1,
                    seq = (ushort)seq,
                    frame = (byte)MAVLink.MAV_FRAME.LOCAL_NED,  // Cambiado a LOCAL_NED para coordenadas locales
                    command = (ushort)MAVLink.MAV_CMD.FENCE_POLYGON_VERTEX_INCLUSION,
                    param1 = waypoints.Count,
                    x = (int)(wp.x * 1e3), // Convertir a milímetros
                    y = (int)(wp.y * 1e3), // Convertir a milímetros
                    mission_type = (byte)MAVLink.MAV_MISSION_TYPE.FENCE
                });
                seq++;
            }

            // Ahora preparamos los obstáculos (círculos o polígonos de exclusión)
            for (int i = 1; i < scenario.Count; i++)
            {
                waypoints = scenario[i];
                if (waypoints.Count == 2) // Si es un círculo
                {
                    wploader.Add(new MAVLink.mavlink_mission_item_int_t()
                    {
                        target_system = 1,
                        target_component = 1,
                        seq = (ushort)seq,
                        frame = (byte)MAVLink.MAV_FRAME.LOCAL_NED,  // Cambiado a LOCAL_NED para coordenadas locales
                        command = (ushort)MAVLink.MAV_CMD.FENCE_CIRCLE_EXCLUSION,
                        param1 = Convert.ToSingle(waypoints[1].x), // El radio del círculo (en metros)
                        x = (int)(waypoints[0].x * 1e3), // Convertir a milímetros
                        y = (int)(waypoints[0].y * 1e3), // Convertir a milímetros
                        mission_type = (byte)MAVLink.MAV_MISSION_TYPE.FENCE
                    });
                    seq++;
                }
                else // Si es un polígono
                {
                    foreach (var wp in waypoints)
                    {
                        wploader.Add(new MAVLink.mavlink_mission_item_int_t()
                        {
                            target_system = 1,
                            target_component = 1,
                            seq = (ushort)seq,
                            frame = (byte)MAVLink.MAV_FRAME.LOCAL_NED,  // Cambiado a LOCAL_NED para coordenadas locales
                            command = (ushort)MAVLink.MAV_CMD.FENCE_POLYGON_VERTEX_EXCLUSION,
                            param1 = waypoints.Count,
                            x = (int)(wp.x * 1e3), // Convertir a milímetros
                            y = (int)(wp.y * 1e3), // Convertir a milímetros
                            mission_type = (byte)MAVLink.MAV_MISSION_TYPE.FENCE
                        });
                        seq++;
                    }
                }
            }

            // Enviar el número de waypoints al autopiloto
            var msg = new MAVLink.mavlink_mission_count_t
            {
                target_system = 1,
                target_component = 1,
                count = (ushort)wploader.Count,
                mission_type = (byte)MAVLink.MAV_MISSION_TYPE.FENCE
            };

            byte[] packet = mavlink.GenerateMAVLinkPacket10(MAVLink.MAVLINK_MSG_ID.MISSION_COUNT, msg);
            EnviarMensaje(packet);

            // Enviar los waypoints del escenario
            string msgType;
            while (true)
            {
                msgType = ((int)MAVLink.MAVLINK_MSG_ID.MISSION_REQUEST).ToString();
                MAVLink.MAVLinkMessage request = messageHandler.WaitForMessageBlock(msgType, timeout: -1);
                int next = ((MAVLink.mavlink_mission_request_t)request.data).seq;

                MAVLink.mavlink_mission_item_int_t msg2 = wploader[next];
                packet = mavlink.GenerateMAVLinkPacket10(MAVLink.MAVLINK_MSG_ID.MISSION_ITEM_INT, msg2);
                EnviarMensaje(packet);

                if (next == wploader.Count - 1) break; // Ya los he enviado todos
            }

            // Esperar confirmación de recepción completa
            msgType = ((int)MAVLink.MAVLINK_MSG_ID.MISSION_ACK).ToString();
            MAVLink.MAVLinkMessage response = messageHandler.WaitForMessageBlock(msgType, timeout: -1);
        }
    }
}

