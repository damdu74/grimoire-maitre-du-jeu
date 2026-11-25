// ============================================================
//  PROFIL — AdVenture Capitalist (Steam)
//  MÉTHODE : Édition directe du fichier de sauvegarde JSON
//  Fermer le jeu avant d'appliquer, puis relancer !
// ============================================================

using System.Globalization;

namespace GameTrainer.Core.Profiles;

public class AdventureCapitalistProfile : GameProfile
{
    public override string ExecutableName => "adventure-capitalist.exe";
    public override string GameName       => "AdVenture Capitalist";
    public override string Emoji          => "💰";
    public override string GameVersion    => "Steam";

    public static string? SavePath => SaveFileEditor.FindAdventureCapitalistSave();

    public override List<SortDefinition> GetSorts() => new()
    {
        // ── RÉINITIALISER ────────────────────────────────────
        new() { Name="Réinitialiser tout",           Description="Remet tous les sorts à OFF",
                Category=SortCategory.Divers,        Type=SortType.Value,
                Hotkey="F12",                        FreezeValue="RESET_ALL" },

        // ── SUCCÈS ───────────────────────────────────────────
        new() { Name="Débloquer tous les succès",    Description="Débloque tous les succès Steam",
                Category=SortCategory.Divers,        Type=SortType.Value,
                Hotkey="F11",                        FreezeValue="STEAM_ACHIEVEMENTS" },

        // ── MODIFICATEURS ────────────────────────────────────
        new() { Name="Profits boostés",              Description="Multiplie tous les gains",
                Category=SortCategory.Ressources,    Type=SortType.Slider,
                Hotkey="F1",                         DataType=ScanDataType.Double,
                SliderMin=1f, SliderMax=100f,        SliderDefault=1f,
                SliderStep=1f, SliderUnit="×",       FreezeValue="SAVE:_purchasedMultiplier" },

        new() { Name="Vitesse rapide",               Description="Multiplie la vitesse de production",
                Category=SortCategory.Ressources,    Type=SortType.Slider,
                Hotkey="F2",                         DataType=ScanDataType.Double,
                SliderMin=1f, SliderMax=100f,        SliderDefault=1f,
                SliderStep=1f, SliderUnit="×",       FreezeValue="SAVE:fluxCapitalorMultiplier" },

        // ── RESSOURCES ───────────────────────────────────────
        new() { Name="Argent illimité",              Description="Met l'argent au maximum (1 Googol)",
                Category=SortCategory.Ressources,    Type=SortType.Value,
                Hotkey="F3",                         FreezeValue="SAVE:cash=1e+100" },

        new() { Name="Anges illimités",              Description="Met les anges au maximum",
                Category=SortCategory.Ressources,    Type=SortType.Value,
                Hotkey="F4",                         FreezeValue="SAVE:angels=1e+50" },

        new() { Name="Mégabucks illimités",          Description="Met les mégabucks au maximum",
                Category=SortCategory.Ressources,    Type=SortType.Value,
                Hotkey="F5",                         FreezeValue="SAVE:megaBucks=99999" },

        new() { Name="Or illimité",                  Description="Met l'or premium au maximum",
                Category=SortCategory.Ressources,    Type=SortType.Value,
                Hotkey="F6",                         FreezeValue="SAVE:gold=99999" },

        // ── PROGRESSION ──────────────────────────────────────
        new() { Name="Toutes entreprises au max",    Description="Met toutes les entreprises au niveau 1000",
                Category=SortCategory.Divers,        Type=SortType.Value,
                Hotkey="F7",                         FreezeValue="SAVE:VENTURES:owned=1000" },

        new() { Name="Niveaux max instantané",       Description="Déverrouille tous les niveaux",
                Category=SortCategory.Divers,        Type=SortType.Value,
                Hotkey="F8",                         FreezeValue="SAVE:VENTURES:totalOwned=9999" },
    };

    public static bool ApplySaveSort(SortDefinition sort, float sliderValue = 1f)
    {
        string? path = SavePath;
        if (path == null) return false;

        var editor = new SaveFileEditor(path);
        if (!editor.Load()) return false;

        bool ok = false;
        string cmd = sort.FreezeValue;

        if (cmd.StartsWith("SAVE:VENTURES:"))
        {
            var parts = cmd["SAVE:VENTURES:".Length..].Split('=');
            if (parts.Length == 2 && double.TryParse(parts[1], out double v))
                ok = editor.SetAllVentureValues(parts[0], v);
        }
        else if (cmd.StartsWith("SAVE:"))
        {
            var parts = cmd["SAVE:".Length..].Split('=');
            if (parts.Length == 2)
            {
                if (sort.Type == SortType.Slider)
                    ok = editor.SetValue(parts[0], (double)sliderValue);
                else if (double.TryParse(parts[1],
                    NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                    ok = editor.SetValue(parts[0], d);
                else
                    ok = editor.SetValue(parts[0], parts[1]);
            }
        }

        if (ok) editor.Save();
        return ok;
    }
}
