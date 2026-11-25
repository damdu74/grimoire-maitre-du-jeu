using GameTrainer.Core;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace GameTrainer.UI;

public class UpdateForm : Form
{
    private readonly UpdateInfo _update;
    private RetroButton _btnUpdate = null!;
    private RetroButton _btnSkip   = null!;
    private ProgressBar _progress  = null!;
    private Label _lblProgress     = null!;
    private CancellationTokenSource _cts = new();
    private Point _dragOffset;
    private bool  _dragging;

    public UpdateForm(UpdateInfo update)
    {
        _update = update;
        BuildUI();
    }

    private void BuildUI()
    {
        FormBorderStyle = FormBorderStyle.None;
        Size            = new Size(500, 390);
        StartPosition   = FormStartPosition.CenterScreen;
        BackColor       = GT.BgApp;
        DoubleBuffered  = true;

        MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { _dragOffset = new Point(e.X, e.Y); _dragging = true; } };
        MouseMove += (s, e) => { if (_dragging) Location = new Point(Location.X + e.X - _dragOffset.X, Location.Y + e.Y - _dragOffset.Y); };
        MouseUp   += (s, e) => _dragging = false;

        Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode    = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            GT.DrawGrid(g, ClientRectangle);
            GT.DrawScanlines(g, ClientRectangle);

            // Bordure sobre
            GT.DrawBorder(g, new Rectangle(1, 1, Width - 2, Height - 2), GT.CyanBorder, 10f, 1f);

            // Ligne dégradée en haut
            GT.DrawRainbowLine(g, 0, 44, Width, 100);

            // Icône + titre
            using var ef = new Font("Segoe UI Emoji", 18f);
            g.DrawString("📖", ef, new SolidBrush(GT.TextMain), new PointF(16, 10));

            using var tf = GT.Orb(10f);
            using var tb = new SolidBrush(GT.TextWhite);
            g.DrawString("MISE À JOUR DISPONIBLE", tf, tb,
                new RectangleF(54, 0, Width - 70, 44),
                new StringFormat { LineAlignment = StringAlignment.Center });

            // Version
            using var vf = GT.Orb(8f, false);
            using var vb = new SolidBrush(GT.TextSoft);
            g.DrawString($"v{AutoUpdater.GetCurrentVersion()}  →  v{_update.Version}", vf, vb,
                new RectangleF(18, 52, Width - 36, 22));

            if (!string.IsNullOrEmpty(_update.ReleaseDate))
            {
                using var db = new SolidBrush(GT.TextMuted);
                g.DrawString("Publié le " + _update.ReleaseDate, vf, db,
                    new RectangleF(18, 72, Width - 36, 18));
            }

            // Séparateur + titre nouveautés
            GT.DrawRainbowLine(g, 18, 100, Width - 36, 80);
            using var nf = GT.Orb(7f, false);
            using var nb = new SolidBrush(Color.FromArgb(110, GT.TextMuted));
            g.DrawString("NOUVEAUTÉS", nf, nb, new PointF(18, 108));

            // Notes
            using var bf = GT.Body(10f);
            using var bb = new SolidBrush(GT.TextMain);
            float y = 128f;
            foreach (var note in _update.ReleaseNotes.Take(6))
            {
                using var dot = new SolidBrush(GT.Cyan);
                g.FillEllipse(dot, 18, y + 5, 5, 5);
                g.DrawString(note, bf, bb, new RectangleF(30, y, Width - 48, 20));
                y += 22f;
            }

            // Obligatoire
            if (_update.Mandatory)
            {
                using var mb = new SolidBrush(GT.PinkLight);
                using var mf = GT.Orb(7f);
                g.DrawString("⚠ MISE À JOUR OBLIGATOIRE", mf, mb,
                    new RectangleF(0, Height - 88, Width, 18),
                    new StringFormat { Alignment = StringAlignment.Center });
            }
        };

        _progress = new ProgressBar
        {
            Location  = new Point(18, Height - 96),
            Size      = new Size(Width - 36, 5),
            Minimum   = 0, Maximum = 100, Value = 0,
            Style     = ProgressBarStyle.Continuous,
            Visible   = false,
            BackColor = GT.BgCard,
            ForeColor = GT.CyanDim,
        };

        _lblProgress = new Label
        {
            Location  = new Point(18, Height - 108),
            AutoSize  = false,
            Size      = new Size(Width - 36, 16),
            Text      = "",
            ForeColor = GT.TextSoft,
            BackColor = Color.Transparent,
            Font      = GT.Orb(7f, false),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible   = false,
        };

        _btnUpdate = new RetroButton
        {
            Text     = "⬇  METTRE À JOUR",
            Style    = RetroButton.BtnStyle.Pink,
            Location = new Point(18, Height - 54),
            Size     = new Size(Width / 2 - 26, 34),
        };
        _btnUpdate.Click += OnUpdateClick;

        _btnSkip = new RetroButton
        {
            Text     = "PLUS TARD",
            Style    = RetroButton.BtnStyle.Ghost,
            Location = new Point(Width / 2 + 8, Height - 54),
            Size     = new Size(Width / 2 - 26, 34),
        };
        _btnSkip.Click += (s, e) =>
        {
            if (_update.Mandatory)
                MessageBox.Show("Cette mise à jour est obligatoire.", "Grimoire");
            else { DialogResult = DialogResult.Cancel; Close(); }
        };

        Controls.AddRange(new Control[] { _progress, _lblProgress, _btnUpdate, _btnSkip });
    }

    private async void OnUpdateClick(object? sender, EventArgs e)
    {
        _btnUpdate.Enabled   = false;
        _btnSkip.Enabled     = !_update.Mandatory;
        _progress.Visible    = true;
        _lblProgress.Visible = true;
        _lblProgress.Text    = "Téléchargement en cours...";

        var progress = new Progress<int>(pct =>
        {
            _progress.Value   = pct;
            _lblProgress.Text = $"Téléchargement... {pct}%";
        });

        string? path = await AutoUpdater.DownloadInstallerAsync(
            _update.DownloadUrl, progress, _cts.Token);

        if (path == null)
        {
            MessageBox.Show("Le téléchargement a échoué.\nVérifie ta connexion internet.",
                "Grimoire — Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _btnUpdate.Enabled   = true;
            _progress.Visible    = false;
            _lblProgress.Visible = false;
            return;
        }

        _lblProgress.Text = "✅ Terminé — lancement de l'installateur...";
        await Task.Delay(800);
        AutoUpdater.LaunchInstallerAndExit(path);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _cts.Cancel();
        base.OnFormClosing(e);
    }
}
