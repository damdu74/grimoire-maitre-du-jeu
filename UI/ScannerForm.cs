// ============================================================
//  FENÊTRE SCANNER — Style Retro Gamer
//  Accessible depuis la bannière de chaque jeu
// ============================================================

using GameTrainer.Core;
using GameTrainer.Models;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace GameTrainer.UI;

public class ScannerForm : Form
{
    private readonly ProcessManager _pm;
    private readonly MemoryManager  _mm;
    private readonly MemoryScanner  _ms;

    // Résultats
    private List<ScanResult> _results = new();

    // Controls
    private ComboBox _cmbType     = null!;
    private ComboBox _cmbScanType = null!;
    private TextBox  _txtValue    = null!;
    private RetroButton _btnFirst = null!;
    private RetroButton _btnNext  = null!;
    private RetroButton _btnReset = null!;
    private ListView _lvResults   = null!;
    private Label    _lblStatus   = null!;
    private ProgressBar _progress = null!;
    private Label    _lblCount    = null!;

    // Drag
    private Point _dragOff;
    private bool  _dragging;

    public ScannerForm(ProcessManager pm, MemoryManager mm)
    {
        _pm = pm;
        _mm = mm;
        _ms = new MemoryScanner(pm, mm);
        _ms.ScanProgress += p => Invoke(() => _progress.Value = Math.Min(100, p));
        _ms.ScanStatus   += s => Invoke(() => _lblStatus.Text = s);
        BuildUI();
    }

    private void BuildUI()
    {
        FormBorderStyle = FormBorderStyle.None;
        Size            = new Size(680, 560);
        StartPosition   = FormStartPosition.CenterScreen;
        BackColor       = GT.BgApp;
        DoubleBuffered  = true;

        Paint += (s, e) =>
        {
            GT.DrawGrid(e.Graphics, ClientRectangle);
            GT.DrawScanlines(e.Graphics, ClientRectangle);
        };

        MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { _dragOff = new Point(e.X, e.Y); _dragging = true; } };
        MouseMove += (s, e) => { if (_dragging) Location = new Point(Location.X + e.X - _dragOff.X, Location.Y + e.Y - _dragOff.Y); };
        MouseUp   += (s, e) => _dragging = false;

