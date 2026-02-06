using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

//SIRVE PARA COMPROBAR CONEXIÓN

namespace csDronLink2
{
    public class MiJoystick
    {
        Joystick joystick;
        bool joysticActivo = false;
        public event Action<int> ButtonDown;
        public int Conectar()
        {
            // Inicializa DirectInput
            var directInput = new DirectInput();

            // Busca dispositivos tipo Joystick
            var joystickGuid = Guid.Empty;
            foreach (var deviceInstance in directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices))
                joystickGuid = deviceInstance.InstanceGuid;

            // Si no encontró Gamepad, prueba con Joystick
            if (joystickGuid == Guid.Empty)
            {
                foreach (var deviceInstance in directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AllDevices))
                    joystickGuid = deviceInstance.InstanceGuid;
            }

            if (joystickGuid == Guid.Empty)
            {
                Console.WriteLine("No se encontró ningún joystick conectado.");
                //Console.ReadKey();
                return 0;
            }

            // Crea el joystick
            joystick = new Joystick(directInput, joystickGuid);

            Console.WriteLine($"Usando joystick: {joystick.Information.ProductName}");

            
            return 1;
        }




        










        
        

    }
}
