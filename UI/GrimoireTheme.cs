// ============================================================
//  THÈME — Le Grimoire du Maître du Jeu
//  Style : Retro Gamer — bleu marine profond, accents cyan/rose
//  RÈGLE : zéro text-shadow agressif, couleurs douces et lisibles
// ============================================================

using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace GameTrainer.UI;

public static class GT
{
    // ── Couleurs de fond ──────────────────────────────────────────────────────
    public static readonly Color BgApp     = Color.FromArgb(13,  27,  62);   // #0d1b3e
    public static readonly Color BgPanel   = Color.FromArgb( 7,  16,  40);   // #071028
    public static readonly Color BgCard    = Color.FromArgb( 7,  16,  40);   // #071028
    public static readonly Color BgCardOn  = Color.FromArgb(10,  22,  50);   // légèrement plus clair

    // ── Accents (sans glow) ───────────────────────────────────────────────────
    public static readonly Color Cyan      = Color.FromArgb( 93, 200, 221);  // #5dc8dd  doux
    public static readonly Color CyanDim   = Color.FromArgb( 58, 158, 184);  // #3a9eb8  foncé
    public static readonly Color CyanBorder= Color.FromArgb( 45, 110, 140);  // bordure
    public static readonly Color Pink      = Color.FromArgb(200,  48,  78);  // #c8304e  rose mat
    public static readonly Color PinkLight = Color.FromArgb(232, 112, 136);  // #e87088  texte badge
    public static readonly Color Yellow    = Color.FromArgb(212, 184,  64);  // #d4b840  jaune doré
    public static readonly Color Green     = Color.FromArgb( 61, 214, 140);  // #3dd68c  actif
    public static readonly Color Orange    = Color.FromArgb(232, 146,  60);  // #e8923c  en arrière-plan

    // ── Textes ────────────────────────────────────────────────────────────────
    public static readonly Color TextWhite  = Color.FromArgb(234, 242, 255); // #eaf2ff  titres
    public static readonly Color TextMain   = Color.FromArgb(184, 204, 238); // #b8ccee  corps
    public static readonly Color TextSoft   = Color.FromArgb(125, 155, 200); // #7d9bc8  secondaire
    public static readonly Color TextMuted  = Color.FromArgb( 80, 105, 150); // #506996  désactivé
    public static readonly Color TextCyanOn = Color.FromArgb(125, 216, 234); // #7dd8ea  actif cyan

    // ── Bordures ──────────────────────────────────────────────────────────────
    public static readonly Color BorderNorm = Color.FromArgb( 30,   0, 200, 230); // subtil
    public static readonly Color BorderOn   = Color.FromArgb( 80,   0, 200, 230); // actif
    public static readonly Color BorderGrid = Color.FromArgb( 25,   0, 200, 230); // grille

    // ── Polices ───────────────────────────────────────────────────────────────
    private static PrivateFontCollection? _pfc;
    private static FontFamily? _orbitron;

    public static FontFamily OrbitronFamily
    {
        get
        {
            if (_orbitron != null) return _orbitron;
            string exe = AppDomain.CurrentDomain.BaseDirectory;
            string[] paths = {
                Path.Combine(exe, "Orbitron-Bold.ttf"),
                Path.Combine(exe, "fonts", "Orbitron-Bold.ttf"),
            };
            foreach (var p in paths)
            {
                if (!File.Exists(p)) continue;
                _pfc ??= new PrivateFontCollection();
                _pfc.AddFontFile(p);
                _orbitron = _pfc.Families.LastOrDefault();
                if (_orbitron != null) return _orbitron;
            }
            // Segoe UI est présent sur tous les Windows 10/11 (Arial Narrow ne l'est pas → plantage)
            return _orbitron = new FontFamily("Segoe UI");
        }
    }

    // Orbitron — titres, badges, labels techniques
    public static Font Orb(float size, bool bold = true)
        => new(OrbitronFamily, size, bold ? FontStyle.Bold : FontStyle.Regular);

