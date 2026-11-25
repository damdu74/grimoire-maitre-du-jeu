// ============================================================
//  MODÈLES DE DONNÉES
//
//  Ces classes représentent les données manipulées par le trainer.
// ============================================================

namespace GameTrainer.Models;

/// <summary>
/// Un résultat de scan : une adresse mémoire + la valeur qu'elle contient.
/// </summary>
public class ScanResult
{
    public IntPtr Address { get; set; }
    public GameTrainer.Core.ScanDataType DataType { get; set; }
    public byte[] PreviousValue { get; set; } = Array.Empty<byte>();
    public byte[] CurrentValue { get; set; } = Array.Empty<byte>();

    // Valeurs converties pour l'affichage
    public string DisplayAddress => $"0x{Address.ToString("X").PadLeft(16, '0')}";
    public string DisplayValue => DataType switch
    {
        Core.ScanDataType.Int32 => BitConverter.ToInt32(CurrentValue, 0).ToString(),
        Core.ScanDataType.Float => BitConverter.ToSingle(CurrentValue, 0).ToString("F3"),
        Core.ScanDataType.Double => BitConverter.ToDouble(CurrentValue, 0).ToString("F3"),
        Core.ScanDataType.Int64 => BitConverter.ToInt64(CurrentValue, 0).ToString(),
        Core.ScanDataType.Byte => CurrentValue[0].ToString(),
        _ => "?"
    };
    public string DisplayPrevious => DataType switch
    {
        Core.ScanDataType.Int32 => BitConverter.ToInt32(PreviousValue, 0).ToString(),
        Core.ScanDataType.Float => BitConverter.ToSingle(PreviousValue, 0).ToString("F3"),
        Core.ScanDataType.Double => BitConverter.ToDouble(PreviousValue, 0).ToString("F3"),
        Core.ScanDataType.Int64 => BitConverter.ToInt64(PreviousValue, 0).ToString(),
        Core.ScanDataType.Byte => PreviousValue[0].ToString(),
        _ => "?"
    };
}

/// <summary>
/// Une adresse sauvegardée par l'utilisateur, avec une description
/// et une option de "freeze" (geler la valeur).
///
/// CONCEPT FREEZE :
/// Le freeze fonctionne avec un Timer : toutes les X ms, on réécrit
/// la valeur voulue à l'adresse. Le jeu modifie la vie → le trainer
/// la remet immédiatement → effet "vie infinie".
/// </summary>
public class SavedAddress
{
    public string Description { get; set; } = "Sans nom";
    public IntPtr Address { get; set; }
    public GameTrainer.Core.ScanDataType DataType { get; set; }
    public string FreezeValue { get; set; } = "0";
    public bool IsFrozen { get; set; } = false;
    public string CurrentDisplay { get; set; } = "...";

    public string DisplayAddress => $"0x{Address.ToString("X").PadLeft(16, '0')}";
    public string FrozenStatus => IsFrozen ? "❄️ Gelé" : "—";
}
