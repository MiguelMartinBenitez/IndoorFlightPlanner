using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AppFlightPlanner
{
    public static class DebugConsole
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetConsoleWindow();

        private static bool _shown;

        public static void Show()
        {
            if (_shown) return;

            // Si ya existe consola (por ejemplo si arrancas desde cmd), no crees otra
            if (GetConsoleWindow() == IntPtr.Zero)
            {
                AllocConsole();
            }

            _shown = true;

            // Asegura que Console.WriteLine funciona bien
            try
            {
                var stdout = Console.OpenStandardOutput();
                var writer = new StreamWriter(stdout) { AutoFlush = true };
                Console.SetOut(writer);

                var stderr = Console.OpenStandardError();
                var errWriter = new StreamWriter(stderr) { AutoFlush = true };
                Console.SetError(errWriter);
            }
            catch { /* no petar por esto */ }

            // Redirigir Debug.WriteLine a la consola
            Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Debug.AutoFlush = true;

            Console.WriteLine("=== DebugConsole ON ===");
        }

        public static void Hide()
        {
            if (!_shown) return;
            _shown = false;
            try { FreeConsole(); } catch { }
        }

        public static void Log(string msg)
        {
            if (!_shown) return;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");
        }

        public static void LogEx(string title, Exception ex)
        {
            if (!_shown) return;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {title}");
            Console.WriteLine(ex.ToString());
        }
    }
}
