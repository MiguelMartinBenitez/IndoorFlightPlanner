using AppFlightPlanner.Data.Modelos;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;



namespace AppFlightPlanner.Data.Repositorios
{
    public class RepoEspacios
    {
        // ==== MODELO COMPLETO DEL ESPACIO ====
        public class SpaceRow
        {
            public int Id { get; }
            public string Nombre { get; }
            public List<Layer> Layers { get; }
            public List<StartPoint> StartPoints { get; }
            public List<PlanDeVuelo> FlightPlans { get; }
            public DateTime CreatedAt { get; }

            public SpaceRow(
                int id,
                string nombre,
                List<Layer> layers,
                List<StartPoint> startPoints,
                List<PlanDeVuelo> flightPlans,
                DateTime createdAt)
            {
                Id = id;
                Nombre = nombre;
                Layers = layers;
                StartPoints = startPoints;
                FlightPlans = flightPlans;
                CreatedAt = createdAt;
            }
        }


        // ============================================================
        //  INSERTAR UN NUEVO ESPACIO
        // ============================================================
        public int Insert(string nombre, List<Layer> layers, List<StartPoint> startPoints)
        {
            using (var cn = BaseDatos.Open())
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
INSERT INTO Spaces (Nombre, LayersJson, StartPointsJson, FlightPlansJson, CreatedAt)
VALUES (@nombre, @layers, @startPoints, @plans, @createdAt);
SELECT last_insert_rowid();
";

                cmd.Parameters.AddWithValue("@nombre", nombre);
                cmd.Parameters.AddWithValue("@layers", JsonConvert.SerializeObject(layers));
                cmd.Parameters.AddWithValue("@startPoints",
                    JsonConvert.SerializeObject(startPoints ?? new List<StartPoint>()));
                cmd.Parameters.AddWithValue("@plans",
                    JsonConvert.SerializeObject(new List<PlanDeVuelo>()));
                cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("o"));

                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        // ============================================================
        //  OBTENER TODOS LOS ESPACIOS
        // ============================================================
        public List<SpaceRow> GetAll()
        {
            var list = new List<SpaceRow>();

            using (var cn = BaseDatos.Open())
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT Id, Nombre, LayersJson, StartPointsJson, FlightPlansJson, CreatedAt FROM Spaces;";
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        string layersJson = rd["LayersJson"].ToString();
                        string startPointsJson = rd["StartPointsJson"].ToString();
                        string flightPlansJson = rd["FlightPlansJson"].ToString();

                        var layers = JsonConvert.DeserializeObject<List<Layer>>(layersJson)
                                     ?? new List<Layer>();
                        var startPoints = JsonConvert.DeserializeObject<List<StartPoint>>(startPointsJson)
                                          ?? new List<StartPoint>();
                        var flightPlans = JsonConvert.DeserializeObject<List<PlanDeVuelo>>(flightPlansJson)
                                          ?? new List<PlanDeVuelo>();

                        list.Add(new SpaceRow(
                            rd.GetInt32(0),
                            rd.GetString(1),
                            layers,
                            startPoints,
                            flightPlans,
                            DateTime.Parse(rd.GetString(5))
                        ));
                    }
                }
            }

            return list;
        }

        // ============================================================
        //  OBTENER ESPACIO POR ID
        // ============================================================
        public SpaceRow GetById(int id)
        {
            using (var cn = BaseDatos.Open())
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT Id, Nombre, LayersJson, StartPointsJson, FlightPlansJson, CreatedAt FROM Spaces WHERE Id=@id;";
                cmd.Parameters.AddWithValue("@id", id);

                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read())
                        return null;

                    var layers = JsonConvert.DeserializeObject<List<Layer>>(rd["LayersJson"].ToString())
                                 ?? new List<Layer>();
                    var startPoints =
                        JsonConvert.DeserializeObject<List<StartPoint>>(rd["StartPointsJson"].ToString())
                        ?? new List<StartPoint>();
                    var flightPlans =
                        JsonConvert.DeserializeObject<List<PlanDeVuelo>>(rd["FlightPlansJson"].ToString())
                        ?? new List<PlanDeVuelo>();

                    return new SpaceRow(
                        rd.GetInt32(0),
                        rd.GetString(1),
                        layers,
                        startPoints,
                        flightPlans,
                        DateTime.Parse(rd.GetString(5))
                    );
                }
            }
        }

        // ============================================================
        //  ACTUALIZAR PLANES DE VUELO
        // ============================================================
        public void UpdatePlanes(int id, List<PlanDeVuelo> planes)
        {
            using (var cn = BaseDatos.Open())
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
UPDATE Spaces
SET FlightPlansJson = @plans
WHERE Id = @id;
";

                cmd.Parameters.AddWithValue("@plans", JsonConvert.SerializeObject(planes));
                cmd.Parameters.AddWithValue("@id", id);

                cmd.ExecuteNonQuery();
            }
        }
    }


}
