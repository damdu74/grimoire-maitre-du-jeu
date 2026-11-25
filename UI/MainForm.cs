// ============================================================
//  FENÊTRE PRINCIPALE — Style Retro Gamer
//  Fond bleu marine, grille perspective, accents cyan/rose sobres
// ============================================================

using GameTrainer.Core;
using GameTrainer.Core.Profiles;
using GameTrainer.Models;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace GameTrainer.UI;

public class MainForm : Form
{
    // ── Core ──────────────────────────────────────────────────────────────────
    private ProcessManager _pm  = new();
    private MemoryManager?  _mm;
    private MemoryScanner?  _ms;
    private CheatEngine?    _ce;
    private GameProfile?    _profile;
    private List<SortDefinition> _sorts = new();   // les sorts affichés (mêmes objets que les cartes)

    // ── Raccourcis globaux (marchent même quand le jeu a le focus) ──────────────
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    private const int WM_HOTKEY = 0x0312;
    [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string app, string? idList);
    private readonly Dictionary<int, SortDefinition> _hotkeys = new();

    // ── Timers ────────────────────────────────────────────────────────────────
    private readonly System.Windows.Forms.Timer _freezeT  = new() { Interval = 100 };
    private readonly System.Windows.Forms.Timer _updateT  = new() { Interval = 600 };

    // ── Data ──────────────────────────────────────────────────────────────────
    private List<DetectedGame>  _games    = new();
    private DetectedGame?       _selected;
    private List<SavedAddress>  _addresses = new();

    // ── Layout panels ─────────────────────────────────────────────────────────
    private Panel _sidebar      = null!;
    private Panel _mainArea     = null!;
    private Panel _banner       = null!;
    private Panel _cheatsArea   = null!;
    private Panel _statusBar    = null!;
    private Panel _titleBar     = null!;
    private Panel _gameList     = null!;
    private RetroButton _btnAddGame = null!;
    private TextBox _search = null!;
    private RetroButton _btnLaunch = null!;
    private Image? _heroImage;   // grande image de fond de la bannière
    private int?   _attachTriedPid;

    // ── Banner controls ───────────────────────────────────────────────────────
    private Label _lblTitle     = null!;
    private Label _lblStatus    = null!;
    private Label _lblBits      = null!;
    private Label _lblSorts     = null!;
    private Label _btnFavorite  = null!;
    private RetroButton _btnScannerOpen = null!;
    private PictureBox _picGame = null!;

    // Favoris — persistés en mémoire (exes des jeux favoris)
    private HashSet<string> _favorites = new(StringComparer.OrdinalIgnoreCase);

    // ── Statusbar ─────────────────────────────────────────────────────────────
    private Label _lblBar = null!;

    // ── Drag ──────────────────────────────────────────────────────────────────
    private Point _dragOff;
    private bool  _dragging;

    // ═════════════════════════════════════════════════════════════════════════
    public MainForm()
    {
        InitWindow();
        BuildTitleBar();
        BuildSidebar();
        BuildMain();
        BuildStatusBar();
        SetupTimers();
    }

