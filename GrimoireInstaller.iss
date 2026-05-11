// ============================================================
//  FENÊTRE PRINCIPALE — Style RetroKeys Synthwave
// ============================================================
using GameTrainer.Core;
using GameTrainer.Models;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace GameTrainer.UI;

public class MainForm : Form
{
    private ProcessManager _processManager = new();
    private MemoryManager? _memoryManager;
    private MemoryScanner? _memoryScanner;
    private System.Windows.Forms.Timer _freezeTimer  = new() { Interval = 100 };
    private System.Windows.Forms.Timer _updateTimer  = new() { Interval = 500 };
    private System.Windows.Forms.Timer _gameDetTimer = new() { Interval = 5000 };
    private List<DetectedGame> _detectedGames = new();
    private DetectedGame? _selectedGame;
    private List<SavedAddress> _savedAddresses = new();

    private Panel _sidebar = null!;
    private Panel _mainArea = null!;
    private Panel _bannerPanel = null!;
    private Panel _cheatsArea = null!;
    private Panel _statusBar = null!;
    private Panel _titleBar = null!;
    private Panel _gameListPanel = null!;
    private RetroButton _btnAddGame = null!;
    private Label _lblGameTitle = null!;
    private Label _lblGameStatus = null!;
    private Label _lblGameBits = null!;
    private Label _lblSortCount = null!;
    private RetroButton _btnAttach = null!;
    private RetroButton _btnScanner = null!;
    private Label _lblStatus = null!;
    private Point _dragOffset;
    private bool _dragging = false;

    public MainForm()
    {
        InitializeWindow();
        BuildTitleBar();
        BuildSidebar();
        BuildMainArea();
        BuildStatusBar();
        SetupTimers();
        DetectGamesAsync();
    }

