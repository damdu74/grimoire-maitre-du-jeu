// ============================================================
//  SYSTÈME DE PROFILS DE TRICHES — Style WeMod / Wand
//
//  CONCEPT :
//  Chaque jeu a un "profil" (un fichier .cs) qui contient :
//  - Le nom de l'exe à détecter (ex: RDR2.exe)
//  - La liste des sorts (triches) pré-codés
//  - Les adresses mémoire (pointer chains) de chaque variable
//
//  Le Grimoire détecte l'exe au démarrage → charge le bon profil
//  → affiche les cartes de sorts → l'utilisateur toggle on/off.
//
//  Pour ajouter un jeu : créer une classe qui hérite de GameProfile.
// ============================================================

namespace GameTrainer.Core;

// ── Types de sorts ────────────────────────────────────────────────────────────

public enum SortType
{
    Toggle,   // On/Off avec freeze en boucle (vie infinie, argent...)
    Value,    // Écrit une valeur une seule fois (ajouter de l'argent)
    Slider,   // Valeur réglable min→max (vitesse, multiplicateur XP)
    Patch,    // Patch d'octets en mémoire (NOP pour désactiver une logique)
}

public enum SortCategory
{
    Survie,
    Ressources,
    Deplacement,
    Combat,
    Divers,
}

// ── Définition d'un sort ──────────────────────────────────────────────────────

public class SortDefinition
{
    // Infos affichées
    public string Name         { get; set; } = "";
    public string Description  { get; set; } = "";
    public SortCategory Category { get; set; } = SortCategory.Divers;
    public SortType Type       { get; set; } = SortType.Toggle;
    public string Hotkey       { get; set; } = "";   // Ex: "F1"

    // Pour Toggle et Value
    public ScanDataType DataType  { get; set; } = ScanDataType.Int32;
    public string FreezeValue     { get; set; } = "9999";  // valeur gelée

    // Pour Slider
    public float SliderMin     { get; set; } = 0.5f;
    public float SliderMax     { get; set; } = 5.0f;
    public float SliderDefault { get; set; } = 1.0f;
    public float SliderStep    { get; set; } = 0.5f;
    public string SliderUnit   { get; set; } = "×";

    // Pour Patch
    public byte[] PatchBytes    { get; set; } = Array.Empty<byte>(); // octets à écrire
    public byte[] OriginalBytes { get; set; } = Array.Empty<byte>(); // pour restaurer

    // Adresse mémoire — pointer chain
    // Format : [baseOffset, offset1, offset2, ..., offsetFinal]
    // Ex : module.BaseAddress + 0x1234 → +0x58 → +0x14 = valeur
    public long   BaseOffset { get; set; } = 0;
    public int[]  Offsets    { get; set; } = Array.Empty<int>();

    // État runtime (non sérialisé)
    public bool   IsActive   { get; set; } = false;
    public float  CurrentSliderValue { get; set; } = 1.0f;
    public string DisplayValue { get; set; } = "";
}

// ── Profil de jeu (classe de base) ───────────────────────────────────────────

public abstract class GameProfile
{
    // Nom de l'exe à détecter (insensible à la casse)
    public abstract string ExecutableName { get; }

    // Nom affiché dans le Grimoire
    public abstract string GameName { get; }

    // Emoji affiché dans la sidebar
    public virtual string Emoji => "🎮";

    // Version du jeu pour laquelle les adresses ont été vérifiées
    public virtual string GameVersion => "Toutes versions";

    // Liste des sorts pré-codés
    public abstract List<SortDefinition> GetSorts();

    // Nom du module de base (souvent le même que l'exe, parfois différent)
    // Ex: "RDR2.exe", "Cyberpunk2077.exe"
    public virtual string ModuleName => ExecutableName;
}

// ── Registre des profils ──────────────────────────────────────────────────────

public static class ProfileRegistry
{
    // Tous les profils disponibles
    // Quand tu codes un nouveau jeu, tu l'ajoutes ici.
    private static readonly List<GameProfile> _profiles = new()
    {
        new Profiles.AdventureCapitalistProfile(),
    };

    /// <summary>
    /// Trouve le profil correspondant à un exe détecté.
    /// Retourne null si aucun profil n'existe pour ce jeu.
    /// </summary>
    public static GameProfile? FindProfile(string executableName)
    {
        return _profiles.FirstOrDefault(p =>
            p.ExecutableName.Equals(executableName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Liste tous les jeux pour lesquels on a des triches.
    /// </summary>
    public static IReadOnlyList<GameProfile> AllProfiles => _profiles;

    /// <summary>
    /// Ajoute un profil dynamiquement (utilisé pour les tests).
    /// </summary>
    public static void Register(GameProfile profile)
    {
        if (!_profiles.Any(p => p.ExecutableName.Equals(
            profile.ExecutableName, StringComparison.OrdinalIgnoreCase)))
            _profiles.Add(profile);
    }
}
