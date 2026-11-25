// ============================================================
//  CONTRÔLES VISUELS — Style Retro Gamer
//  Tous les contrôles sont dessinés avec GDI+ directement.
// ============================================================

using System.Drawing.Drawing2D;
using System.Drawing.Text;
using GameTrainer.Models;

namespace GameTrainer.UI;

// ══════════════════════════════════════════════════════════════════════════════
//  TOGGLE SWITCH
// ══════════════════════════════════════════════════════════════════════════════
public class RetroToggle : Control
{
    private bool _on = false;
    private float _thumb = 3f;
    private System.Windows.Forms.Timer _anim = new() { Interval = 14 };

    public bool IsOn
    {
        get => _on;
        set { if (_on == value) return; _on = value; _anim.Start(); Toggled?.Invoke(this, _on); }
    }
    public event EventHandler<bool>? Toggled;

    public RetroToggle()
    {
        Size = new Size(42, 22);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.DoubleBuffer, true);
        Cursor = Cursors.Hand;
        _anim.Tick += (s, e) =>
        {
            float target = _on ? Width - 17f : 3f;
            float diff = target - _thumb;
            if (Math.Abs(diff) < 3f) { _thumb = target; _anim.Stop(); }
            else _thumb += diff * 0.35f;
            Invalidate();
        };
        Click += (s, e) => IsOn = !_on;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        // Fond
        var trackCol = _on
            ? Color.FromArgb(45, 0, 200, 230)
            : Color.FromArgb(18, 26, 58);
        GT.FillRound(g, new SolidBrush(trackCol), rect, Height / 2f);

        // Bordure
        var borderCol = _on
            ? Color.FromArgb(115, 0, 200, 230)
            : Color.FromArgb(40, 0, 180, 210);
        GT.DrawBorder(g, rect, borderCol, Height / 2f, 1f);

        // Thumb
        float sz = Height - 8f;
        var thumbBrush = new SolidBrush(_on ? GT.Cyan : Color.FromArgb(58, 74, 106));
        g.FillEllipse(thumbBrush, _thumb, 4f, sz, sz);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  BOUTON RETRO
// ══════════════════════════════════════════════════════════════════════════════
public class RetroButton : Control
{
    public enum BtnStyle { Pink, Cyan, Ghost }
    private bool _hov = false;
    public BtnStyle Style { get; set; } = BtnStyle.Pink;

