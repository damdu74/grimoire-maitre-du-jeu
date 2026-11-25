// ============================================================
//  MOTEUR DE TRICHES — Exécute les sorts d'un profil
//
//  Rôle :
//  - Résout les pointer chains pour trouver les adresses finales
//  - Applique les sorts (freeze, write, patch) via MemoryManager
//  - Appelé par le Timer freeze toutes les 100ms
// ============================================================

namespace GameTrainer.Core;

public class CheatEngine
{
    private readonly ProcessManager  _pm;
    private readonly MemoryManager   _mm;
    private GameProfile?             _profile;
    private IntPtr                   _moduleBase = IntPtr.Zero;

    public GameProfile? CurrentProfile => _profile;
    public bool HasProfile => _profile != null;

    public CheatEngine(ProcessManager pm, MemoryManager mm)
    {
        _pm = pm;
        _mm = mm;
    }

    // ── Charge un profil pour le jeu attaché ─────────────────────────────────

    public void LoadProfile(GameProfile profile)
    {
        _profile = profile;
        _moduleBase = GetModuleBase(profile.ModuleName);
    }

    public void UnloadProfile()
    {
        _profile = null;
        _moduleBase = IntPtr.Zero;
    }

    // ── Récupère l'adresse de base du module principal du jeu ─────────────────
    // C'est le point de départ pour résoudre les pointer chains.
    // Elle change à chaque lancement (ASLR) → doit être lue dynamiquement.

    private IntPtr GetModuleBase(string moduleName)
    {
        try
        {
            var process = _pm.AttachedProcess;
            if (process == null) return IntPtr.Zero;

            foreach (System.Diagnostics.ProcessModule mod in process.Modules)
            {
                if (mod.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
                    return mod.BaseAddress;
            }
        }
        catch { }
        return IntPtr.Zero;
    }

    // ── Résout l'adresse finale d'un sort ─────────────────────────────────────

    public IntPtr ResolveAddress(SortDefinition sort)
    {
        if (_moduleBase == IntPtr.Zero) return IntPtr.Zero;

        IntPtr baseAddr = _moduleBase + (nint)sort.BaseOffset;

        if (sort.Offsets.Length == 0)
            return baseAddr;

        return _mm.ResolvePointerChain(baseAddr, sort.Offsets);
    }

    // ── Exécution du freeze (appelé par le Timer 100ms) ───────────────────────

    // Reçoit la liste affichée : GetSorts() recrée des objets neufs à chaque appel,
    // donc leur IsActive serait toujours false.
    public void ExecuteFreeze(IEnumerable<SortDefinition> sorts)
    {
        if (_profile == null || _moduleBase == IntPtr.Zero || !_pm.IsAlive()) return;

        foreach (var sort in sorts.Where(s => s.IsActive))
        {
            try
            {
                switch (sort.Type)
                {
                    case SortType.Toggle:
                        ApplyToggle(sort);
                        break;
                    case SortType.Slider:
                        ApplySlider(sort);
                        break;
                }
            }
            catch { }
        }
    }

    // ── Toggle : écrit la valeur gelée en boucle ─────────────────────────────

    private void ApplyToggle(SortDefinition sort)
    {
        var addr = ResolveAddress(sort);
        if (addr == IntPtr.Zero) return;

        switch (sort.DataType)
        {
            case ScanDataType.Int32:
                if (int.TryParse(sort.FreezeValue, out int vi))
                    _mm.WriteInt(addr, vi);
                break;
            case ScanDataType.Float:
                if (float.TryParse(sort.FreezeValue, out float vf))
                    _mm.WriteFloat(addr, vf);
                break;
            case ScanDataType.Double:
                if (double.TryParse(sort.FreezeValue, out double vd))
                    _mm.WriteDouble(addr, vd);
                break;
            case ScanDataType.Int64:
                if (long.TryParse(sort.FreezeValue, out long vl))
                    _mm.WriteLong(addr, vl);
                break;
        }
    }

    // ── Slider : écrit la valeur choisie par l'utilisateur ───────────────────

    private void ApplySlider(SortDefinition sort)
    {
        var addr = ResolveAddress(sort);
        if (addr == IntPtr.Zero) return;

        switch (sort.DataType)
        {
            case ScanDataType.Float:
                _mm.WriteFloat(addr, sort.CurrentSliderValue);
                break;
            case ScanDataType.Int32:
                _mm.WriteInt(addr, (int)sort.CurrentSliderValue);
                break;
        }
    }

    // ── Value : écriture unique (quand l'utilisateur active) ─────────────────

    public bool ApplyValue(SortDefinition sort)
    {
        var addr = ResolveAddress(sort);
        if (addr == IntPtr.Zero) return false;

        switch (sort.DataType)
        {
            case ScanDataType.Int32:
                return int.TryParse(sort.FreezeValue, out int vi)
                    && _mm.WriteInt(addr, vi);
            case ScanDataType.Float:
                return float.TryParse(sort.FreezeValue, out float vf)
                    && _mm.WriteFloat(addr, vf);
            case ScanDataType.Double:
                return double.TryParse(sort.FreezeValue, out double vd)
                    && _mm.WriteDouble(addr, vd);
        }
        return false;
    }

    // ── Patch : modifie des octets de code ───────────────────────────────────

    public bool ApplyPatch(SortDefinition sort)
    {
        var addr = ResolveAddress(sort);
        if (addr == IntPtr.Zero) return false;
        // Les octets d'origine doivent être là : sinon le jeu a été mis à jour, on ne touche à rien
        var cur = _mm.ReadBytes(addr, sort.PatchBytes.Length);
        if (cur.SequenceEqual(sort.PatchBytes)) return true;
        if (sort.OriginalBytes.Length > 0 && !cur.SequenceEqual(sort.OriginalBytes)) return false;
        return _mm.WriteBytes(addr, sort.PatchBytes);
    }

    public bool RestorePatch(SortDefinition sort)
    {
        var addr = ResolveAddress(sort);
        if (addr == IntPtr.Zero) return false;
        if (!_mm.ReadBytes(addr, sort.PatchBytes.Length).SequenceEqual(sort.PatchBytes)) return true;
        return _mm.WriteBytes(addr, sort.OriginalBytes);
    }

    // ── Lecture de la valeur actuelle (pour affichage dans les cartes) ────────

    public string ReadCurrentValue(SortDefinition sort)
    {
        try
        {
            var addr = ResolveAddress(sort);
            if (addr == IntPtr.Zero) return "?";

            return sort.DataType switch
            {
                ScanDataType.Int32  => _mm.ReadInt(addr).ToString("N0"),
                ScanDataType.Float  => _mm.ReadFloat(addr).ToString("F1"),
                ScanDataType.Double => _mm.ReadDouble(addr).ToString("F1"),
                ScanDataType.Int64  => _mm.ReadLong(addr).ToString("N0"),
                _                   => "?"
            };
        }
        catch { return "?"; }
    }
}
