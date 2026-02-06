using AForge.Video;
using AForge.Video.DirectShow;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using Accord.Video.FFMPEG;

namespace csDronLink2
{
    public class Camara
    {
        private VideoCaptureDevice _device;
        private Bitmap _lastValidFrame;
        private int _frameCount = 0;
        private AutoResetEvent _frameReady = new AutoResetEvent(false);
        private VideoFileWriter _videoWriter;
        private bool _grabandoVideo = false;
        private string _videoFilePath;

        private VideoCaptureDevice SeleccionarCamara()
        {
            var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

            if (devices.Count == 0)
                throw new Exception("No se ha detectado ninguna cámara.");

            foreach (FilterInfo d in devices)
            {
                string name = d.Name.ToLower();

                if (!name.Contains("nvidia") &&
                    !name.Contains("virtual") &&
                    !name.Contains("broadcast") &&
                    !name.Contains("obs"))
                {
                    return new VideoCaptureDevice(d.MonikerString);
                }
            }

            return new VideoCaptureDevice(devices[0].MonikerString);
        }
        public bool StartCamera()
        {
            var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (devices.Count == 0)
                return false;

            _device = SeleccionarCamara();

            if (_device.VideoCapabilities.Length > 0)
            {
                _device.VideoResolution = _device.VideoCapabilities[0];
            }

            _device.NewFrame += OnNewFrame;
            _device.Start();

            

            return true;
        }

        private void OnNewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            _frameCount++;

            if (_frameCount < 10)
                return;

            Bitmap frame = ConvertToRgb(eventArgs.Frame);

            if (IsFrameBlack(frame))
            {
                frame.Dispose();
                return;
            }

            _lastValidFrame?.Dispose();
            _lastValidFrame = (Bitmap)frame.Clone();
            _frameReady.Set();

            if (_grabandoVideo && _videoWriter != null && _videoWriter.IsOpen)
            {
                _videoWriter.WriteVideoFrame(frame);
            }

            frame.Dispose();
        }

        private Bitmap ConvertToRgb(Bitmap source)
        {
            Bitmap rgb = new Bitmap(
                source.Width,
                source.Height,
                PixelFormat.Format24bppRgb
            );

            using (Graphics g = Graphics.FromImage(rgb))
            {
                g.DrawImage(source, 0, 0, source.Width, source.Height);
            }

            return rgb;
        }

        private bool IsFrameBlack(Bitmap bmp)
        {
            Color c = bmp.GetPixel(bmp.Width / 2, bmp.Height / 2);
            return c.R < 10 && c.G < 10 && c.B < 10;
        }

        public string TakePhoto(string folder)
        {
            if (_device == null || !_device.IsRunning)
                throw new Exception("Cámara no iniciada");

            if (!_frameReady.WaitOne(5000))
                throw new Exception("No se recibió ningún frame válido de la cámara");

            Directory.CreateDirectory(folder);

            string file = Path.Combine(
                folder,
                $"foto_{DateTime.Now:yyyyMMdd_HHmmss}.jpg"
            );

            _lastValidFrame.Save(file, ImageFormat.Jpeg);

            return file;
        }

        public string StartVideo(string folder)
        {
            if (_device == null || !_device.IsRunning)
                throw new Exception("Cámara no iniciada");

            if (_grabandoVideo)
                throw new Exception("El vídeo ya está en grabación");

            Directory.CreateDirectory(folder);

            _videoFilePath = Path.Combine(
                folder,
                $"video_{DateTime.Now:yyyyMMdd_HHmmss}.avi"
            );

            _videoWriter = new VideoFileWriter();

            var cap = _device.VideoResolution;

            _videoWriter.Open(
                _videoFilePath,
                cap.FrameSize.Width,
                cap.FrameSize.Height,
                25, // FPS
                VideoCodec.MPEG4
            );

            _grabandoVideo = true;

            return _videoFilePath;
        }

        public void StopVideo()
        {
            if (!_grabandoVideo)
                return;

            _grabandoVideo = false;

            if (_videoWriter != null)
            {
                _videoWriter.Close();
                _videoWriter.Dispose();
                _videoWriter = null;
            }
        }

        public void StopCamera()
        {
            if (_device != null && _device.IsRunning)
            {
                _device.SignalToStop();
                _device.WaitForStop();
            }

            _device = null;
            _lastValidFrame = null;
            _frameCount = 0;
        }
    }
}
