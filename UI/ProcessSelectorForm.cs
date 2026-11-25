// ============================================================
//  ÉTAPE 4 — INTERFACE : SÉLECTEUR DE PROCESSUS
//
//  CONCEPT : Cette fenêtre liste tous les processus en cours
//  sur le PC, comme le Gestionnaire des tâches Windows.
//  L'utilisateur choisit le jeu → on s'y attache.
// ============================================================

using GameTrainer.Core;

namespace GameTrainer.UI;

public class ProcessSelectorForm : Form
{
    private ListView _listView = null!;
    private Button _btnRefresh = null!;
    private Button _btnAttach = null!;
    private Button _btnCancel = null!;
    private TextBox _txtFilter = null!;
    private Label _lblInfo = null!;

    public ProcessInfo? SelectedProcess { get; private set; }

    public ProcessSelectorForm()
    {
        InitializeComponent();
        LoadProcesses();
    }

    private void InitializeComponent()
    {
        // ── Configuration de la fenêtre ───────────────────────────────────
        Text = "📖 Le Grimoire — Choisir un jeu";
        Size = new Size(700, 500);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.White;
        FormBorderStyle = FormBorderStyle.FixedDialog;

        // ── Zone de filtre ────────────────────────────────────────────────
        var lblFilter = new Label
        {
            Text = "Rechercher :",
            Location = new Point(10, 15),
            AutoSize = true,
            ForeColor = Color.LightGray
        };

        _txtFilter = new TextBox
        {
            Location = new Point(90, 12),
            Width = 300,
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "Nom du processus..."
        };
        _txtFilter.TextChanged += (s, e) => FilterProcesses();

        _btnRefresh = CreateButton("🔄 Actualiser", new Point(400, 10), Color.FromArgb(60, 80, 60));
        _btnRefresh.Click += (s, e) => LoadProcesses();

        // ── Liste des processus ───────────────────────────────────────────
        _listView = new ListView
        {
            Location = new Point(10, 50),
            Size = new Size(660, 360),
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        // Colonnes
        _listView.Columns.Add("PID", 70);
        _listView.Columns.Add("Nom du processus", 200);
        _listView.Columns.Add("Titre de la fenêtre", 270);
        _listView.Columns.Add("RAM (MB)", 80);

        _listView.DoubleClick += (s, e) => AttachToSelected();

        // ── Info ──────────────────────────────────────────────────────────
        _lblInfo = new Label
        {
            Location = new Point(10, 420),
            AutoSize = true,
            ForeColor = Color.Gray,
            Text = "Double-clic ou sélectionner puis 'Attacher'"
        };

        // ── Boutons ───────────────────────────────────────────────────────
        _btnAttach = CreateButton("✅ Attacher", new Point(490, 416), Color.FromArgb(0, 120, 60));
        _btnAttach.Click += (s, e) => AttachToSelected();

        _btnCancel = CreateButton("❌ Annuler", new Point(590, 416), Color.FromArgb(120, 40, 40));
        _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.AddRange(new Control[]
        {
            lblFilter, _txtFilter, _btnRefresh,
            _listView, _lblInfo, _btnAttach, _btnCancel
        });
    }

    private Button CreateButton(string text, Point location, Color backColor)
    {
        return new Button
        {
            Text = text,
            Location = location,
            Size = new Size(95, 30),
            BackColor = backColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
    }

    private List<ProcessInfo> _allProcesses = new();

    private void LoadProcesses()
    {
        _allProcesses = ProcessManager.GetRunningProcesses();
        FilterProcesses();
        _lblInfo.Text = $"{_allProcesses.Count} processus détectés";
    }

    private void FilterProcesses()
    {
        string filter = _txtFilter.Text.ToLower();
        var filtered = string.IsNullOrEmpty(filter)
            ? _allProcesses
            : _allProcesses.Where(p =>
                p.Name.ToLower().Contains(filter) ||
                p.Title.ToLower().Contains(filter)).ToList();

        _listView.Items.Clear();
        foreach (var p in filtered)
        {
            var item = new ListViewItem(p.PID.ToString());
            item.SubItems.Add(p.Name);
            item.SubItems.Add(p.Title);
            item.SubItems.Add(p.MemoryMB.ToString());
            item.Tag = p;
            _listView.Items.Add(item);
        }
    }

    private void AttachToSelected()
    {
        if (_listView.SelectedItems.Count == 0)
        {
            MessageBox.Show("Sélectionne d'abord un processus.", "Info",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SelectedProcess = (ProcessInfo)_listView.SelectedItems[0].Tag!;
        DialogResult = DialogResult.OK;
        Close();
    }
}