    // ── Fenêtre ───────────────────────────────────────────────────────────────
    private void InitWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        Size            = new Size(1100, 680);
        MinimumSize     = new Size(900, 560);
        StartPosition   = FormStartPosition.CenterScreen;
        BackColor       = GT.BgApp;
        DoubleBuffered  = true;
        ResizeRedraw    = true;   // sinon la grille garde des traces après agrandissement
        Paint          += (s, e) =>
        {
            GT.DrawGrid(e.Graphics, ClientRectangle);
            GT.DrawPerspectiveGrid(e.Graphics, ClientRectangle);
            GT.DrawScanlines(e.Graphics, ClientRectangle);
        };
        Resize += (s, e) => Reflow();
    }

    // ── TitleBar ──────────────────────────────────────────────────────────────
    private void BuildTitleBar()
    {
        _titleBar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 44,
            BackColor = GT.BgPanel,
        };
        _titleBar.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            GT.DrawRainbowLine(g, 0, _titleBar.Height - 1, _titleBar.Width, 100);

            // Emoji
            using var ef = new Font("Segoe UI Emoji", 14f);
            g.DrawString("📖", ef, new SolidBrush(GT.TextMain), new PointF(12, 12));

            // "LE GRIMOIRE" rose + "DU MAÎTRE DU JEU" cyan, collés sur une ligne
            using var f = GT.Orb(9f);
            using var b1 = new SolidBrush(GT.PinkLight);
            using var b2 = new SolidBrush(GT.Cyan);
            var sf = new StringFormat { LineAlignment = StringAlignment.Center };
            float w1 = g.MeasureString("LE GRIMOIRE ", f).Width;
            g.DrawString("LE GRIMOIRE", f, b1, new RectangleF(46, 0, w1, _titleBar.Height), sf);
            g.DrawString("DU MAÎTRE DU JEU", f, b2, new RectangleF(46 + w1, 0, 300, _titleBar.Height), sf);
        };
        _titleBar.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { _dragging = true; _dragOff = new Point(e.X, e.Y); } };
        _titleBar.MouseMove += (s, e) => { if (_dragging) Location = new Point(Location.X + e.X - _dragOff.X, Location.Y + e.Y - _dragOff.Y); };
        _titleBar.MouseUp   += (s, e) => _dragging = false;

        var bC = WinBtn("✕", Color.FromArgb(190, 50, 70), _titleBar.Width - 44,  44);
        var bM = WinBtn("□", GT.TextMuted,                 _titleBar.Width - 88,  44);
        var bN = WinBtn("─", GT.TextMuted,                 _titleBar.Width - 132, 44);
        bC.Anchor = bM.Anchor = bN.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        bC.Click += (s, e) => Application.Exit();
        bM.Click += (s, e) => WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        bN.Click += (s, e) => WindowState = FormWindowState.Minimized;
        _titleBar.Controls.AddRange(new Control[] { bC, bM, bN });
        Controls.Add(_titleBar);
    }

    private Button WinBtn(string txt, Color hover, int x, int w)
    {
        var b = new Button
        {
            Text = txt, Location = new Point(x, 0), Size = new Size(w, 44),
            FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent,
            ForeColor = GT.TextMuted, Font = new Font("Segoe UI", 10f), Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(35, hover);
        return b;
    }

    // ── Sidebar ───────────────────────────────────────────────────────────────
    private void BuildSidebar()
    {
        _sidebar = new Panel { BackColor = GT.BgPanel, Width = 210 };
        _sidebar.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(30, 0, 180, 210), 0.5f);
            e.Graphics.DrawLine(pen, _sidebar.Width - 1, 0, _sidebar.Width - 1, _sidebar.Height);

            // Cadre du champ de recherche
            GT.DrawBorder(e.Graphics, new Rectangle(8, 10, _sidebar.Width - 17, 28), Color.FromArgb(60, 0, 180, 210), 5f, 1f);

            // Label section
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using var lf = GT.Orb(7f, false);
            using var lb = new SolidBrush(Color.FromArgb(110, GT.TextMuted));
            e.Graphics.DrawString("MES JEUX", lf, lb, new PointF(12, 48));
        };

        _search = new TextBox
        {
            Location        = new Point(16, 16),
            Width           = _sidebar.Width - 33,
            BorderStyle     = BorderStyle.None,
            BackColor       = GT.BgPanel,
            ForeColor       = GT.TextMain,
            Font            = GT.Body(10f),
            PlaceholderText = "🔍  Rechercher un jeu",
        };
        _search.TextChanged += (s, e) => BuildSidebarList();
        _sidebar.Controls.Add(_search);

        _gameList = new Panel
        {
            Location    = new Point(0, 66),
            Width       = _sidebar.Width,
            BackColor   = GT.BgPanel,
            AutoScroll  = true,
        };

        _btnAddGame = new RetroButton
        {
            Text  = "+ Ajouter un jeu",
            Style = RetroButton.BtnStyle.Ghost,
            Size  = new Size(_sidebar.Width - 16, 30),
        };
        _btnAddGame.Click += OnAddGame;

        // Barres de défilement sombres (thème Windows 10/11) au lieu des barres blanches
        _gameList.HandleCreated += (s, e) => SetWindowTheme(_gameList.Handle, "DarkMode_Explorer", null);
        _sidebar.Controls.Add(_gameList);
        _sidebar.Controls.Add(_btnAddGame);
        Controls.Add(_sidebar);
    }

    // ── Main area ─────────────────────────────────────────────────────────────
    private void BuildMain()
    {
        _mainArea = new Panel { BackColor = Color.Transparent };

        // Banner
        _banner = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 190,
            BackColor = GT.BgPanel,
        };
        _banner.Paint += OnBannerPaint;

        // Jaquette du jeu (format Steam 2:3)
        _picGame = new PictureBox
        {
            Location    = new Point(20, 20),
            Size        = new Size(100, 150),
            SizeMode    = PictureBoxSizeMode.Zoom,
            BackColor   = Color.FromArgb(18, 0, 180, 210),
            BorderStyle = BorderStyle.None,
        };
        _picGame.Paint += (s, e) =>
        {
            if (_picGame.Image == null) return;
            var g = e.Graphics;
            g.InterpolationMode  = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode    = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.SmoothingMode      = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            var src  = _picGame.Image;
            var dest = _picGame.ClientRectangle;
            float ratioSrc  = (float)src.Width / src.Height;
            float ratioDest = (float)dest.Width / dest.Height;
            Rectangle drawRect;
            if (ratioSrc > ratioDest)
            { int h = (int)(dest.Width / ratioSrc); drawRect = new Rectangle(0, (dest.Height - h) / 2, dest.Width, h); }
            else
            { int w = (int)(dest.Height * ratioSrc); drawRect = new Rectangle((dest.Width - w) / 2, 0, w, dest.Height); }
            e.Graphics.Clear(Color.FromArgb(18, 0, 180, 210));
            g.DrawImage(src, drawRect, new Rectangle(0, 0, src.Width, src.Height), GraphicsUnit.Pixel);
        };

        // Titre du jeu
        _lblTitle = new Label
        {
            AutoSize  = false,
            Location  = new Point(136, 28),
            Size      = new Size(600, 40),
            Text      = "CHOISIS UN JEU",
            ForeColor = GT.TextWhite,
            BackColor = Color.Transparent,
            Font      = GT.Orb(18f),
            AutoEllipsis = true,
        };

        // Bouton favori (étoile) à côté du titre
        _btnFavorite = new Label
        {
            Text      = "☆",
            Location  = new Point(134, 74),
            AutoSize  = true,
            ForeColor = Color.FromArgb(100, GT.Yellow),
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI Emoji", 13f),
            Cursor    = Cursors.Hand,
            Tag       = false, // false = pas favori
        };
        _btnFavorite.Click += OnToggleFavorite;

        _lblStatus = MakeBadge("● ARRÊTÉ",   GT.TextMuted,  Color.FromArgb(18, 80, 105, 150), new Point(164, 77));
        _lblBits   = MakeBadge("—",          GT.TextMuted,  Color.FromArgb(12, 0, 180, 210),  new Point(0, 77));
        _lblSorts  = MakeBadge("0 SORT",     GT.CyanDim,    Color.FromArgb(14, 0, 180, 210),  new Point(0, 77));
        // Les badges suivent la largeur du précédent (le texte du statut change)
        _lblStatus.SizeChanged += (s, e) => PlaceBadges();
        _lblBits.SizeChanged   += (s, e) => PlaceBadges();

        // Gros bouton d'action : lance le jeu, ou indique qu'il tourne
        _btnLaunch = new RetroButton
        {
            Text     = "▶  LANCER LE JEU",
            Style    = RetroButton.BtnStyle.Pink,
            Location = new Point(136, 122),
            Size     = new Size(190, 40),
            Visible  = false,
        };
        _btnLaunch.Click += OnLaunchGame;

        // Bouton Scanner en haut à droite de la bannière
        _btnScannerOpen = new RetroButton
        {
            Text   = "SCANNER",
            Style  = RetroButton.BtnStyle.Ghost,
            Size   = new Size(130, 30),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        _btnScannerOpen.Click += OpenScanner;

        _banner.Controls.AddRange(new Control[]
            { _picGame, _lblTitle, _btnFavorite, _lblStatus, _lblBits, _lblSorts, _btnLaunch, _btnScannerOpen });
        PlaceBadges();
        // Rien à afficher tant qu'aucun jeu n'est choisi
        foreach (var c in new Control[] { _picGame, _btnFavorite, _lblStatus, _lblBits, _lblSorts }) c.Visible = false;

        // Zone des sorts
        _cheatsArea = new Panel
        {
            AutoScroll  = true,
            BackColor   = Color.Transparent,
            Padding     = new Padding(14),
        };
        _cheatsArea.Paint += PaintCheatsEmpty;
        // Panels : repeindre entièrement au redimensionnement (image de fond, message centré)
        _banner.Resize     += (s, e) => _banner.Invalidate(true);
        _cheatsArea.Resize += (s, e) => _cheatsArea.Invalidate();

        _mainArea.Controls.Add(_cheatsArea);
        _mainArea.Controls.Add(_banner);
        Controls.Add(_mainArea);
    }

    private void PlaceBadges()
    {
        _lblBits.Left  = _lblStatus.Right + 6;
        _lblSorts.Left = _lblBits.Right + 6;
    }

    private void OnBannerPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        var full = new Rectangle(0, 0, _banner.Width, _banner.Height);

        // Image « héro » Steam en fond, recadrée en largeur, assombrie vers la gauche
        if (_heroImage != null)
        {
            var src = _heroImage;
            int srcH = (int)(src.Width * (float)full.Height / full.Width);
            var srcRect = srcH <= src.Height
                ? new Rectangle(0, (src.Height - srcH) / 3, src.Width, srcH)
                : new Rectangle(0, 0, src.Width, src.Height);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, full, srcRect, GraphicsUnit.Pixel);
            using var shade = new LinearGradientBrush(full,
                Color.FromArgb(240, GT.BgPanel), Color.FromArgb(120, GT.BgPanel), LinearGradientMode.Horizontal);
            g.FillRectangle(shade, full);
        }

        GT.DrawScanlines(g, full);
        GT.DrawRainbowLine(g, 0, _banner.Height - 1, _banner.Width, 100);
        // Bordure arrondie autour de l'image
        if (_picGame != null)
        {
            var r = new Rectangle(_picGame.Left - 1, _picGame.Top - 1,
                _picGame.Width + 2, _picGame.Height + 2);
            GT.DrawBorder(g, r, Color.FromArgb(50, 0, 180, 210), 6f, 1f);
        }
    }

    private void PaintCheatsEmpty(object? s, PaintEventArgs e)
    {
        if (_cheatsArea.Controls.Count > 0) return;
        var g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        var sf = new StringFormat { Alignment = StringAlignment.Center };
        using var f1 = GT.Orb(10f);
        using var f2 = GT.Body(10f);
        using var b1 = new SolidBrush(Color.FromArgb(50, GT.Cyan));
        using var b2 = new SolidBrush(GT.TextMuted);
        g.DrawString("📖", new Font("Segoe UI Emoji", 36f),
            new SolidBrush(Color.FromArgb(40, GT.Cyan)),
            new RectangleF(0, 55, _cheatsArea.Width, 55), sf);
        string l1 = _selected == null ? "Choisis un jeu dans la liste à gauche"
                                      : "Pas encore de triches pour ce jeu";
        g.DrawString(l1, f2, b2, new RectangleF(0, 118, _cheatsArea.Width, 24), sf);
        g.DrawString("Le Scanner permet d'en trouver soi-même (lance le jeu d'abord)",
            f2, b2, new RectangleF(0, 142, _cheatsArea.Width, 24), sf);
    }

    private Label MakeBadge(string txt, Color fore, Color back, Point loc)
    {
        return new Label
        {
            Text        = txt,
            AutoSize    = true,
            Location    = loc,
            ForeColor   = fore,
            BackColor   = back,
            Font        = GT.Orb(7f, false),
            Padding     = new Padding(6, 2, 6, 2),
            BorderStyle = BorderStyle.FixedSingle,
        };
    }

    // ── Statusbar ─────────────────────────────────────────────────────────────
    private void BuildStatusBar()
    {
        _statusBar = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 28,
            BackColor = GT.BgPanel,
        };
        _statusBar.Paint += (s, e) =>
            GT.DrawRainbowLine(e.Graphics, 0, 0, _statusBar.Width, 90);

        _lblBar = new Label
        {
            AutoSize    = false,
            Dock        = DockStyle.Fill,
            Text        = "⚡  Aucun processus attaché",
            ForeColor   = GT.TextMuted,
            BackColor   = Color.Transparent,
            Font        = GT.Orb(7f, false),
            TextAlign   = ContentAlignment.MiddleLeft,
            Padding     = new Padding(12, 0, 0, 0),
        };
        var lblR = new Label
        {
            AutoSize    = false,
            Dock        = DockStyle.Right,
            Width       = 280,
            Text        = "FREEZE: 100MS  |  SOLO UNIQUEMENT",
            ForeColor   = Color.FromArgb(90, GT.TextMuted),
            BackColor   = Color.Transparent,
            Font        = GT.Orb(7f, false),
            TextAlign   = ContentAlignment.MiddleRight,
            Padding     = new Padding(0, 0, 12, 0),
        };
        _statusBar.Controls.AddRange(new Control[] { _lblBar, lblR });
        Controls.Add(_statusBar);
    }

    // ── Reflow ────────────────────────────────────────────────────────────────
    private void Reflow()
    {
        int th = _titleBar?.Height ?? 44;
        int sh = _statusBar?.Height ?? 28;
        int sw = 210;

        if (_sidebar != null)
        {
            _sidebar.Location = new Point(0, th);
            _sidebar.Size     = new Size(sw, Height - th - sh);
            _gameList.Size    = new Size(sw, _sidebar.Height - 66 - 54);
            _btnAddGame.Location = new Point(8, _sidebar.Height - 44);
        }
        if (_mainArea != null)
        {
            _mainArea.Location = new Point(sw, th);
            _mainArea.Size     = new Size(Width - sw, Height - th - sh);
            _cheatsArea.Location = new Point(0, _banner.Height);
            _cheatsArea.Size     = new Size(_mainArea.Width, _mainArea.Height - _banner.Height);
            _banner.Width        = _mainArea.Width;
            _lblTitle.Size       = new Size(Math.Max(100, _mainArea.Width - 136 - 160), 40);
            if (_btnScannerOpen != null)
                _btnScannerOpen.Location = new Point(_banner.Width - _btnScannerOpen.Width - 16, 16);
        }
    }

    // ── Timers ────────────────────────────────────────────────────────────────
    private void SetupTimers()
    {
        _freezeT.Tick += (s, e) =>
        {
            if (_mm == null || !_pm.IsAlive()) return;
            _ce?.ExecuteFreeze(_sorts);
            foreach (var addr in _addresses.Where(a => a.IsFrozen))
            {
                try
                {
                    switch (addr.DataType)
                    {
                        case ScanDataType.Int32:  _mm.WriteInt(addr.Address,    int.Parse(addr.FreezeValue));    break;
                        case ScanDataType.Float:  _mm.WriteFloat(addr.Address,  float.Parse(addr.FreezeValue));  break;
                        case ScanDataType.Double: _mm.WriteDouble(addr.Address, double.Parse(addr.FreezeValue)); break;
                    }
                }
                catch { }
            }
        };

        _updateT.Tick += (s, e) =>
        {
            if (!_pm.IsAlive() && _pm.IsAttached) { _pm.Detach(); RefreshBar(); }
            try
            {
                GameDetector.RefreshRunningStatus(_games);
                foreach (Control c in _gameList.Controls)
                    if (c is GameSidebarItem item && item.Tag is DetectedGame g)
                    { bool was = item.IsRunning; item.IsRunning = g.IsRunning; if (was != item.IsRunning) item.Invalidate(); }

                // Le jeu sélectionné vient de démarrer → connexion automatique (une tentative par PID)
                if (_selected is { IsRunning: true, RunningPid: int pid } && !_pm.IsAttached && pid != _attachTriedPid)
                {
                    _attachTriedPid = pid;
                    AttachTo(pid);
                }
                if (_selected != null) UpdateRunState(_selected);
                RefreshLiveValues();
            }
            catch { }
        };

        Load += (s, e) =>
        {
            Reflow();
            _freezeT.Start();
            _updateT.Start();
            DetectAsync();
        };
    }

    // ── Détection ─────────────────────────────────────────────────────────────
    private async void DetectAsync()
    {
        var prog = new Progress<string>(msg =>
        {
            if (IsHandleCreated) Invoke(() => _lblBar.Text = "🔍  " + msg);
        });
        _games = await GameDetector.DetectAllGamesAsync(prog);
        if (IsHandleCreated) Invoke(BuildSidebarList);
    }

    // Ne pas appeler en boucle : reconstruire la liste la ferait sauter.
    // L'état « Actif » des jeux est rafraîchi en place par le timer _updateT.
    private void BuildSidebarList()
    {
        int scroll = -_gameList.AutoScrollPosition.Y;   // garde la position de défilement
        _gameList.SuspendLayout();
        foreach (Control c in _gameList.Controls.Cast<Control>().ToList()) c.Dispose();
        int y = 0;
        GameDetector.RefreshRunningStatus(_games);

        // Trie : favoris en premier, puis actifs, puis alphabétique
        string q = _search.Text.Trim();
        var sorted = _games
            .Where(g => q.Length == 0 || g.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(g => _favorites.Contains(g.ExecutableName))
            .ThenByDescending(g => ProfileRegistry.FindProfile(g.ExecutableName) != null)
            .ThenByDescending(g => g.IsRunning)
            .ThenBy(g => g.Name)
            .ToList();
        foreach (var game in sorted)
        {
            if (game.GameImage == null)
                PreloadGameImage(game);

            bool isFav = _favorites.Contains(game.ExecutableName);
            var item = new GameSidebarItem
            {
                GameName   = (isFav ? "★ " : "") + game.Name,
                GameEmoji  = GuessEmoji(game.Name),
                IsRunning  = game.IsRunning,
                HasCheats  = ProfileRegistry.FindProfile(game.ExecutableName) != null,
                IsSelected = _selected?.ExecutableName == game.ExecutableName,
                GameImage  = game.GameImage,
                Location   = new Point(6, y),
                // place réservée à la barre verticale → pas de barre horizontale parasite
                Size       = new Size(_gameList.Width - 12 - SystemInformation.VerticalScrollBarWidth, 50),
                Tag        = game,
            };
            item.Click += OnGameClick;
            _gameList.Controls.Add(item);
            y += 52;
        }
        // Hauteur fixée par Reflow : la liste défile au lieu de recouvrir « Ajouter un jeu »
        _gameList.ResumeLayout();
        _gameList.AutoScrollPosition = new Point(0, scroll);
        RefreshBar();
    }

    /// <summary>
    /// Charge l'image d'un jeu en arrière-plan et met à jour
    /// l'item sidebar correspondant dès que c'est prêt.
    /// </summary>
    private async void PreloadGameImage(DetectedGame game)
    {
        await Task.Run(() =>
        {
            // 1 — Cache Steam local
            if (game.SteamAppId.HasValue)
            {
                var img = FindSteamCacheImage(game.SteamAppId.Value);
                if (img != null) { game.GameImage = img; UpdateSidebarImage(game, img); return; }
            }

            // 2 — Images dans le dossier
            if (!string.IsNullOrEmpty(game.InstallFolder) && Directory.Exists(game.InstallFolder))
            {
                string[] names = { "header.jpg","header.png","cover.jpg","cover.png",
                                   "box.jpg","box.png","banner.jpg","capsule.jpg" };
                foreach (var name in names)
                {
                    string p = Path.Combine(game.InstallFolder, name);
                    if (!File.Exists(p)) continue;
                    try
                    {
                        var img = Image.FromFile(p);
                        game.GameImage = img;
                        UpdateSidebarImage(game, img);
                        return;
                    }
                    catch { }
                }
            }

            // 3 — Icône du .exe
            if (!string.IsNullOrEmpty(game.ExecutablePath) && File.Exists(game.ExecutablePath))
            {
                try
                {
                    using var ico = new System.Drawing.Icon(game.ExecutablePath, 256, 256);
                    var img = ico.ToBitmap();
                    game.GameImage = img;
                    UpdateSidebarImage(game, img);
                    return;
                }
                catch
                {
                    try
                    {
                        var ico = Icon.ExtractAssociatedIcon(game.ExecutablePath);
                        if (ico != null)
                        {
                            var img = ico.ToBitmap();
                            game.GameImage = img;
                            UpdateSidebarImage(game, img);
                            return;
                        }
                    }
                    catch { }
                }
            }
        });

        // 4 — Steam en ligne si toujours rien
        if (game.GameImage == null && game.SteamAppId.HasValue)
            await LoadSteamImageAsync(game);
    }

    private void UpdateSidebarImage(DetectedGame game, Image img)
    {
        if (!IsHandleCreated) return;
        Invoke(() =>
        {
            foreach (Control c in _gameList.Controls)
                if (c is GameSidebarItem si && si.Tag == game)
                { si.GameImage = img; si.Invalidate(); break; }
        });
    }

    private void OnGameClick(object? sender, EventArgs e)
    {
        if (sender is not GameSidebarItem item) return;
        if (item.Tag is not DetectedGame game) return;
        _selected = game;
        foreach (Control c in _gameList.Controls)
            if (c is GameSidebarItem gi) { gi.IsSelected = gi.Tag == game; gi.Invalidate(); }
        UpdateBanner(game);
        if (game.IsRunning && game.RunningPid.HasValue) AttachTo(game.RunningPid.Value);
        LoadProfileForGame(game);
    }

    private void LoadProfileForGame(DetectedGame game)
    {
        _cheatsArea.Controls.Clear();
        _cheatsArea.Invalidate();
        UnregisterSortHotkeys();
        _sorts = new();

        var profile = _profile = ProfileRegistry.FindProfile(game.ExecutableName);
        if (profile == null) { _ce?.UnloadProfile(); return; }

        var sorts = _sorts = profile.GetSorts();
        if (_ce != null && _pm.IsAttached) _ce.LoadProfile(profile);
        RegisterSortHotkeys();
        _lblSorts.Text = $"{sorts.Count} SORT{(sorts.Count > 1 ? "S" : "")}";

        var resetSorts = sorts.Where(s => s.FreezeValue == "RESET_ALL").ToList();
        var otherGroups = sorts.Where(s => s.FreezeValue != "RESET_ALL")
                               .GroupBy(s => s.Category)
                               .OrderBy(g => (int)g.Key)
                               .ToList();

        int y    = 10;
        int cardW = 220;
        int cardH = 90;
        int gap   = 8;

        if (resetSorts.Any())
        {
            y = AddCategoryLabel("RÉINITIALISER", y);
            y = AddSortCards(resetSorts, y, cardW, cardH, gap, true);
        }

        foreach (var group in otherGroups)
        {
            y = AddCategoryLabel(CategoryLabel(group.Key), y);
            y = AddSortCards(group.ToList(), y, cardW, cardH, gap, false);
        }
    }

    private static string CategoryLabel(SortCategory c) => c switch
    {
        SortCategory.Survie       => "SURVIE",
        SortCategory.Ressources   => "RESSOURCES",
        SortCategory.Deplacement  => "DÉPLACEMENT",
        SortCategory.Combat       => "COMBAT",
        _                         => "DIVERS",
    };

    private int AddCategoryLabel(string title, int y)
    {
        var lbl = new Label
        {
            Text      = title,
            AutoSize  = false,
            Location  = new Point(12, y),
            Size      = new Size(_cheatsArea.Width - 32, 20),
            ForeColor = Color.FromArgb(110, GT.Cyan),
            BackColor = Color.Transparent,
            Font      = GT.Orb(7f, false),
        };
        lbl.Paint += (s, e) =>
        {
            int lx = (int)e.Graphics.MeasureString(lbl.Text, lbl.Font).Width + 8;
            using var pen = new Pen(Color.FromArgb(25, 0, 180, 210), 0.5f);
            e.Graphics.DrawLine(pen, lx, lbl.Height / 2, lbl.Width - 10, lbl.Height / 2);
        };
        _cheatsArea.Controls.Add(lbl);
        return y + 26;
    }

    private int AddSortCards(List<SortDefinition> sorts, int y,
        int cardW, int cardH, int gap, bool fullWidth)
    {
        int avail = _cheatsArea.Width - 32;
        int cols  = fullWidth ? 1 : Math.Max(1, avail / (cardW + gap));
        int col   = 0;

        foreach (var sort in sorts)
        {
            int w = fullWidth ? avail : cardW;
            var card = new CheatCard
            {
                CheatName = sort.Name,
                CheatDesc = sort.Description,
                Hotkey    = sort.Hotkey,
                ValueText = sort.Type == SortType.Slider
                    ? $"{sort.SliderDefault}{sort.SliderUnit}"
                    : (sort.FreezeValue is "RESET_ALL" or "STEAM_ACHIEVEMENTS" ? "" : sort.FreezeValue),
                Location  = new Point(12 + col * (w + gap), y),
                Size      = new Size(w, cardH),
                Tag       = sort,
            };

            card.ToggleChanged += (s, active) =>
            {
                sort.IsActive = active;
                if (sort.FreezeValue == "RESET_ALL" && active)
                    ResetAllSorts();
                else if (active && sort.FreezeValue.StartsWith("SAVE:"))
                    ApplySaveSort(sort);
                else if (_ce?.CurrentProfile != null && IsMemorySort(sort))
                    ApplyMemorySort(sort, active);
            };

            _cheatsArea.Controls.Add(card);
            col++;
            if (col >= cols) { col = 0; y += cardH + gap; }
        }
        if (col > 0) y += cardH + gap;
        return y + 6;
    }

    private void ResetAllSorts()
    {
        foreach (Control c in _cheatsArea.Controls)
            if (c is CheatCard card)
                card.SetOn(false);   // déclenche ToggleChanged → sort.IsActive = false, patchs restaurés
    }

    // Sorts qui agissent en mémoire (les autres éditent la sauvegarde ou sont des commandes)
    private static bool IsMemorySort(SortDefinition s) =>
        !s.FreezeValue.StartsWith("SAVE:") && s.FreezeValue is not ("RESET_ALL" or "STEAM_ACHIEVEMENTS");

    // Affiche la valeur actuelle lue dans le jeu (« 12 500 ») sur chaque carte
    private void RefreshLiveValues()
    {
        if (_ce?.CurrentProfile == null || !_pm.IsAlive()) return;
        foreach (var card in _cheatsArea.Controls.OfType<CheatCard>())
        {
            if (card.Tag is not SortDefinition s || !IsMemorySort(s) || s.Type is SortType.Patch or SortType.Slider) continue;
            var v = _ce.ReadCurrentValue(s);
            if (v == "?" || v == card.ValueText) continue;
            card.ValueText = v;
            card.Invalidate();
        }
    }

    // Toggle/Slider sont réécrits par le timer freeze ; Value et Patch s'appliquent ici une fois.
    private void ApplyMemorySort(SortDefinition sort, bool active)
    {
        bool ok = sort.Type switch
        {
            SortType.Value => !active || _ce!.ApplyValue(sort),
            SortType.Patch => active ? _ce!.ApplyPatch(sort) : _ce!.RestorePatch(sort),
            _              => true,
        };
        _lblBar.Text      = ok ? $"{(active ? "✅" : "⏹")}  {sort.Name}" : $"❌  {sort.Name} : adresse introuvable";
        _lblBar.ForeColor = ok ? GT.Green : GT.PinkLight;
    }

    // ── Raccourcis clavier ────────────────────────────────────────────────────
    // Format accepté : "F1", "Ctrl+F2", "Alt+Shift+N"...
    private void RegisterSortHotkeys()
    {
        if (!IsHandleCreated) return;
        int id = 1;
        foreach (var sort in _sorts.Where(s => !string.IsNullOrWhiteSpace(s.Hotkey)))
        {
            uint mods = 0x4000; // MOD_NOREPEAT : maintenir la touche ne bascule pas en boucle
            Keys key = Keys.None;
            foreach (var part in sort.Hotkey.Split('+', StringSplitOptions.TrimEntries))
            {
                switch (part.ToLowerInvariant())
                {
                    case "ctrl":  mods |= 0x2; break;
                    case "alt":   mods |= 0x1; break;
                    case "shift": mods |= 0x4; break;
                    default: Enum.TryParse(part, true, out key); break;
                }
            }
            if (key == Keys.None) continue;
            if (RegisterHotKey(Handle, id, mods, (uint)key)) _hotkeys[id] = sort;
            id++;
        }
    }

    private void UnregisterSortHotkeys()
    {
        foreach (var id in _hotkeys.Keys) UnregisterHotKey(Handle, id);
        _hotkeys.Clear();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && _hotkeys.TryGetValue((int)m.WParam, out var sort))
        {
            _cheatsArea.Controls.OfType<CheatCard>().FirstOrDefault(c => c.Tag == sort)?.Flip();
            System.Media.SystemSounds.Asterisk.Play();  // retour sonore : on ne voit pas l'appli en jeu
        }
        base.WndProc(ref m);
    }

    private void ApplySaveSort(SortDefinition sort, float sliderValue = 1f)
    {
        // Avertissement : le jeu doit être fermé
        if (_pm.IsAttached && _pm.IsAlive())
        {
            var result = MessageBox.Show(
                $"⚠️ Pour appliquer '{sort.Name}', le jeu doit être fermé.\n\n" +
                "Ferme Adventure Capitalist maintenant puis clique OK.",
                "Grimoire — Ferme le jeu",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning);

            if (result != DialogResult.OK) return;
        }

        bool ok = AdventureCapitalistProfile.ApplySaveSort(sort, sliderValue);

        if (ok)
        {
            _lblBar.Text      = $"✅  '{sort.Name}' appliqué — Relance le jeu !";
            _lblBar.ForeColor = GT.Green;
        }
        else
        {
            _lblBar.Text      = $"❌  Échec — Fichier de sauvegarde introuvable";
            _lblBar.ForeColor = GT.PinkLight;

            MessageBox.Show(
                "Fichier de sauvegarde introuvable.\n\n" +
                "Vérifie que Adventure Capitalist est installé sur Steam\n" +
                "et que tu as déjà lancé le jeu au moins une fois.",
                "Grimoire — Erreur",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void AttachTo(int pid)
    {
        try
        {
            _pm.Attach(pid);
            _mm = new MemoryManager(_pm);
            _ms = new MemoryScanner(_pm, _mm);
            _ce = new CheatEngine(_pm, _mm);
            if (_profile != null) _ce.LoadProfile(_profile);
            _ms.ScanStatus += msg => { if (IsHandleCreated) Invoke(() => _lblBar.Text = "🔍  " + msg); };
            RefreshBar();
            _cheatsArea.Invalidate();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Grimoire", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void UpdateBanner(DetectedGame game)
    {
        _lblTitle.Text = game.Name.ToUpper();
        foreach (var c in new Control[] { _picGame, _btnFavorite, _lblStatus, _lblBits, _lblSorts }) c.Visible = true;
        _lblSorts.Text = "0 SORT";   // LoadProfileForGame le remplace si un profil existe
        UpdateRunState(game);
        UpdateFavoriteButton();

        // Charge la jaquette et l'image de fond
        LoadGameImage(game);
        _heroImage = game.SteamAppId.HasValue ? FindSteamCacheFile(game.SteamAppId.Value, "library_hero.jpg") : null;
        _banner.Invalidate(true);
    }

    // Voyant + bouton d'action, appelé aussi par le timer quand le jeu démarre ou s'arrête
    private void UpdateRunState(DetectedGame game)
    {
        bool connected = game.IsRunning && _pm.IsAttached && _pm.IsAlive();
        (_lblStatus.Text, _lblStatus.ForeColor, _lblStatus.BackColor) =
            connected       ? ("● CONNECTÉ",    GT.Green,     Color.FromArgb(16, 61, 214, 140)) :
            game.IsRunning  ? ("● EN COURS",    GT.Yellow,    Color.FromArgb(16, 212, 184, 64)) :
                              ("● ARRÊTÉ",      GT.TextMuted, Color.FromArgb(14, 80, 105, 150));
        _lblBits.Text = _pm.IsAttached ? (_pm.Is64Bit ? "64-BIT" : "32-BIT") : "—";

        _btnLaunch.Visible = true;
        _btnLaunch.Text    = game.IsRunning ? "●  JEU LANCÉ" : "▶  LANCER LE JEU";
        _btnLaunch.Style   = game.IsRunning ? RetroButton.BtnStyle.Cyan : RetroButton.BtnStyle.Pink;
        _btnLaunch.Enabled = !game.IsRunning;
        _btnLaunch.Invalidate();
    }

    private void OnLaunchGame(object? sender, EventArgs e)
    {
        if (_selected == null || _selected.IsRunning) return;
        try
        {
            var psi = _selected.SteamAppId.HasValue
                ? new System.Diagnostics.ProcessStartInfo($"steam://rungameid/{_selected.SteamAppId}") { UseShellExecute = true }
                : new System.Diagnostics.ProcessStartInfo(_selected.ExecutablePath)
                  { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(_selected.ExecutablePath) ?? "" };
            System.Diagnostics.Process.Start(psi);
            _lblBar.Text      = $"🚀  Lancement de {_selected.Name}… le Grimoire s'y connectera tout seul";
            _lblBar.ForeColor = GT.Yellow;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible de lancer le jeu :\n{ex.Message}", "Grimoire",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>
    /// Cherche une image pour le jeu dans cet ordre :
    /// 1. header.jpg / cover.jpg / box.jpg dans le dossier du jeu
    /// 2. Icône extraite du .exe
    /// 3. Image Steam via AppID (si disponible)
    /// </summary>
    private void LoadGameImage(DetectedGame game)
    {
        _picGame.Image = null;

        // Fonction locale pour appliquer l'image partout
        void Apply(Image img)
        {
            _picGame.Image = img;
            _picGame.Invalidate(); // Force le repaint haute qualité
            game.GameImage = img;
            foreach (Control c in _gameList.Controls)
                if (c is GameSidebarItem si && si.Tag == game)
                { si.GameImage = img; si.Invalidate(); }
        }

        // 1 — Cache Steam local (meilleure qualité, pas de réseau)
        if (game.SteamAppId.HasValue)
        {
            var steamImg = FindSteamCacheImage(game.SteamAppId.Value);
            if (steamImg != null) { Apply(steamImg); return; }
        }

        // 2 — Images dans le dossier d'installation
        if (!string.IsNullOrEmpty(game.InstallFolder) && Directory.Exists(game.InstallFolder))
        {
            string[] names = { "header.jpg","header.png","cover.jpg","cover.png",
                               "box.jpg","box.png","banner.jpg","capsule.jpg" };
            foreach (var name in names)
            {
                string p = Path.Combine(game.InstallFolder, name);
                if (!File.Exists(p)) continue;
                try { Apply(Image.FromFile(p)); return; } catch { }
            }
        }

        // 3 — Icône haute résolution du .exe
        if (!string.IsNullOrEmpty(game.ExecutablePath) && File.Exists(game.ExecutablePath))
        {
            try
            {
                using var ico = new System.Drawing.Icon(game.ExecutablePath, 256, 256);
                Apply(ico.ToBitmap()); return;
            }
            catch
            {
                try
                {
                    var ico = Icon.ExtractAssociatedIcon(game.ExecutablePath);
                    if (ico != null) { Apply(ico.ToBitmap()); return; }
                }
                catch { }
            }
        }

        // 4 — Image Steam en ligne (async, fallback réseau)
        if (game.SteamAppId.HasValue)
            _ = LoadSteamImageAsync(game);
    }

    // Steam récent range ses images dans librarycache\<appid>\[sous-dossier hashé\]nom.jpg
    private Image? FindSteamCacheFile(int appId, string fileName)
    {
        string? steamPath =
            Microsoft.Win32.Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string
            ?? Microsoft.Win32.Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string;
        if (steamPath == null) return null;
        string dir = Path.Combine(steamPath, "appcache", "librarycache", appId.ToString());
        if (!Directory.Exists(dir)) return null;
        var path = Directory.EnumerateFiles(dir, fileName, SearchOption.AllDirectories).FirstOrDefault();
        if (path == null) return null;
        // Copie en mémoire : Image.FromFile verrouillerait le fichier de Steam
        try { using var ms = new MemoryStream(File.ReadAllBytes(path)); return Image.FromStream(ms); }
        catch { return null; }
    }

    private Image? FindSteamCacheImage(int appId)
    {
        var img = FindSteamCacheFile(appId, "library_600x900.jpg")
               ?? FindSteamCacheFile(appId, "library_capsule.jpg")
               ?? FindSteamCacheFile(appId, "library_header.jpg");
        if (img != null) return img;

        string? steamPath =
            Microsoft.Win32.Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string
            ?? Microsoft.Win32.Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string;

        if (steamPath == null) return null;
        string cacheDir = Path.Combine(steamPath, "appcache", "librarycache");
        if (!Directory.Exists(cacheDir)) return null;

        string[] suffixes = {
            "_library_600x900.jpg",
            "_header.jpg",
            "_logo.png",
            "_icon.jpg",
        };
        foreach (var suffix in suffixes)
        {
            string path = Path.Combine(cacheDir, appId + suffix);
            if (!File.Exists(path)) continue;
            try { return Image.FromFile(path); } catch { }
        }
        return null;
    }

    private async Task LoadSteamImageAsync(DetectedGame game)
    {
        try
        {
            string url = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{game.SteamAppId}/library_600x900.jpg";
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var bytes = await client.GetByteArrayAsync(url);
            using var ms = new MemoryStream(bytes);
            var img = Image.FromStream(ms);
            if (IsHandleCreated)
                Invoke(() =>
                {
                    _picGame.Image = img;
                    game.GameImage = img;
                    foreach (Control c in _gameList.Controls)
                        if (c is GameSidebarItem si && si.Tag == game)
                        { si.GameImage = img; si.Invalidate(); }
                });
        }
        catch { }
    }

    private void RefreshBar()
    {
        if (_pm.IsAttached && _pm.IsAlive())
        {
            _lblBar.Text      = $"🟢  Attaché à {_pm.AttachedProcess?.ProcessName}  |  PID {_pm.AttachedProcess?.Id}  |  {(_pm.Is64Bit ? "64-bit" : "32-bit")}";
            _lblBar.ForeColor = GT.Green;
        }
        else
        {
            _lblBar.Text      = "⚡  Aucun processus attaché";
            _lblBar.ForeColor = GT.TextMuted;
        }
    }

    // ── Liste persistante des jeux ajoutés manuellement ─────────────────────
    private List<DetectedGame> _manualGames = new();

    // ── Événements boutons ────────────────────────────────────────────────────

    /// <summary>
    /// Ouvre l'explorateur de fichiers pour choisir un .exe de jeu.
    /// Ajoute le jeu à la sidebar et s'y attache s'il tourne déjà.
    /// </summary>
    private void OnToggleFavorite(object? sender, EventArgs e)
    {
        if (_selected == null) return;

        bool isFav = _favorites.Contains(_selected.ExecutableName);
        if (isFav)
        {
            _favorites.Remove(_selected.ExecutableName);
            _btnFavorite.Text      = "☆";
            _btnFavorite.ForeColor = Color.FromArgb(100, GT.Yellow);
        }
        else
        {
            _favorites.Add(_selected.ExecutableName);
            _btnFavorite.Text      = "★";
            _btnFavorite.ForeColor = GT.Yellow;
        }

        // Reconstruit la sidebar avec les favoris en tête
        BuildSidebarList();
    }

    private void UpdateFavoriteButton()
    {
        if (_selected == null) return;
        bool isFav = _favorites.Contains(_selected.ExecutableName);
        _btnFavorite.Text      = isFav ? "★" : "☆";
        _btnFavorite.ForeColor = isFav ? GT.Yellow : Color.FromArgb(100, GT.Yellow);
    }

    private void OnAddGame(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title            = "Sélectionne l'exécutable du jeu",
            Filter           = "Exécutables (*.exe)|*.exe",
            FilterIndex      = 1,
            RestoreDirectory = true,
            Multiselect      = false,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        string exePath  = dialog.FileName;
        string exeName  = Path.GetFileName(exePath);
        string gameName = Path.GetFileNameWithoutExtension(exePath);
        string folder   = Path.GetDirectoryName(exePath) ?? "";

        // Vérifie si ce jeu est déjà dans la liste
        bool alreadyExists = _games.Any(g =>
            g.ExecutableName.Equals(exeName, StringComparison.OrdinalIgnoreCase))
            || _manualGames.Any(g =>
            g.ExecutableName.Equals(exeName, StringComparison.OrdinalIgnoreCase));

        if (alreadyExists)
        {
            MessageBox.Show($"{gameName} est déjà dans ta liste.", "Grimoire",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Crée le jeu détecté
        var game = new DetectedGame
        {
            Name           = gameName,
            ExecutableName = exeName,
            ExecutablePath = exePath,
            InstallFolder  = folder,
            Source         = "Manuel",
            IsRunning      = false,
        };

        // Vérifie s'il tourne déjà
        var running = System.Diagnostics.Process.GetProcessesByName(
            Path.GetFileNameWithoutExtension(exeName));
        if (running.Length > 0)
        {
            game.IsRunning  = true;
            game.RunningPid = running[0].Id;
        }

        // Ajoute à la liste
        _manualGames.Add(game);
        _games.Add(game);

        // Reconstruit la sidebar
        BuildSidebarList();

        // Sélectionne automatiquement le jeu ajouté
        var addedItem = _gameList.Controls
            .OfType<GameSidebarItem>()
            .FirstOrDefault(i => i.Tag is DetectedGame g &&
                g.ExecutableName.Equals(exeName, StringComparison.OrdinalIgnoreCase));

        if (addedItem != null)
        {
            OnGameClick(addedItem, EventArgs.Empty);
        }

        // Attache automatiquement s'il tourne
        if (game.IsRunning && game.RunningPid.HasValue)
        {
            AttachTo(game.RunningPid.Value);
        }
        else
        {
            // Informe l'utilisateur que le jeu sera attaché au prochain lancement
            _lblBar.Text      = $"📁  {gameName} ajouté — Lance le jeu pour l'activer";
            _lblBar.ForeColor = GT.Yellow;
        }
    }

    private void OnSelectProcess(object? sender, EventArgs e) => OnAddGame(sender, e);

    private void OpenScanner(object? sender, EventArgs e)
    {
        if (_mm == null)
        {
            MessageBox.Show(
                "Attache-toi d'abord à un jeu !\n\nClique sur un jeu actif dans la sidebar.",
                "Grimoire", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var scanner = new ScannerForm(_pm, _mm);
        scanner.Show(this);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static string GuessEmoji(string name)
    {
        name = name.ToLower();
        if (name.Contains("red dead")) return "🤠";
        if (name.Contains("cyber"))    return "🌆";
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
        if (name.Contains("horizon")) return "🏹";
        if (name.Contains("star") || name.Contains("space")) return "🚀";
        return "🎮";
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _freezeT.Stop(); _updateT.Stop();
        UnregisterSortHotkeys();
        _pm.Dispose();
        base.OnFormClosing(e);
    }
}