    public RetroButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.DoubleBuffer, true);
        Cursor = Cursors.Hand;
        Size = new Size(120, 32);
    }

    protected override void OnMouseEnter(EventArgs e) { _hov = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { _hov = false; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        Color bg, border, textCol;
        switch (Style)
        {
            case BtnStyle.Pink:
                bg      = _hov ? Color.FromArgb(220, 55, 85) : GT.Pink;
                border  = GT.Pink;
                textCol = Color.White;
                break;
            case BtnStyle.Cyan:
                bg      = _hov ? Color.FromArgb(20, 0, 200, 230) : Color.FromArgb(12, 0, 180, 210);
                border  = GT.CyanDim;
                textCol = GT.Cyan;
                break;
            default: // Ghost
                bg      = _hov ? Color.FromArgb(10, 0, 180, 210) : Color.Transparent;
                border  = Color.FromArgb(70, 0, 180, 210);
                textCol = GT.Cyan;
                break;
        }

        GT.FillRound(g, new SolidBrush(bg), rect, 4f);
        GT.DrawBorder(g, rect, border, 4f, 1f);

        using var font = GT.Orb(8f);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var tb = new SolidBrush(textCol);
        g.DrawString(Text.ToUpper(), font, tb, new RectangleF(0, 0, Width, Height), sf);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  CARTE DE SORT
// ══════════════════════════════════════════════════════════════════════════════
public class CheatCard : UserControl
{
    public string CheatName { get; set; } = "Sort";
    public string CheatDesc { get; set; } = "";
    public string Hotkey    { get; set; } = "";
    public string ValueText { get; set; } = "";
    public bool   IsActive  { get; set; } = false;

    public event EventHandler<bool>? ToggleChanged;

    private RetroToggle _toggle;
    private bool _hov = false;

    public CheatCard()
    {
        _toggle = new RetroToggle { Location = new Point(0, 10) };
        _toggle.Toggled += (s, val) => { IsActive = val; Invalidate(); ToggleChanged?.Invoke(this, val); };

        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw, true);
        Controls.Add(_toggle);
        Size = new Size(235, 90);
        Cursor = Cursors.Hand;
        Click += (s, e) => Flip();
    }

    public void Flip() => _toggle.IsOn = !_toggle.IsOn;
    public void SetOn(bool on) => _toggle.IsOn = on;

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _toggle.Location = new Point(Width - _toggle.Width - 10, 10);
    }

    protected override void OnMouseEnter(EventArgs e) { _hov = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { _hov = false; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        // Fond
        Color bg = IsActive ? GT.BgCardOn : GT.BgCard;
        GT.FillRound(g, new SolidBrush(bg), rect, 7f);

        // Bordure
        Color border = IsActive ? Color.FromArgb(82, 0, 200, 230)
                     : _hov    ? Color.FromArgb(50, 0, 180, 210)
                                : Color.FromArgb(30, 0, 180, 210);
        GT.DrawBorder(g, rect, border, 7f, 1f);

        // Nom
        using var nameFont = GT.Body(10.5f, true);
        Color nameCol = IsActive ? GT.TextCyanOn : GT.TextMain;
        using var nameBrush = new SolidBrush(nameCol);
        g.DrawString(CheatName, nameFont, nameBrush,
            new RectangleF(10, 10, Width - 60, 18),
            new StringFormat { Trimming = StringTrimming.EllipsisCharacter });

        // Description
        if (!string.IsNullOrEmpty(CheatDesc))
        {
            using var descFont  = GT.Body(9f);
            using var descBrush = new SolidBrush(IsActive
                ? Color.FromArgb(128, 100, 200, 220)
                : Color.FromArgb(100, 160, 185, 220));
            g.DrawString(CheatDesc, descFont, descBrush,
                new RectangleF(10, 32, Width - 24, 18));
        }

        // Hotkey badge
        if (!string.IsNullOrEmpty(Hotkey))
        {
            using var hkFont = GT.Orb(7f);
            var hkSize = g.MeasureString(Hotkey, hkFont);
            var hkRect = new Rectangle(10, Height - 24, (int)hkSize.Width + 14, 16);

            Color hkBg  = IsActive ? Color.FromArgb(20, 0, 200, 230) : Color.FromArgb(12, 0, 0, 0);
            Color hkBrd = IsActive ? Color.FromArgb(64, 0, 200, 230) : Color.FromArgb(25, 160, 185, 220);
            Color hkTxt = IsActive ? GT.Cyan : GT.TextMuted;

            GT.FillRound(g, new SolidBrush(hkBg), hkRect, 3f);
            GT.DrawBorder(g, hkRect, hkBrd, 3f, 0.5f);
            using var hkBrush = new SolidBrush(hkTxt);
            g.DrawString(Hotkey, hkFont, hkBrush,
                new RectangleF(hkRect.X + 7, hkRect.Y + 1, hkRect.Width, hkRect.Height));
        }

        // Valeur
        if (!string.IsNullOrEmpty(ValueText))
        {
            using var valFont  = GT.Orb(8f);
            using var valBrush = new SolidBrush(GT.Yellow);
            var valSf = new StringFormat { Alignment = StringAlignment.Far };
            g.DrawString(ValueText, valFont, valBrush,
                new RectangleF(0, Height - 24, Width - 10, 16), valSf);
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  ITEM SIDEBAR — Entrée dans la liste des jeux
// ══════════════════════════════════════════════════════════════════════════════
public class GameSidebarItem : Control
{
    public string GameName   { get; set; } = "";
    public string GameEmoji  { get; set; } = "🎮";
    public bool   IsRunning  { get; set; } = false;
    public bool   IsSelected { get; set; } = false;
    public bool   HasCheats  { get; set; } = false;   // un profil de triches existe pour ce jeu
    public Image? GameImage  { get; set; } = null;

    private bool _hov = false;

    public GameSidebarItem()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.DoubleBuffer, true);
        Size = new Size(200, 50);
        Cursor = Cursors.Hand;
        BackColor = GT.BgPanel;
    }

    protected override void OnMouseEnter(EventArgs e) { _hov = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { _hov = false; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        var rect = new Rectangle(1, 1, Width - 2, Height - 2);

        if (IsSelected)
        {
            GT.FillRound(g, new SolidBrush(Color.FromArgb(26, 0, 200, 230)), rect, 6f);
            GT.DrawBorder(g, rect, Color.FromArgb(56, 0, 200, 230), 6f, 1f);
        }
        else if (_hov)
            GT.FillRound(g, new SolidBrush(Color.FromArgb(13, 0, 180, 210)), rect, 6f);

        // Thumbnail
        var iconRect = new Rectangle(8, (Height - 32) / 2, 32, 32);
        GT.FillRound(g, new SolidBrush(Color.FromArgb(20, 0, 180, 210)), iconRect, 5f);

        if (GameImage != null)
        {
            // Clip arrondi + haute qualité
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode   = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            using var clip = GT.RoundRect(iconRect, 5f);
            g.SetClip(clip);
            g.DrawImage(GameImage, iconRect);
            g.ResetClip();
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Default;
            g.PixelOffsetMode   = System.Drawing.Drawing2D.PixelOffsetMode.Default;
        }
        else
        {
            using var ef  = new Font("Segoe UI Emoji", 13f);
            var esf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(GameEmoji, ef, Brushes.White,
                new RectangleF(iconRect.X, iconRect.Y, iconRect.Width, iconRect.Height), esf);
        }

        GT.DrawBorder(g, iconRect, Color.FromArgb(30, 0, 180, 210), 5f, 0.5f);

        // Nom
        using var nameFont  = GT.Body(11f, true);
        using var nameBrush = new SolidBrush(IsSelected ? GT.TextMain : Color.FromArgb(200, GT.TextMain));
        g.DrawString(GameName, nameFont, nameBrush,
            new RectangleF(48, 8, Width - 56, 18),
            new StringFormat { Trimming = StringTrimming.EllipsisCharacter });

        // Statut
        using var statusFont = GT.Body(9f);
        if (IsRunning)
        {
            g.FillEllipse(new SolidBrush(GT.Green), 48, Height - 17, 6, 6);
            g.DrawString("Actif", statusFont, new SolidBrush(GT.Green),
                new RectangleF(58, Height - 20, Width - 66, 14));
        }
        else
        {
            g.DrawString("Inactif", statusFont, new SolidBrush(GT.TextMuted),
                new RectangleF(48, Height - 20, Width - 56, 14));
        }

        // Badge « TRICHES » à droite de la ligne de statut
        if (HasCheats)
        {
            using var bf = GT.Orb(6.5f);
            var bs = g.MeasureString("TRICHES", bf);
            var br = new Rectangle(Width - (int)bs.Width - 16, Height - 20, (int)bs.Width + 8, 14);
            GT.FillRound(g, new SolidBrush(Color.FromArgb(40, GT.Pink)), br, 3f);
            GT.DrawBorder(g, br, Color.FromArgb(120, GT.Pink), 3f, 0.5f);
            g.DrawString("TRICHES", bf, new SolidBrush(GT.PinkLight), br.X + 4, br.Y + 1);
        }
    }
}
