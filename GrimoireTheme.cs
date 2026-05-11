// ============================================================
//  FENÊTRE DE MISE À JOUR — Style RetroKeys Synthwave
//
//  S'affiche au démarrage si une nouvelle version est détectée.
//  Montre les notes de version et propose de mettre à jour.
// ============================================================

using GameTrainer.Core;
using System.Drawing.Drawing2D;

namespace GameTrainer.UI;

public class UpdateForm : Form
{
    private readonly UpdateInfo _update;
    private RetroButton _btnUpdate = null!;
    private RetroButton _btnSkip   = null!;
    private ProgressBar _progress  = null!;
    private Label _lblProgress     = null!;
    private CancellationTokenSource _cts = new();

    public UpdateForm(UpdateInfo update)
    {
        _update = update;
        BuildUI();
    }

    private void BuildUI()
    {
        // ── Fenêtre ───────────────────────────────────────────────────────────
        FormBorderStyle = FormBorderStyle.None;
        Size            = new Size(520, 400);
        StartPosition   = FormStartPosition.CenterScreen;
        BackColor       = GrimoireTheme.BgDeep;
        DoubleBuffered  = true;

        // Drag
        MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { _dragOffset = new Point(e.X, e.Y); _dragging = true; } };
        MouseMove += (s, e) => { if (_dragging) Location = new Point(Location.X + e.X - _dragOffset.X, Location.Y + e.Y - _dragOffset.Y); };
        MouseUp   += (s, e) => _dragging = false;

        Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            GrimoireTheme.DrawRetroGrid(g, ClientRectangle);
            GrimoireTheme.DrawScanlines(g, ClientRectangle);

            // Bordure néon autour de toute la fenêtre
            GrimoireTheme.DrawNeonBorder(g,
                new Rectangle(1, 1, Width - 2, Height - 2),
                GrimoireTheme.Purple, 10f, 6f);

            // Ligne dégradée en haut
            GrimoireTheme.DrawRainbowLine(g, 0, 44, Width);

            // Icône + titre
            using var emojiFont = new Font("Segoe UI Emoji", 20f);
            g.DrawString("📖", emojiFont, new SolidBrush(GrimoireTheme.TextMain),
                new RectangleF(20, 8, 36, 36));

            using var titleFont = GrimoireTheme.FontTitle(10f);
            var sf = new StringFormat { LineAlignment = StringAlignment.Center };
            GrimoireTheme.DrawNeonText(g, "MISE À JOUR DISPONIBLE", titleFont,
                GrimoireTheme.Cyan, GrimoireTheme.Cyan,
                new RectangleF(64, 0, Width - 80, 44), sf, 4f);

            // Version
            using var verFont  = GrimoireTheme.FontUI(9f);
            using var verBrush = new SolidBrush(GrimoireTheme.TextMuted);
            string verText = $"v{AutoUpdater.GetCurrentVersion()}  →  v{_update.Version}";
            g.DrawString(verText, verFont, verBrush, new RectangleF(20, 54, Width - 40, 24));

            // Date
            if (!string.IsNullOrEmpty(_update.ReleaseDate))
            {
                using var dateBrush = new SolidBrush(Color.FromArgb(100, GrimoireTheme.Cyan));
                g.DrawString("Publié le " + _update.ReleaseDate, verFont, dateBrush,
                    new RectangleF(20, 74, Width - 40, 20));
            }

            // Titre "Notes de version"
            using var notesTitle = GrimoireTheme.FontUI(7.5f);
            using var notesTB    = new SolidBrush(GrimoireTheme.TextMuted);
            g.DrawString("NOUVEAUTÉS", notesTitle, notesTB, new PointF(20, 108));
            GrimoireTheme.DrawRainbowLine(g, 20, 124, Width - 40);

