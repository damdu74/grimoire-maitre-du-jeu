// ============================================================
//  CONTRÔLES VISUELS PERSONNALISÉS — Style RetroKeys
//
//  RetroToggle   → Interrupteur on/off animé (style CSS toggle)
//  RetroButton   → Bouton avec bordure néon + glow au survol
//  CheatCard     → Carte de sort avec toggle, hotkey, valeur
//  GameSidebarItem → Entrée dans la liste de jeux (sidebar)
// ============================================================

using System.Drawing.Drawing2D;
using GameTrainer.Models;

namespace GameTrainer.UI;

// ══════════════════════════════════════════════════════════════════════════════
//  TOGGLE SWITCH — Interrupteur on/off animé
//
//  Reproduit exactement le style CSS de RetroKeys :
//  fond gris → fond violet quand actif
//  pastille blanche qui glisse de gauche à droite
// ══════════════════════════════════════════════════════════════════════════════
public class RetroToggle : Control
{
    private bool _isOn = false;
    private float _thumbX = 3f;         // Position animée du thumb
    private System.Windows.Forms.Timer _anim = new();

    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (_isOn == value) return;
            _isOn = value;
            _anim.Start();
            OnToggleChanged?.Invoke(this, _isOn);
        }
    }

    public event EventHandler<bool>? OnToggleChanged;

    public RetroToggle()
    {
        Size = new Size(44, 24);
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.DoubleBuffer, true);
        Cursor = Cursors.Hand;

        _anim.Interval = 16; // ~60fps
        _anim.Tick += (s, e) =>
        {
            float target = _isOn ? Width - 3 - 16f : 3f;
            float speed = 4f;
            if (Math.Abs(_thumbX - target) < speed) { _thumbX = target; _anim.Stop(); }
            else _thumbX += (_thumbX < target ? speed : -speed);
            Invalidate();
        };

        Click += (s, e) => IsOn = !_isOn;
        _thumbX = 3f;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        // Fond du toggle
        Color trackColor = _isOn
            ? Color.FromArgb(155, 48, 255)   // Violet actif
            : Color.FromArgb(50, 40, 70);     // Gris sombre inactif

        GrimoireTheme.FillRoundedRect(g, new SolidBrush(trackColor), rect, Height / 2f);

        // Bordure néon si actif
        if (_isOn)
        {
            using var borderPen = new Pen(Color.FromArgb(120, 155, 48, 255), 1f);
            using var path = GrimoireTheme.RoundedRect(rect, Height / 2f);
            g.DrawPath(borderPen, path);
        }

        // Thumb (pastille blanche)
        float thumbY = 4f;
        float thumbSize = Height - 8f;
        using var thumbBrush = new SolidBrush(Color.White);
        g.FillEllipse(thumbBrush, _thumbX, thumbY, thumbSize, thumbSize);

        // Petit glow sur le thumb quand actif
        if (_isOn)
        {
            using var glowBrush = new SolidBrush(Color.FromArgb(60, 155, 48, 255));
            g.FillEllipse(glowBrush, _thumbX - 2, thumbY - 2, thumbSize + 4, thumbSize + 4);
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  BOUTON RÉTRO — Bordure néon + glow au survol
// ══════════════════════════════════════════════════════════════════════════════
public class RetroButton : Control
{
    public enum ButtonStyle { Pink, Cyan, Purple, Ghost }

    private bool _hovered = false;
    private ButtonStyle _style = ButtonStyle.Pink;

    public ButtonStyle Style
    {
        get => _style;
        set { _style = value; Invalidate(); }
    }

    public string? SubText { get; set; }

    public RetroButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.DoubleBuffer, true);
        Cursor = Cursors.Hand;
        Size = new Size(130, 34);
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        // Couleurs selon le style
        Color primary = _style switch
        {
            ButtonStyle.Cyan   => GrimoireTheme.Cyan,
            ButtonStyle.Purple => GrimoireTheme.Purple,
            ButtonStyle.Ghost  => GrimoireTheme.TextMuted,
            _                  => GrimoireTheme.Pink
        };

        // Fond
        Color bgColor = _style == ButtonStyle.Ghost
            ? Color.FromArgb(_hovered ? 25 : 0, primary)
            : Color.FromArgb(_hovered ? 80 : 50, primary);

        using var bgBrush = new SolidBrush(bgColor);
        GrimoireTheme.FillRoundedRect(g, bgBrush, rect, 6f);

        // Bordure néon
        if (_hovered)
            GrimoireTheme.DrawNeonBorder(g, rect, primary, 6f, _hovered ? 5f : 2f);
        else
        {
            using var borderPen = new Pen(Color.FromArgb(120, primary), 1f);
            using var path = GrimoireTheme.RoundedRect(rect, 6f);
            g.DrawPath(borderPen, path);
        }

        // Texte
        using var font = GrimoireTheme.FontUI(8f);
        var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        Color textColor = _hovered ? Color.White : primary;
        GrimoireTheme.DrawNeonText(g, Text.ToUpper(), font, primary, textColor,
            new RectangleF(0, 0, Width, Height), sf, _hovered ? 6f : 2f);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  CARTE DE SORT — Reproduit les .card de RetroKeys
//
//  Structure visuelle :
//  ┌─────────────────────────────┐
//  │  Nom du sort       [toggle] │
//  │  Description courte         │
//  │  [F1]            valeur     │
//  └─────────────────────────────┘
// ══════════════════════════════════════════════════════════════════════════════
public class CheatCard : UserControl
{
    // Données
    public string CheatName { get; set; } = "Sort sans nom";
    public string CheatDesc { get; set; } = "";
    public string Hotkey    { get; set; } = "";
    public string ValueText { get; set; } = "";
    public bool   IsActive  { get; set; } = false;

    public event EventHandler<bool>? ToggleChanged;

    private RetroToggle _toggle;
    private bool _hovered = false;

    public CheatCard()
    {
        _toggle = new RetroToggle
        {
            Location = new Point(Width - 54, 12)
        };
        _toggle.OnToggleChanged += (s, val) =>
        {
            IsActive = val;
            Invalidate();
            ToggleChanged?.Invoke(this, val);
        };

        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.DoubleBuffer |
                 ControlStyles.ResizeRedraw, true);

        Controls.Add(_toggle);
        Size = new Size(240, 95);
        Cursor = Cursors.Hand;

        Click += (s, e) => _toggle.IsOn = !_toggle.IsOn;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _toggle.Location = new Point(Width - _toggle.Width - 12, 12);
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        // Fond de la carte
        Color cardBg = IsActive
            ? Color.FromArgb(28, 20, 37)    // Légèrement violet si actif
            : Color.FromArgb(13, 0, 32);     // Fond card normal

        using var bgBrush = new SolidBrush(cardBg);
        GrimoireTheme.FillRoundedRect(g, bgBrush, rect, 8f);

        // Bordure
        Color borderColor = IsActive  ? GrimoireTheme.Purple :
                            _hovered  ? GrimoireTheme.Pink :
                                        GrimoireTheme.BorderPurple;
        float glowSize = (IsActive || _hovered) ? 4f : 0f;
        GrimoireTheme.DrawNeonBorder(g, rect, borderColor, 8f, glowSize);

        // Overlay dégradé (comme ::before dans le CSS)
        using var overlayBrush = new LinearGradientBrush(
            new Point(0, 0), new Point(Width, Height),
            Color.FromArgb(12, 155, 48, 255),
            Color.FromArgb(8, 0, 245, 255));
        GrimoireTheme.FillRoundedRect(g, overlayBrush, rect, 8f);

        // ── Nom du sort ───────────────────────────────────────────────────────
        Color nameColor = IsActive ? GrimoireTheme.TextMain : GrimoireTheme.TextMain;
        using var nameFont = GrimoireTheme.FontUI(8.5f);
        var nameRect = new RectangleF(12, 12, Width - 80, 20);
        using var nameBrush = new SolidBrush(nameColor);
        g.DrawString(CheatName, nameFont, nameBrush, nameRect,
            new StringFormat { Trimming = StringTrimming.EllipsisCharacter });

        // ── Description ───────────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(CheatDesc))
        {
            using var descFont = GrimoireTheme.FontBody(8.5f);
            using var descBrush = new SolidBrush(IsActive
                ? Color.FromArgb(160, GrimoireTheme.Purple)
                : GrimoireTheme.TextMuted);
            g.DrawString(CheatDesc, descFont, descBrush,
                new RectangleF(12, 35, Width - 24, 24));
        }

        // ── Hotkey badge ──────────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(Hotkey))
        {
            using var hkFont = GrimoireTheme.FontUI(7f);
            var hkSize = g.MeasureString(Hotkey, hkFont);
            var hkRect = new Rectangle(12, Height - 26, (int)hkSize.Width + 14, 18);

            // Fond du badge
            Color hkBg = IsActive
                ? Color.FromArgb(40, 155, 48, 255)
                : Color.FromArgb(20, 123, 107, 154);
            GrimoireTheme.FillRoundedRect(g, new SolidBrush(hkBg), hkRect, 3f);

            // Bordure du badge
            Color hkBorder = IsActive
                ? Color.FromArgb(100, GrimoireTheme.Purple)
                : Color.FromArgb(60, GrimoireTheme.TextMuted);
            using var hkPen = new Pen(hkBorder, 0.5f);
            using var hkPath = GrimoireTheme.RoundedRect(hkRect, 3f);
            g.DrawPath(hkPen, hkPath);

            // Texte du badge
            using var hkBrush = new SolidBrush(IsActive ? GrimoireTheme.Purple : GrimoireTheme.TextMuted);
            g.DrawString(Hotkey, hkFont, hkBrush,
                new RectangleF(hkRect.X + 7, hkRect.Y + 2, hkRect.Width, hkRect.Height));
        }

        // ── Valeur actuelle ───────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(ValueText))
        {
            using var valFont = GrimoireTheme.FontUI(7.5f);
            var valSf = new StringFormat { Alignment = StringAlignment.Far };
            Color valColor = IsActive ? GrimoireTheme.Cyan : GrimoireTheme.TextMuted;
            GrimoireTheme.DrawNeonText(g, ValueText, valFont,
                valColor, valColor,
                new RectangleF(0, Height - 26, Width - 12, 18),
                valSf, IsActive ? 4f : 0f);
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  ITEM SIDEBAR — Entrée dans la liste des jeux
// ══════════════════════════════════════════════════════════════════════════════
public class GameSidebarItem : Control
{
    public string GameName    { get; set; } = "";
    public string GameEmoji   { get; set; } = "🎮";
    public bool   IsRunning   { get; set; } = false;
    public bool   IsSelected  { get; set; } = false;

    private bool _hovered = false;

    public GameSidebarItem()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.DoubleBuffer, true);
        Size = new Size(200, 50);
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(1, 1, Width - 2, Height - 2);

        // Fond selon état
        if (IsSelected)
        {
            using var selBrush = new SolidBrush(Color.FromArgb(42, 31, 74));
            GrimoireTheme.FillRoundedRect(g, selBrush, rect, 6f);
            using var selBorderPen = new Pen(Color.FromArgb(80, GrimoireTheme.Purple), 1f);
            using var selPath = GrimoireTheme.RoundedRect(rect, 6f);
            g.DrawPath(selBorderPen, selPath);
        }
        else if (_hovered)
        {
            using var hoverBrush = new SolidBrush(Color.FromArgb(34, 34, 39));
            GrimoireTheme.FillRoundedRect(g, hoverBrush, rect, 6f);
        }

        // Icône emoji dans un carré
        var iconRect = new Rectangle(8, (Height - 34) / 2, 34, 34);
        using var iconBg = new SolidBrush(Color.FromArgb(30, GrimoireTheme.Purple));
        GrimoireTheme.FillRoundedRect(g, iconBg, iconRect, 5f);
        using var iconPen = new Pen(GrimoireTheme.BorderPurple, 0.5f);
        using var iconPath = GrimoireTheme.RoundedRect(iconRect, 5f);
        g.DrawPath(iconPen, iconPath);

        // Emoji
        using var emojiFont = new Font("Segoe UI Emoji", 14f);
        var emojiSf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        using var emojiBrush = new SolidBrush(GrimoireTheme.TextMain);
        g.DrawString(GameEmoji, emojiFont, emojiBrush,
            new RectangleF(iconRect.X, iconRect.Y, iconRect.Width, iconRect.Height), emojiSf);

        // Nom du jeu
        using var nameFont = GrimoireTheme.FontUI(7.5f);
        using var nameBrush = new SolidBrush(IsSelected ? GrimoireTheme.TextMain : Color.FromArgb(200, GrimoireTheme.TextMain));
        var nameSf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter };
        g.DrawString(GameName, nameFont, nameBrush,
            new RectangleF(50, 8, Width - 58, 18), nameSf);

        // Statut (en cours / inactif)
        using var statusFont = GrimoireTheme.FontBody(8.5f);
        if (IsRunning)
        {
            // Point vert + texte
            using var dotBrush = new SolidBrush(Color.FromArgb(74, 222, 128));
            g.FillEllipse(dotBrush, 50, Height - 18, 7, 7);
            using var statusBrush = new SolidBrush(Color.FromArgb(74, 222, 128));
            g.DrawString("Actif", statusFont, statusBrush,
                new RectangleF(62, Height - 20, Width - 70, 14));
        }
        else
        {
            using var statusBrush = new SolidBrush(GrimoireTheme.TextMuted);
            g.DrawString("Inactif", statusFont, statusBrush,
                new RectangleF(50, Height - 20, Width - 58, 14));
        }
    }
}