    // Rajdhani / Segoe UI — corps de texte, descriptions
    public static Font Body(float size, bool bold = false)
        => new("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular);

    // Consolas — valeurs numériques, adresses mémoire
    public static Font Mono(float size)
        => new("Consolas", size, FontStyle.Regular);

    // ── Utilitaires GDI+ ─────────────────────────────────────────────────────

    public static GraphicsPath RoundRect(Rectangle r, float radius)
    {
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        var p = new GraphicsPath();
        p.AddArc(r.X,            r.Y,            d, d, 180, 90);
        p.AddArc(r.Right - d,    r.Y,            d, d, 270, 90);
        p.AddArc(r.Right - d,    r.Bottom - d,   d, d,   0, 90);
        p.AddArc(r.X,            r.Bottom - d,   d, d,  90, 90);
        p.CloseFigure();
        return p;
    }

    public static void FillRound(Graphics g, Brush b, Rectangle r, float radius)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var p = RoundRect(r, radius);
        g.FillPath(b, p);
    }

    /// <summary>
    /// Dessine une bordure arrondie simple — PAS de glow agressif.
    /// </summary>
    public static void DrawBorder(Graphics g, Rectangle r, Color col,
        float radius = 8f, float width = 1f)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(col, width);
        using var path = RoundRect(r, radius);
        g.DrawPath(pen, path);
    }

    /// <summary>
    /// Ligne dégradée cyan→rose→jaune — utilisée en séparateur.
    /// Intensité basse, pas agressive.
    /// </summary>
    public static void DrawRainbowLine(Graphics g, int x, int y, int width, int alpha = 120)
    {
        using var brush = new LinearGradientBrush(
            new Point(x, y), new Point(x + width, y),
            Color.Transparent, Color.Transparent);
        var blend = new ColorBlend(5);
        blend.Colors = new[] {
            Color.Transparent,
            Color.FromArgb(alpha, CyanDim),
            Color.FromArgb(alpha, Pink),
            Color.FromArgb(alpha, Yellow),
            Color.Transparent
        };
        blend.Positions = new[] { 0f, 0.2f, 0.5f, 0.8f, 1f };
        brush.InterpolationColors = blend;
        using var pen = new Pen(brush, 1f);
        g.DrawLine(pen, x, y, x + width, y);
    }

    /// <summary>
    /// Grille de fond — subtile, pas éblouissante.
    /// </summary>
    public static void DrawGrid(Graphics g, Rectangle bounds)
    {
        using var pen = new Pen(Color.FromArgb(18, 0, 180, 210), 0.5f);
        int gs = 40;
        for (int y = bounds.Top; y < bounds.Bottom; y += gs)
            g.DrawLine(pen, bounds.Left, y, bounds.Right, y);
        for (int x = bounds.Left; x < bounds.Right; x += gs)
            g.DrawLine(pen, x, bounds.Top, x, bounds.Bottom);
    }

    /// <summary>
    /// Grille perspective en bas de fenêtre (style Retro Gamer).
    /// </summary>
    public static void DrawPerspectiveGrid(Graphics g, Rectangle bounds)
    {
        using var pen = new Pen(Color.FromArgb(22, 0, 180, 210), 0.5f);
        int rows = 8, cols = 12;
        int baseY = bounds.Bottom;
        int topY  = bounds.Bottom - 160;
        int cx    = bounds.Left + bounds.Width / 2;

        for (int r = 0; r <= rows; r++)
        {
            float t = (float)r / rows;
            int y = (int)(topY + t * (baseY - topY));
            float xL = cx - (bounds.Width * 0.5f) * t;
            float xR = cx + (bounds.Width * 0.5f) * t;
            g.DrawLine(pen, xL, y, xR, y);
        }
        for (int c = 0; c <= cols; c++)
        {
            float t = (float)c / cols;
            float xBase = bounds.Left + t * bounds.Width;
            float xTop  = cx + (xBase - cx) * 0f;
            g.DrawLine(pen, xTop, topY, xBase, baseY);
        }
    }

    /// <summary>Scanlines CRT très subtiles.</summary>
    public static void DrawScanlines(Graphics g, Rectangle bounds)
    {
        using var pen = new Pen(Color.FromArgb(14, 0, 0, 0), 1f);
        for (int y = bounds.Top; y < bounds.Bottom; y += 4)
            g.DrawLine(pen, bounds.Left, y, bounds.Right, y);
    }
}