    private void InitializeWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        Size = new Size(1100, 680);
        MinimumSize = new Size(900, 560);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = GrimoireTheme.BgDeep;
        DoubleBuffered = true;
        Paint += OnFormPaint;
        Resize += (s, e) => RepositionAll();
    }

    private void OnFormPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        GrimoireTheme.DrawRetroGrid(g, ClientRectangle);
        GrimoireTheme.DrawRetroSun(g, ClientRectangle);
        GrimoireTheme.DrawScanlines(g, ClientRectangle);
    }

    // ── TITLEBAR ─────────────────────────────────────────────────────────────
    private void BuildTitleBar()
    {
        _titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.FromArgb(18, 0, 32),
        };
        _titleBar.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            GrimoireTheme.DrawRainbowLine(g, 0, _titleBar.Height - 1, _titleBar.Width);

            using var emojiFont = new Font("Segoe UI Emoji", 14f);
            g.DrawString("📖", emojiFont, new SolidBrush(GrimoireTheme.TextMain),
                new RectangleF(12, 8, 28, 28));

            using var logoFont = GrimoireTheme.FontTitle(9f);
            var sf = new StringFormat { LineAlignment = StringAlignment.Center };
            GrimoireTheme.DrawNeonText(g, "LE GRIMOIRE", logoFont,
                GrimoireTheme.Pink, GrimoireTheme.Pink,
                new RectangleF(46, 0, 140, _titleBar.Height), sf, 3f);
            GrimoireTheme.DrawNeonText(g, "DU MAÎTRE DU JEU", logoFont,
                GrimoireTheme.Cyan, GrimoireTheme.Cyan,
                new RectangleF(188, 0, 240, _titleBar.Height), sf, 3f);
        };

        _titleBar.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left) { _dragging = true; _dragOffset = new Point(e.X, e.Y); }
        };
        _titleBar.MouseMove += (s, e) =>
        {
            if (_dragging) Location = new Point(Location.X + e.X - _dragOffset.X, Location.Y + e.Y - _dragOffset.Y);
        };
        _titleBar.MouseUp += (s, e) => _dragging = false;

        var btnClose    = MakeWinBtn("✕", Color.FromArgb(255, 60, 60),  _titleBar.Width - 44,  44);
        var btnMaximize = MakeWinBtn("□", GrimoireTheme.TextMuted,       _titleBar.Width - 88,  44);
        var btnMinimize = MakeWinBtn("─", GrimoireTheme.TextMuted,       _titleBar.Width - 132, 44);

        btnClose.Anchor = btnMaximize.Anchor = btnMinimize.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnClose.Click    += (s, e) => Application.Exit();
        btnMaximize.Click += (s, e) => WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        btnMinimize.Click += (s, e) => WindowState = FormWindowState.Minimized;

        _titleBar.Controls.AddRange(new Control[] { btnClose, btnMaximize, btnMinimize });
        Controls.Add(_titleBar);
    }

    private Button MakeWinBtn(string text, Color hoverBg, int x, int w)
    {
        var btn = new Button
        {
            Text = text, Location = new Point(x, 0), Size = new Size(w, 44),
            FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent,
            ForeColor = GrimoireTheme.TextMuted, Font = new Font("Segoe UI", 10f), Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, hoverBg);
        return btn;
    }

    // ── SIDEBAR ───────────────────────────────────────────────────────────────
    private void BuildSidebar()
    {
        _sidebar = new Panel { BackColor = Color.FromArgb(18, 18, 24), Width = 210 };
        _sidebar.Paint += (s, e) =>
        {
            var g = e.Graphics;
            using var pen = new Pen(GrimoireTheme.BorderPurple, 0.5f);
            g.DrawLine(pen, _sidebar.Width - 1, 0, _sidebar.Width - 1, _sidebar.Height);
            using var lf = GrimoireTheme.FontUI(7f);
            using var lb = new SolidBrush(GrimoireTheme.TextMuted);
            g.DrawString("MES JEUX", lf, lb, new PointF(12, 12));
        };

        _gameListPanel = new Panel
        {
            Location = new Point(0, 36),
            Width = _sidebar.Width,
            BackColor = Color.Transparent,
            AutoScroll = true,
        };

        _btnAddGame = new RetroButton
        {
            Text = "+ ATTACHER UN JEU",
            Style = RetroButton.ButtonStyle.Ghost,
            Size = new Size(_sidebar.Width - 20, 32),
        };
        _btnAddGame.Click += OnSelectProcess;

        _sidebar.Controls.Add(_gameListPanel);
        _sidebar.Controls.Add(_btnAddGame);
        Controls.Add(_sidebar);
    }

    // ── MAIN AREA ─────────────────────────────────────────────────────────────
    private void BuildMainArea()
    {
        _mainArea = new Panel { BackColor = Color.Transparent };

        _bannerPanel = new Panel { Dock = DockStyle.Top, Height = 130, BackColor = Color.FromArgb(28, 16, 37) };
        _bannerPanel.Paint += (s, e) =>
        {
            GrimoireTheme.DrawScanlines(e.Graphics, new Rectangle(0, 0, _bannerPanel.Width, _bannerPanel.Height));
            GrimoireTheme.DrawRainbowLine(e.Graphics, 0, _bannerPanel.Height - 1, _bannerPanel.Width);
        };

        _lblGameTitle = new Label
        {
            AutoSize = false, Location = new Point(16, 50), Size = new Size(500, 36),
            Text = "AUCUN JEU ATTACHÉ", ForeColor = GrimoireTheme.TextMain,
            BackColor = Color.Transparent, Font = GrimoireTheme.FontTitle(16f),
        };

        _lblGameStatus = MakeBadge("● INACTIF",  Color.FromArgb(123, 107, 154), new Point(16,  95));
        _lblGameBits   = MakeBadge("—",           GrimoireTheme.TextMuted,       new Point(104, 95));
        _lblSortCount  = MakeBadge("0 SORT",      GrimoireTheme.Purple,          new Point(148, 95));

        _btnAttach  = new RetroButton { Text = "ATTACHER", Style = RetroButton.ButtonStyle.Pink,  Size = new Size(110, 32) };
        _btnScanner = new RetroButton { Text = "SCANNER",  Style = RetroButton.ButtonStyle.Ghost, Size = new Size(100, 32) };
        _btnAttach.Click  += OnSelectProcess;
        _btnScanner.Click += OpenScanner;

        _bannerPanel.Controls.AddRange(new Control[]
            { _lblGameTitle, _lblGameStatus, _lblGameBits, _lblSortCount, _btnAttach, _btnScanner });

        _cheatsArea = new Panel { AutoScroll = true, BackColor = Color.Transparent, Padding = new Padding(16) };
        _cheatsArea.Paint += OnCheatsAreaPaint;

        _mainArea.Controls.Add(_cheatsArea);
        _mainArea.Controls.Add(_bannerPanel);
        Controls.Add(_mainArea);
    }

    private void OnCheatsAreaPaint(object? sender, PaintEventArgs e)
    {
        if (_cheatsArea.Controls.Count > 0) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        using var f1 = GrimoireTheme.FontUI(9f);
        using var f2 = GrimoireTheme.FontBody(10f);
        using var b  = new SolidBrush(GrimoireTheme.TextMuted);
        var sf = new StringFormat { Alignment = StringAlignment.Center };
        GrimoireTheme.DrawNeonText(g, "📖", f1, GrimoireTheme.Purple, GrimoireTheme.Purple,
            new RectangleF(0, 60, _cheatsArea.Width, 40), sf, 4f);
        g.DrawString("Attache-toi à un jeu pour voir ses sorts", f2, b,
            new RectangleF(0, 115, _cheatsArea.Width, 24), sf);
        g.DrawString("Utilise le Scanner pour trouver des adresses mémoire", f2, b,
            new RectangleF(0, 140, _cheatsArea.Width, 24), sf);
    }

    private Label MakeBadge(string text, Color color, Point location)
    {
        return new Label
        {
            Text = text, AutoSize = true, Location = location,
            ForeColor = color, BackColor = Color.FromArgb(20, color.R, color.G, color.B),
            Font = GrimoireTheme.FontUI(7f), Padding = new Padding(6, 2, 6, 2),
            BorderStyle = BorderStyle.FixedSingle,
        };
    }

    // ── STATUSBAR ─────────────────────────────────────────────────────────────
    private void BuildStatusBar()
    {
        _statusBar = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = Color.FromArgb(18, 18, 24) };
        _statusBar.Paint += (s, e) => GrimoireTheme.DrawRainbowLine(e.Graphics, 0, 0, _statusBar.Width);

        _lblStatus = new Label
        {
            AutoSize = false, Dock = DockStyle.Fill,
            Text = "⚡  Aucun processus attaché", ForeColor = GrimoireTheme.TextMuted,
            BackColor = Color.Transparent, Font = GrimoireTheme.FontUI(7f),
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0),
        };
        var lblRight = new Label
        {
            AutoSize = false, Dock = DockStyle.Right, Width = 300,
            Text = "Freeze: 100ms  |  Solo uniquement  🛡",
            ForeColor = GrimoireTheme.TextMuted, BackColor = Color.Transparent,
            Font = GrimoireTheme.FontUI(7f), TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 12, 0),
        };
        _statusBar.Controls.AddRange(new Control[] { _lblStatus, lblRight });
        Controls.Add(_statusBar);
    }

    // ── REPOSITIONNEMENT ──────────────────────────────────────────────────────
    private void RepositionAll()
    {
        int titleH  = _titleBar?.Height ?? 44;
        int statusH = _statusBar?.Height ?? 30;
        int sideW   = 210;

        if (_sidebar != null)
        {
            _sidebar.Location = new Point(0, titleH);
            _sidebar.Size     = new Size(sideW, Height - titleH - statusH);
            _gameListPanel.Size = new Size(sideW, _sidebar.Height - 80);
            _btnAddGame.Location = new Point(8, _sidebar.Height - 48);
        }
        if (_mainArea != null)
        {
            _mainArea.Location = new Point(sideW, titleH);
            _mainArea.Size     = new Size(Width - sideW, Height - titleH - statusH);
            _cheatsArea.Location = new Point(0, _bannerPanel.Height);
            _cheatsArea.Size     = new Size(_mainArea.Width, _mainArea.Height - _bannerPanel.Height);
            _bannerPanel.Width   = _mainArea.Width;
            if (_btnAttach != null)
            {
                _btnAttach.Location  = new Point(_bannerPanel.Width - 228, 48);
                _btnScanner.Location = new Point(_bannerPanel.Width - 112, 48);
            }
        }
    }

    // ── DÉTECTION DES JEUX ────────────────────────────────────────────────────
    private async void DetectGamesAsync()
    {
        var progress = new Progress<string>(msg => Invoke(() => _lblStatus.Text = "🔍  " + msg));
        _detectedGames = await GameDetector.DetectAllGamesAsync(progress);
        Invoke(RebuildGameSidebar);
    }

    private void RebuildGameSidebar()
    {
        _gameListPanel.Controls.Clear();
        int y = 0;
        GameDetector.RefreshRunningStatus(_detectedGames);
        foreach (var game in _detectedGames)
        {
            var item = new GameSidebarItem
            {
                GameName   = game.Name,
                GameEmoji  = GuessEmoji(game.Name),
                IsRunning  = game.IsRunning,
                IsSelected = _selectedGame?.ExecutableName == game.ExecutableName,
                Location   = new Point(6, y),
                Size       = new Size(_gameListPanel.Width - 12, 52),
                Tag        = game,
            };
            item.Click += OnGameItemClick;
            _gameListPanel.Controls.Add(item);
            y += 54;
        }
        _gameListPanel.Height = Math.Max(y + 10, _sidebar.Height - 80);
        UpdateStatusBar();
    }

    private void OnGameItemClick(object? sender, EventArgs e)
    {
        if (sender is not GameSidebarItem item) return;
        if (item.Tag is not DetectedGame game) return;
        _selectedGame = game;
        foreach (Control c in _gameListPanel.Controls)
            if (c is GameSidebarItem gi) { gi.IsSelected = gi.Tag == game; gi.Invalidate(); }

        UpdateBanner(game);

        if (game.IsRunning && game.RunningPid.HasValue)
        {
            try
            {
                _processManager.Attach(game.RunningPid.Value);
                _memoryManager = new MemoryManager(_processManager);
                _memoryScanner = new MemoryScanner(_processManager, _memoryManager);
                _memoryScanner.ScanStatus += msg => Invoke(() => _lblStatus.Text = "🔍  " + msg);
                UpdateStatusBar();
                _cheatsArea.Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Grimoire", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private void UpdateBanner(DetectedGame game)
    {
        _lblGameTitle.Text = game.Name.ToUpper();
        if (game.IsRunning)
        {
            _lblGameStatus.Text = $"● EN COURS — PID {game.RunningPid}";
            _lblGameStatus.ForeColor = Color.FromArgb(74, 222, 128);
            _lblGameStatus.BackColor = Color.FromArgb(20, 74, 222, 128);
        }
        else
        {
            _lblGameStatus.Text = "● INACTIF";
            _lblGameStatus.ForeColor = GrimoireTheme.TextMuted;
            _lblGameStatus.BackColor = Color.FromArgb(15, 123, 107, 154);
        }
        _lblGameBits.Text = _processManager.IsAttached
            ? (_processManager.Is64Bit ? "64-BIT" : "32-BIT") : "—";
        _bannerPanel.Invalidate();
    }

    private void UpdateStatusBar()
    {
        if (_processManager.IsAttached && _processManager.IsAlive())
        {
            _lblStatus.Text      = $"🟢  Attaché à {_processManager.AttachedProcess?.ProcessName}  |  PID {_processManager.AttachedProcess?.Id}  |  {(_processManager.Is64Bit ? "64-bit" : "32-bit")}";
            _lblStatus.ForeColor = Color.FromArgb(74, 222, 128);
        }
        else
        {
            _lblStatus.Text      = "⚡  Aucun processus attaché";
            _lblStatus.ForeColor = GrimoireTheme.TextMuted;
        }
    }

    // ── TIMERS ────────────────────────────────────────────────────────────────
    private void SetupTimers()
    {
        _freezeTimer.Tick += (s, e) =>
        {
            if (_memoryManager == null || !_processManager.IsAlive()) return;
            foreach (var addr in _savedAddresses.Where(a => a.IsFrozen))
            {
                try
                {
                    switch (addr.DataType)
                    {
                        case Core.ScanDataType.Int32:   _memoryManager.WriteInt(addr.Address,    int.Parse(addr.FreezeValue));    break;
                        case Core.ScanDataType.Float:   _memoryManager.WriteFloat(addr.Address,  float.Parse(addr.FreezeValue));  break;
                        case Core.ScanDataType.Double:  _memoryManager.WriteDouble(addr.Address, double.Parse(addr.FreezeValue)); break;
                    }
                }
                catch { }
            }
        };
        _updateTimer.Tick += (s, e) =>
        {
            if (!_processManager.IsAlive() && _processManager.IsAttached) { _processManager.Detach(); UpdateStatusBar(); }
            GameDetector.RefreshRunningStatus(_detectedGames);
            foreach (Control c in _gameListPanel.Controls)
                if (c is GameSidebarItem item && item.Tag is DetectedGame g)
                { bool was = item.IsRunning; item.IsRunning = g.IsRunning; if (was != item.IsRunning) item.Invalidate(); }
        };
        _gameDetTimer.Tick += (s, e) => { GameDetector.RefreshRunningStatus(_detectedGames); Invoke(RebuildGameSidebar); };
        _freezeTimer.Start(); _updateTimer.Start(); _gameDetTimer.Start();
        BeginInvoke(RepositionAll);
    }

    private void OnSelectProcess(object? sender, EventArgs e)
    {
        using var form = new ProcessSelectorForm();
        if (form.ShowDialog(this) == DialogResult.OK && form.SelectedProcess != null)
        {
            try
            {
                _processManager.Attach(form.SelectedProcess.PID);
                _memoryManager = new MemoryManager(_processManager);
                _memoryScanner = new MemoryScanner(_processManager, _memoryManager);
                _memoryScanner.ScanStatus += msg => Invoke(() => _lblStatus.Text = "🔍  " + msg);
                UpdateStatusBar();
                var tempGame = new DetectedGame
                {
                    Name = form.SelectedProcess.Name, ExecutableName = form.SelectedProcess.Name + ".exe",
                    Source = "Actif", IsRunning = true, RunningPid = form.SelectedProcess.PID
                };
                _selectedGame = tempGame;
                UpdateBanner(tempGame);
                _cheatsArea.Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private void OpenScanner(object? sender, EventArgs e)
    {
        if (_memoryScanner == null) { MessageBox.Show("Attache-toi d'abord à un jeu !", "Grimoire"); return; }
        MessageBox.Show("Fenêtre Scanner — prochaine mise à jour.", "Grimoire");
    }

    private static string GuessEmoji(string name)
    {
        name = name.ToLower();
        if (name.Contains("red dead")) return "🤠";
        if (name.Contains("cyber")) return "🌆";
        if (name.Contains("elden") || name.Contains("dark souls") || name.Contains("sekiro")) return "⚔️";
        if (name.Contains("witcher")) return "🐺";
        if (name.Contains("skyrim") || name.Contains("oblivion")) return "🏔️";
        if (name.Contains("fallout")) return "☢️";
        if (name.Contains("minecraft")) return "⛏️";
        if (name.Contains("gta")) return "🚗";
        if (name.Contains("hogwarts")) return "🧙";
        if (name.Contains("spider")) return "🕷️";
        if (name.Contains("god of war")) return "🪓";
        if (name.Contains("assassin")) return "🗡️";
        if (name.Contains("far cry")) return "🌴";
        return "🎮";
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _freezeTimer.Stop(); _updateTimer.Stop(); _gameDetTimer.Stop();
        _processManager.Dispose();
        base.OnFormClosing(e);
    }
}