        // ── Titlebar ─────────────────────────────────────────
        var titleBar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 40,
            BackColor = GT.BgPanel,
        };
        titleBar.Paint += (s, e) =>
        {
            GT.DrawRainbowLine(e.Graphics, 0, titleBar.Height - 1, titleBar.Width, 90);
            using var f = GT.Orb(9f);
            using var b = new SolidBrush(GT.Cyan);
            e.Graphics.DrawString("🔍  SCANNER MÉMOIRE", f, b, new PointF(12, 11));
        };
        titleBar.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { _dragOff = new Point(e.X + Left - Left, e.Y); _dragging = true; } };
        titleBar.MouseMove += (s, e) => { if (_dragging) Location = new Point(Location.X + e.X - _dragOff.X, Location.Y + e.Y - _dragOff.Y); };
        titleBar.MouseUp   += (s, e) => _dragging = false;

        var btnClose = new Button
        {
            Text = "✕", Location = new Point(titleBar.Width - 40, 0),
            Size = new Size(40, 40), FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent, ForeColor = GT.TextMuted,
            Font = new Font("Segoe UI", 10f), Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, GT.Pink);
        btnClose.Click += (s, e) => Close();
        titleBar.Controls.Add(btnClose);
        Controls.Add(titleBar);

        // ── Zone de contrôles ─────────────────────────────────
        var ctrlPanel = new Panel
        {
            Location  = new Point(12, 50),
            Size      = new Size(Width - 24, 80),
            BackColor = Color.Transparent,
        };

        // Type de données
        var lblType = MakeLabel("Type :", new Point(0, 8));
        _cmbType = MakeCombo(new Point(50, 5), 130);
        _cmbType.Items.AddRange(new object[] { "Double", "Float", "Int32", "Int64", "Byte" });
        _cmbType.SelectedIndex = 0;

        // Type de scan
        var lblScan = MakeLabel("Scan :", new Point(195, 8));
        _cmbScanType = MakeCombo(new Point(245, 5), 150);
        _cmbScanType.Items.AddRange(new object[] {
            "Valeur exacte", "Plus grand que", "Plus petit que",
            "A changé", "N'a pas changé"
        });
        _cmbScanType.SelectedIndex = 0;

        // Valeur
        var lblVal = MakeLabel("Valeur :", new Point(410, 8));
        _txtValue = new TextBox
        {
            Location    = new Point(468, 5),
            Size        = new Size(120, 24),
            BackColor   = GT.BgCard,
            ForeColor   = GT.TextMain,
            BorderStyle = BorderStyle.FixedSingle,
            Font        = GT.Mono(10f),
            PlaceholderText = "ex: 1500"
        };
        _txtValue.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) DoFirstScan(); };

        // Boutons
        _btnFirst = new RetroButton { Text = "PREMIER SCAN", Style = RetroButton.BtnStyle.Pink,  Location = new Point(0, 40),   Size = new Size(130, 28) };
        _btnNext  = new RetroButton { Text = "SCAN SUIVANT", Style = RetroButton.BtnStyle.Cyan,  Location = new Point(138, 40), Size = new Size(130, 28) };
        _btnReset = new RetroButton { Text = "RESET",        Style = RetroButton.BtnStyle.Ghost, Location = new Point(276, 40), Size = new Size(80, 28)  };

        _btnFirst.Click += (s, e) => DoFirstScan();
        _btnNext.Click  += (s, e) => DoNextScan();
        _btnReset.Click += (s, e) => { _ms.Reset(); _results.Clear(); UpdateList(); };

        ctrlPanel.Controls.AddRange(new Control[]
        {
            lblType, _cmbType, lblScan, _cmbScanType,
            lblVal, _txtValue, _btnFirst, _btnNext, _btnReset
        });
        Controls.Add(ctrlPanel);

        // ── Barre de progression ──────────────────────────────
        _progress = new ProgressBar
        {
            Location  = new Point(12, 135),
            Size      = new Size(Width - 24, 4),
            Minimum   = 0, Maximum = 100,
            Style     = ProgressBarStyle.Continuous,
            BackColor = GT.BgCard,
            ForeColor = GT.CyanDim,
        };
        Controls.Add(_progress);

        // ── Status ────────────────────────────────────────────
        _lblStatus = new Label
        {
            Location  = new Point(12, 142),
            AutoSize  = false,
            Size      = new Size(Width - 120, 18),
            Text      = "Prêt à scanner",
            ForeColor = GT.TextMuted,
            BackColor = Color.Transparent,
            Font      = GT.Orb(7f, false),
        };
        _lblCount = new Label
        {
            Location  = new Point(Width - 105, 142),
            AutoSize  = false,
            Size      = new Size(95, 18),
            Text      = "0 résultats",
            ForeColor = GT.Cyan,
            BackColor = Color.Transparent,
            Font      = GT.Orb(7f, false),
            TextAlign = ContentAlignment.MiddleRight,
        };
        Controls.Add(_lblStatus);
        Controls.Add(_lblCount);

        // ── ListView des résultats ────────────────────────────
        _lvResults = new ListView
        {
            Location    = new Point(12, 165),
            Size        = new Size(Width - 24, Height - 205),
            View        = View.Details,
            FullRowSelect = true,
            GridLines   = false,
            BackColor   = GT.BgCard,
            ForeColor   = GT.TextMain,
            BorderStyle = BorderStyle.None,
            Font        = GT.Mono(9f),
        };
        _lvResults.Columns.Add("Adresse",        160);
        _lvResults.Columns.Add("Valeur actuelle", 130);
        _lvResults.Columns.Add("Valeur précédente", 130);
        _lvResults.Columns.Add("Type",             80);
        _lvResults.OwnerDraw = false;

        // Double-clic → copie l'adresse dans le presse-papier
        _lvResults.DoubleClick += (s, e) =>
        {
            if (_lvResults.SelectedItems.Count == 0) return;
            string addr = _lvResults.SelectedItems[0].Text;
            Clipboard.SetText(addr);
            _lblStatus.Text = $"✅ Adresse copiée : {addr}";
            _lblStatus.ForeColor = GT.Green;
        };

        Controls.Add(_lvResults);

        // ── Tip bas ───────────────────────────────────────────
        var tip = new Label
        {
            Location  = new Point(12, Height - 30),
            AutoSize  = false,
            Size      = new Size(Width - 24, 20),
            Text      = "Double-clic sur une adresse pour la copier  |  Utilise les scans successifs pour affiner",
            ForeColor = Color.FromArgb(60, GT.TextMuted),
            BackColor = Color.Transparent,
            Font      = GT.Body(8.5f),
            TextAlign = ContentAlignment.MiddleCenter,
        };
        Controls.Add(tip);

        UpdateButtons();
    }

    // ── Helpers UI ────────────────────────────────────────────
    private Label MakeLabel(string text, Point loc) => new()
    {
        Text = text, Location = loc, AutoSize = true,
        ForeColor = GT.TextSoft, BackColor = Color.Transparent,
        Font = GT.Body(10f),
    };

    private ComboBox MakeCombo(Point loc, int width) => new()
    {
        Location = loc, Width = width,
        DropDownStyle = ComboBoxStyle.DropDownList,
        BackColor = GT.BgCard, ForeColor = GT.TextMain,
        FlatStyle = FlatStyle.Flat,
        Font = GT.Body(9.5f),
    };

    // ── Scan ──────────────────────────────────────────────────
    private async void DoFirstScan()
    {
        if (string.IsNullOrWhiteSpace(_txtValue.Text)) return;
        SetScanEnabled(false);
        _progress.Value = 0;
        try
        {
            _results = await _ms.FirstScanAsync(
                _txtValue.Text, GetDataType(), GetScanType());
            UpdateList();
        }
        catch (Exception ex) { _lblStatus.Text = "Erreur : " + ex.Message; }
        finally { SetScanEnabled(true); _progress.Value = 0; }
    }

    private async void DoNextScan()
    {
        if (!_ms.HasPreviousScan) return;
        SetScanEnabled(false);
        try
        {
            _results = await _ms.NextScanAsync(_txtValue.Text, GetScanType());
            UpdateList();
        }
        catch (Exception ex) { _lblStatus.Text = "Erreur : " + ex.Message; }
        finally { SetScanEnabled(true); _progress.Value = 0; }
    }

    private void UpdateList()
    {
        _lvResults.Items.Clear();
        int max = Math.Min(_results.Count, 2000);
        for (int i = 0; i < max; i++)
        {
            var r = _results[i];
            var item = new ListViewItem(r.DisplayAddress);
            item.SubItems.Add(r.DisplayValue);
            item.SubItems.Add(r.DisplayPrevious);
            item.SubItems.Add(r.DataType.ToString());
            item.Tag = r;
            _lvResults.Items.Add(item);
        }
        _lblCount.Text = _results.Count > 2000
            ? $"{_results.Count} résultats (2000 affichés)"
            : $"{_results.Count} résultats";
        UpdateButtons();
    }

    private void SetScanEnabled(bool enabled)
    {
        _btnFirst.Enabled = enabled;
        _btnNext.Enabled  = enabled && _ms.HasPreviousScan;
        _btnReset.Enabled = enabled;
    }

    private void UpdateButtons()
    {
        _btnNext.Enabled = _ms.HasPreviousScan;
    }

    private ScanDataType GetDataType() => _cmbType.SelectedIndex switch
    {
        0 => ScanDataType.Double,
        1 => ScanDataType.Float,
        2 => ScanDataType.Int32,
        3 => ScanDataType.Int64,
        4 => ScanDataType.Byte,
        _ => ScanDataType.Double,
    };

    private ScanType GetScanType() => _cmbScanType.SelectedIndex switch
    {
        0 => ScanType.ExactValue,
        1 => ScanType.BiggerThan,
        2 => ScanType.SmallerThan,
        3 => ScanType.ValueChanged,
        4 => ScanType.ValueUnchanged,
        _ => ScanType.ExactValue,
    };
}
