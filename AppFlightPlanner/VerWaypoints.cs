using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppFlightPlanner
{
    public partial class VerWaypoints : Form
    {
        private readonly List<string> _files;
        private int _index = 0;

        private PictureBox _pb;
        private Label _lbl;
        private Button _btnPrev, _btnNext, _btnOpenFolder, _btnClose;
        public VerWaypoints(List<string> files)
        {
            _files = files ?? throw new ArgumentNullException(nameof(files));
            if (_files.Count == 0) throw new ArgumentException("No hay archivos", nameof(files));

            Text = "Fotos del waypoint";
            StartPosition = FormStartPosition.CenterParent;
            Width = 900;
            Height = 650;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;

            _pb = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };

            _lbl = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleCenter
            };

            _btnPrev = new Button { Text = "← Anterior", Width = 110 };
            _btnNext = new Button { Text = "Siguiente →", Width = 110 };
            _btnOpenFolder = new Button { Text = "Abrir carpeta", Width = 120 };
            _btnClose = new Button { Text = "Cerrar", Width = 90, DialogResult = DialogResult.OK };

            _btnPrev.Click += (s, e) => { _index = (_index - 1 + _files.Count) % _files.Count; LoadCurrent(); };
            _btnNext.Click += (s, e) => { _index = (_index + 1) % _files.Count; LoadCurrent(); };
            _btnOpenFolder.Click += (s, e) => OpenFolder();
            _btnClose.Click += (s, e) => Close();

            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(10),
                WrapContents = false
            };

            panel.Controls.Add(_btnClose);
            panel.Controls.Add(_btnOpenFolder);
            panel.Controls.Add(_btnNext);
            panel.Controls.Add(_btnPrev);

            Controls.Add(_pb);
            Controls.Add(panel);
            Controls.Add(_lbl);

            LoadCurrent();
        }

        private void LoadCurrent()
        {
            string file = _files[_index];

            // liberar imagen anterior (evita lock del fichero)
            if (_pb.Image != null)
            {
                var old = _pb.Image;
                _pb.Image = null;
                old.Dispose();
            }

            // cargar copia en memoria para no bloquear el fichero
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var img = Image.FromStream(fs))
            {
                _pb.Image = new Bitmap(img);
            }

            _lbl.Text = $"{_index + 1}/{_files.Count} — {Path.GetFileName(file)}";
        }

        private void OpenFolder()
        {
            string file = _files[_index];
            string folder = Path.GetDirectoryName(file);

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return;

            try
            {
                // abrir el explorador y seleccionar el archivo
                Process.Start("explorer.exe", $"/select,\"{file}\"");
            }
            catch
            {
                // fallback: abrir solo la carpeta
                try { Process.Start("explorer.exe", folder); } catch { }
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_pb.Image != null)
            {
                _pb.Image.Dispose();
                _pb.Image = null;
            }
            base.OnFormClosed(e);
        }
    }
}
