using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppFlightPlanner
{
    internal static class Program
    {
        /// <summary>
        /// Punto de entrada principal para la aplicación.
        /// </summary>
        [STAThread]
        static void Main()
        {
            //Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(false);
            //Application.Run(new StartForm());

            DebugConsole.Show(); // <-- AQUI
            DebugConsole.Log("App arrancada");

            // Capturar excepciones y verlas en consola
            Application.ThreadException += (s, e) =>
                DebugConsole.LogEx("UI ThreadException", e.Exception);

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                DebugConsole.Log("UnhandledException: " + (e.ExceptionObject?.ToString() ?? "null"));

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new StartForm());
        }
    }
}
