using AppFlightPlanner.Data.Modelos;
using AppFlightPlanner.Data.Repositorios;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace AppFlightPlanner
{
    public partial class IndoorListForm : Form
    {
        public RepoEspacios.SpaceRow? SelectedSpace { get; private set; }

        private readonly List<RepoEspacios.SpaceRow> _items;
        private readonly BindingList<SpaceView> _view = new BindingList<SpaceView>();
        private DataGridView _grid;
        private Button _btnAceptar, _btnCancelar;

        // ViewModel para mostrar en la tabla
        private class SpaceView
        {
            public int Id { get; set; }
            public string Nombre { get; set; } = "";
            public DateTime Creado { get; set; }
            public int Capas { get; set; }        // nº de capas
            public int Poligonos { get; set; }    // nº de polígonos totales
            public int Vertices { get; set; }     // nº de vértices totales
            public int Circulos { get; set; }     // nº de círculos totales
            public string AlturaTotal { get; set; } = ""; // rango total de alturas
        }

        public IndoorListForm(List<RepoEspacios.SpaceRow> items)
        {
            InitializeComponent();
            _items = items;

            Text = "Cargar espacio cerrado";
            StartPosition = FormStartPosition.CenterParent;
            Width = 750;
            Height = 420;

            // Crear controles
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

            _btnAceptar = new Button { Text = "Aceptar", Anchor = AnchorStyles.Right | AnchorStyles.Bottom, Width = 100 };
            _btnCancelar = new Button { Text = "Cancelar", Anchor = AnchorStyles.Right | AnchorStyles.Bottom, Width = 100 };

            var panelBotones = new Panel { Dock = DockStyle.Bottom, Height = 50 };
            panelBotones.Controls.Add(_btnAceptar);
            panelBotones.Controls.Add(_btnCancelar);
            _btnCancelar.Left = panelBotones.Width - _btnCancelar.Width - 12;
            _btnAceptar.Left = _btnCancelar.Left - _btnAceptar.Width - 8;
            _btnAceptar.Top = _btnCancelar.Top = 10;
            panelBotones.Resize += (s, e) =>
            {
                _btnCancelar.Left = panelBotones.Width - _btnCancelar.Width - 12;
                _btnAceptar.Left = _btnCancelar.Left - _btnAceptar.Width - 8;
            };

            Controls.Add(_grid);
            Controls.Add(panelBotones);

            // Columnas
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(SpaceView.Nombre),
                HeaderText = "Nombre",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(SpaceView.Creado),
                HeaderText = "Creado",
                Width = 160,
                DefaultCellStyle = { Format = "yyyy-MM-dd HH:mm" }
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(SpaceView.Capas),
                HeaderText = "Capas",
                Width = 70
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(SpaceView.Poligonos),
                HeaderText = "Polígonos",
                Width = 90
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(SpaceView.Vertices),
                HeaderText = "Vértices",
                Width = 90
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(SpaceView.Circulos),
                HeaderText = "Círculos",
                Width = 80
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(SpaceView.AlturaTotal),
                HeaderText = "Altura total",
                Width = 110
            });

            // Columna Id oculta
            var colId = new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(SpaceView.Id),
                Visible = false
            };
            _grid.Columns.Add(colId);

            // Eventos
            Load += IndoorListForm_Load;
            _btnAceptar.Click += BtnAceptar_Click;
            _btnCancelar.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            _grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0) BtnAceptar_Click(s, e);
            };

            _grid.DataSource = _view;
        }

        private void IndoorListForm_Load(object? sender, EventArgs e)
        {
            _view.Clear();

            foreach (var it in _items)
            {
                // Calcula valores totales de todas las capas
                int capas = it.Layers?.Count ?? 0;
                int totalPolys = 0, totalVerts = 0, totalCircles = 0;
                string rangoAltura = "-";

                if (it.Layers != null && it.Layers.Count > 0)
                {
                    totalPolys = it.Layers.Sum(l => l.Polygons?.Count ?? 0);
                    totalVerts = it.Layers.Sum(l => l.Polygons?.Sum(p => p?.Count ?? 0) ?? 0);
                    totalCircles = it.Layers.Sum(l => l.Circles?.Count ?? 0);
                    rangoAltura = $"{it.Layers.First().AlturaInicio:0.0}-{it.Layers.Last().AlturaFin:0.0} m";
                }

                _view.Add(new SpaceView
                {
                    Id = it.Id,
                    Nombre = it.Nombre,
                    Creado = it.CreatedAt,
                    Capas = capas,
                    Poligonos = totalPolys,
                    Vertices = totalVerts,
                    Circulos = totalCircles,
                    AlturaTotal = rangoAltura
                });
            }
        }

        private void BtnAceptar_Click(object? sender, EventArgs e)
        {
            if (_grid.CurrentRow?.DataBoundItem is SpaceView v)
            {
                var match = _items.FirstOrDefault(x => x.Id == v.Id);
                if (match != null)
                {
                    SelectedSpace = match;
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }
            }

            MessageBox.Show(this,
                "Selecciona un espacio.",
                "Atención",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
