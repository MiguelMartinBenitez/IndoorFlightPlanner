using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MAVLink;
using System.Threading;
using System.Collections.Concurrent;
using System.Net.Sockets;

// MIRAR ESTO. SI LO QUITO NO RECONOCE LA CLASE WaitingRequest
using static csDronLink2.MessageHandler;
using System.IO;
using System.Data.Entity.Core.Metadata.Edm;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.InteropServices;
using System.Data;
using GMap.NET.MapProviders;



namespace csDronLink2
{
    public partial class Dron
    {
        byte id;
        MAVLink.MavlinkParse mavlink = new MAVLink.MavlinkParse();
        string modo;

        SerialPort puertoSerie;
        NetworkStream puertoTCP;

        byte[] navPacket;

        float relative_alt;
        float lat;
        float lon;
        float heading;

        float localX;
        float localY;
        float localZ;
        float velX;
        float velY;
        float velZ;


        Action<List<(string nombre, float valor)>> ProcesarTelemetria = null;

        Boolean navegando = false;
        int velocidad = 1;

        MessageHandler messageHandler;

        public Dron(byte id = 1)
        {
            this.id = id;
        }
        public byte GetId()
        {
            return this.id;
        }

        public void Armar()
        {
            MAVLink.mavlink_command_long_t req = new MAVLink.mavlink_command_long_t();
            req.target_system = 1;
            req.target_component = 1;
            req.command = (ushort)MAVLink.MAV_CMD.COMPONENT_ARM_DISARM;
            req.param1 = 1;
            byte[] packet = mavlink.GenerateMAVLinkPacket10(MAVLink.MAVLINK_MSG_ID.COMMAND_LONG, req);
            EnviarMensaje(packet);
        }


        private void EnviarMensaje(byte[] packet)
        {
            if (modo == "produccion")
                puertoSerie.Write(packet, 0, packet.Length);
            else
                puertoTCP.Write(packet, 0, packet.Length);
        }

        private void RegistrarTelemetria(MAVLinkMessage msg)
        {
            MAVLink.mavlink_global_position_int_t position = (MAVLink.mavlink_global_position_int_t)msg.data;
            this.relative_alt = position.relative_alt / 1000.0f; // mm → m
            this.lat = position.lat;
            this.lon = position.lon;
            this.heading = position.hdg;
            EnviarTelemetriaCombinada();

        }



        private void RegistrarTelemetriaLocal(MAVLinkMessage msg)
        {
            MAVLink.mavlink_local_position_ned_t posLocal = (MAVLink.mavlink_local_position_ned_t)msg.data;

            this.localX = posLocal.x; // metros norte
            this.localY = posLocal.y; // metros este
            this.localZ = posLocal.z; // metros abajo
            this.velX = posLocal.vx;
            this.velY = posLocal.vy;
            this.velZ = posLocal.vz;

            EnviarTelemetriaCombinada();
        }

        private void EnviarTelemetriaCombinada()
        {
            if (ProcesarTelemetria == null) return;

            List<(string nombre, float valor)> telemetria = new List<(string nombre, float valor)>

            {
                ("Lat", lat),
                ("Lon", lon),
                ("Alt", relative_alt),
                ("Heading", heading),

                ("X_local", localX),
                ("Y_local", localY),
                ("Z_local", localZ),
                ("VX_local", velX),
                ("VY_local", velY),
                ("VZ_local", velZ)
            };

            ProcesarTelemetria(telemetria);
        }

        // --- Conexión ---
        public void Conectar(string modo, string conector = null)
        {
            this.modo = modo;

            if (modo == "produccion")
            {
                puertoSerie = new SerialPort
                {
                    PortName = conector,
                    BaudRate = 57600
                };
                puertoSerie.Open();
                messageHandler = new MessageHandler(modo, puertoSerie);
            }
            else
            {
                string ip = "127.0.0.1";
                int port = 5763;
                TcpClient client = new TcpClient(ip, port);
                puertoTCP = client.GetStream();
                messageHandler = new MessageHandler(modo, puertoTCP);
            }

            // Telemetría global
            string msgTypeGlobal = ((int)MAVLink.MAVLINK_MSG_ID.GLOBAL_POSITION_INT).ToString();
            messageHandler.RegisterHandler(msgTypeGlobal, RegistrarTelemetria);

            // Telemetría local
            string msgTypeLocal = ((int)MAVLink.MAVLINK_MSG_ID.LOCAL_POSITION_NED).ToString();
            messageHandler.RegisterHandler(msgTypeLocal, RegistrarTelemetriaLocal);


            EnviarPeticionMensaje((ushort)MAVLink.MAVLINK_MSG_ID.GLOBAL_POSITION_INT, 200000);


            EnviarPeticionMensaje((ushort)MAVLink.MAVLINK_MSG_ID.LOCAL_POSITION_NED, 200000);
        }

        private void EnviarPeticionMensaje(ushort msgId, int intervaloMicrosegundos)
        {
            MAVLink.mavlink_command_long_t req = new MAVLink.mavlink_command_long_t
            {
                target_system = 1,
                target_component = 1,
                command = (ushort)MAVLink.MAV_CMD.SET_MESSAGE_INTERVAL,
                param1 = msgId,
                param2 = intervaloMicrosegundos
            };

            byte[] packet = mavlink.GenerateMAVLinkPacket10(MAVLink.MAVLINK_MSG_ID.COMMAND_LONG, req);
            EnviarMensaje(packet);
        }
    }
}
