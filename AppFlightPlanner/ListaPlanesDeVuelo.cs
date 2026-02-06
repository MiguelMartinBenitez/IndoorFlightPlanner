using AppFlightPlanner.Data.Modelos;
using AppFlightPlanner.Data.Repositorios;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppFlightPlanner
{
    public partial class ListaPlanesVuelo : Form
    {
        public PlanDeVuelo? PlanSeleccionado { get; private set; }

        private readonly RepoEspacios.SpaceRow _space;
        private readonly BindingList<FlightPlanView> _view = new BindingList<FlightPlanView>();
        private DataGridView _grid;
        private Button _btnAceptar, _btnCancelar;

        private class FlightPlanView
        {
            public string Nombre { get; set; } = "";
            public DateTime Fecha { get; set; }
            public int NumWaypoints { get; set; }
            public float StartX { get; set; }
            public float StartY { get; set; }
            public float Heading { get; set; }
            public float AlturaDeDespegue { get; set; }

        }

        public ListaPlanesVuelo(RepoEspacios.SpaceRow space)
        {
            InitializeComponent();
            _space = space;

            Text = $"Planes de vuelo — {space.Nombre}";
            StartPosition = FormStartPosition.CenterParent;
            Width = 900;
            Height = 400;

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoGenerateColumns = false
            };

            _btnAceptar = new Button { Text = "Aceptar", Width = 100 };
            _btnCancelar = new Button { Text = "Cancelar", Width = 100 };

            var panel = new Panel { Dock = DockStyle.Bottom, Height = 50 };
            panel.Controls.Add(_btnAceptar);
            panel.Controls.Add(_btnCancelar);

            _btnCancelar.Left = panel.Width - _btnCancelar.Width - 12;
            _btnAceptar.Left = _btnCancelar.Left - _btnAceptar.Width - 8;
            _btnAceptar.Top = _btnCancelar.Top = 10;

            panel.Resize += (s, e) =>
            {
                _btnCancelar.Left = panel.Width - _btnCancelar.Width - 12;
                _btnAceptar.Left = _btnCancelar.Left - _btnAceptar.Width - 8;
            };

            Controls.Add(_grid);
            Controls.Add(panel);

            // === COLUMNAS ===
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(FlightPlanView.Nombre),
                HeaderText = "Nombre",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(FlightPlanView.Fecha),
                HeaderText = "Fecha",
                Width = 160,
                DefaultCellStyle = { Format = "yyyy-MM-dd HH:mm" }
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(FlightPlanView.NumWaypoints),
                HeaderText = "Waypoints",
                Width = 90
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(FlightPlanView.Heading),
                HeaderText = "Heading (º)",
                Width = 80
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(FlightPlanView.AlturaDeDespegue),
                HeaderText = "Altura de despegue (m)",
                Width = 150
            });

            _grid.DataSource = _view;

            Load += ListaPlanesVuelo_Load;
            _btnAceptar.Click += BtnAceptar_Click;
            _btnCancelar.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            _grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0) BtnAceptar_Click(s, e);
            };
        }

        private void ListaPlanesVuelo_Load(object? sender, EventArgs e)
        {
            _view.Clear();

            if (_space.FlightPlans == null || _space.FlightPlans.Count == 0)
            {
                MessageBox.Show("No hay planes de vuelo en este espacio.");
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            foreach (var p in _space.FlightPlans)
            {
                _view.Add(new FlightPlanView
                {
                    Nombre = p.Nombre,
                    Fecha = p.Fecha,
                    NumWaypoints = p.Waypoints?.Count ?? 0,
                    StartX = p.x,
                    StartY = p.y,
                    Heading = p.heading,
                    AlturaDeDespegue = p.AlturaDespegue
                });
            }
        }

        private void BtnAceptar_Click(object? sender, EventArgs e)
        {
            if (_grid.CurrentRow?.DataBoundItem is FlightPlanView v)
            {
                PlanSeleccionado = _space.FlightPlans
                    .FirstOrDefault(p => p.Nombre == v.Nombre && p.Fecha == v.Fecha);

                if (PlanSeleccionado != null)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }
            }

            MessageBox.Show("Selecciona un plan de vuelo.");
        }
    }
}