            // Notes de version
            using var noteFont  = GrimoireTheme.FontBody(9.5f);
            using var noteBrush = new SolidBrush(GrimoireTheme.TextMain);
            float y = 132f;
            foreach (var note in _update.ReleaseNotes.Take(6))
            {
                // Bullet cyan
                using var bulletBrush = new SolidBrush(GrimoireTheme.Cyan);
                g.FillEllipse(bulletBrush, 20, y + 5, 5, 5);
                g.DrawString(note, noteFont, noteBrush, new RectangleF(32, y, Width - 52, 20));
                y += 22f;
            }

            // Texte obligatoire
            if (_update.Mandatory)
            {
                using var mandatoryFont  = GrimoireTheme.FontUI(7f);
                using var mandatoryBrush = new SolidBrush(GrimoireTheme.Pink);
                GrimoireTheme.DrawNeonText(g, "⚠ MISE À JOUR OBLIGATOIRE", mandatoryFont,
                    GrimoireTheme.Pink, GrimoireTheme.Pink,
                    new RectangleF(20, Height - 90, Width - 40, 20),
                    new StringFormat { Alignment = StringAlignment.Center }, 3f);
            }
        };

        // ── Barre de progression ──────────────────────────────────────────────
        _progress = new ProgressBar
        {
            Location = new Point(20, Height - 100),
            Size     = new Size(Width - 40, 6),
            Minimum  = 0, Maximum = 100, Value = 0,
            Style    = ProgressBarStyle.Continuous,
            Visible  = false,
            BackColor = GrimoireTheme.BgCard,
            ForeColor = GrimoireTheme.Purple,
        };

        _lblProgress = new Label
        {
            Location  = new Point(20, Height - 110),
            AutoSize  = false,
            Size      = new Size(Width - 40, 18),
            Text      = "",
            ForeColor = GrimoireTheme.TextMuted,
            BackColor = Color.Transparent,
            Font      = GrimoireTheme.FontUI(7f),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible   = false,
        };

        // ── Boutons ───────────────────────────────────────────────────────────
        _btnUpdate = new RetroButton
        {
            Text     = "⬇  METTRE À JOUR",
            Style    = RetroButton.ButtonStyle.Pink,
            Location = new Point(20, Height - 56),
            Size     = new Size(Width / 2 - 28, 36),
        };
        _btnUpdate.Click += OnUpdateClick;

        _btnSkip = new RetroButton
        {
            Text     = "PLUS TARD",
            Style    = RetroButton.ButtonStyle.Ghost,
            Location = new Point(Width / 2 + 8, Height - 56),
            Size     = new Size(Width / 2 - 28, 36),
        };
        _btnSkip.Click += (s, e) =>
        {
            if (_update.Mandatory)
                MessageBox.Show("Cette mise à jour est obligatoire.", "Grimoire");
            else
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        };

        Controls.AddRange(new Control[] { _progress, _lblProgress, _btnUpdate, _btnSkip });
    }

    private async void OnUpdateClick(object? sender, EventArgs e)
    {
        // Désactive les boutons pendant le téléchargement
        _btnUpdate.Enabled = false;
        _btnSkip.Enabled   = _update.Mandatory ? false : true;
        _progress.Visible  = true;
        _lblProgress.Visible = true;
        _lblProgress.Text    = "Téléchargement en cours...";

        var progress = new Progress<int>(pct =>
        {
            _progress.Value  = pct;
            _lblProgress.Text = $"Téléchargement... {pct}%";
        });

        string? installerPath = await AutoUpdater.DownloadInstallerAsync(
            _update.DownloadUrl, progress, _cts.Token);

        if (installerPath == null)
        {
            MessageBox.Show(
                "Le téléchargement a échoué.\nVérifie ta connexion internet.",
                "Grimoire — Erreur",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _btnUpdate.Enabled = true;
            _progress.Visible  = false;
            _lblProgress.Visible = false;
            return;
        }

        _lblProgress.Text = "✅ Téléchargement terminé — Lancement de l'installateur...";
        await Task.Delay(800);
        AutoUpdater.LaunchInstallerAndExit(installerPath);
    }

    // Drag support
    private Point _dragOffset;
    private bool  _dragging = false;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _cts.Cancel();
        base.OnFormClosing(e);
    }
}
