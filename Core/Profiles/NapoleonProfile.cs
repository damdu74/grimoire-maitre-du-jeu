namespace GameTrainer.Core.Profiles;

// Total War: NAPOLEON – Definitive Edition, Steam, Napoleon.exe 1.3.0.0 (32 bits)
// Instruction trouvée par 60881e5 sur fearlessrevolution.com (t=707, juillet 2023),
// vérifiée dans Napoleon.exe du PC fixe le 26/11/2026 : présente une seule fois.
public class NapoleonProfile : GameProfile
{
    public override string ExecutableName => "Napoleon.exe";
    public override string GameName       => "Total War: NAPOLEON – Definitive Edition";
    public override string Emoji          => "⚔️";
    public override string GameVersion    => "1.3.0.0 (Steam)";

    public override List<SortDefinition> GetSorts() => new()
    {
        new()
        {
            Name        = "Argent illimité",
            Description = "Recruter, construire et rechercher ne retire plus d'or du trésor (campagne).",
            Category    = SortCategory.Ressources,
            Type        = SortType.Patch,
            Hotkey      = "F1",
            // Napoleon.exe+7AF5FC : sub [ebx+3F4],edi  (trésor de la faction = [ebx+3F4])
            BaseOffset    = 0x7AF5FC,
            OriginalBytes = new byte[] { 0x29, 0xBB, 0xF4, 0x03, 0x00, 0x00 },
            PatchBytes    = new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 },
        },
    };
}
