using System;
using System.IO;
using System.Data.SQLite;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppFlightPlanner.Data
{
    public static class BaseDatos
    {
        public static string DbFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DroneOps");
        public static string DbPath => Path.Combine(DbFolder, "AppDb.sqlite");

        public static SQLiteConnection Open()
        {
            Directory.CreateDirectory(DbFolder);
            bool firstTime = !File.Exists(DbPath);

            var cn = new SQLiteConnection($"Data Source={DbPath};Version=3;");
            cn.Open();

            if (firstTime) CreateSchema(cn);
            return cn;
        }

        public static void EnsureCreated()
        {
            using var _ = Open();
        }

        private static void CreateSchema(SQLiteConnection cn)
        {
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Spaces(
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Nombre TEXT NOT NULL,
  LayersJson TEXT NOT NULL,  -- guarda todas las capas con sus rangos, polígonos y círculos
  StartPointsJson TEXT,
  FlightPlansJson TEXT NOT NULL,
  CreatedAt TEXT NOT NULL
);
";
                cmd.ExecuteNonQuery();
            }
        }
    }
}
