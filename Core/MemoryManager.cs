// ============================================================
//  ÉTAPE 2 — LECTURE ET ÉCRITURE MÉMOIRE
//
//  CONCEPT : Comment fonctionne la mémoire d'un jeu ?
//
//  La RAM d'un jeu est un grand tableau d'octets.
//  Chaque variable du jeu (vie, argent, munitions...) est stockée
//  à une ADRESSE mémoire précise.
//
//  Exemple (vie du joueur en int 32 bits) :
//    Adresse 0x14A3F200 → [64 00 00 00] = 100 (en little-endian)
//
//  En écrivant [00 00 C8 42] à cette adresse → vie = 100.0f (float)
//
//  TYPES DE DONNÉES COURANTS DANS LES JEUX :
//  - int (4 octets)    → vie entière, niveau, score
//  - float (4 octets)  → vie réelle, position X/Y/Z, vitesse
//  - double (8 octets) → argent (pour éviter les arrondis)
//  - byte (1 octet)    → flags booléens, états
//  - long (8 octets)   → timestamps, IDs uniques
// ============================================================

using System.Runtime.InteropServices;

namespace GameTrainer.Core;

public class MemoryManager
{
    // ── Imports Windows ───────────────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(
        IntPtr hProcess,        // Handle du processus
        IntPtr lpBaseAddress,   // Adresse à lire
        byte[] lpBuffer,        // Buffer qui recevra les données
        int nSize,              // Nombre d'octets à lire
        out int lpNumberOfBytesRead // Octets effectivement lus
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(
        IntPtr hProcess,        // Handle du processus
        IntPtr lpBaseAddress,   // Adresse où écrire
        byte[] lpBuffer,        // Données à écrire
        int nSize,              // Nombre d'octets à écrire
        out int lpNumberOfBytesWritten // Octets effectivement écrits
    );

    // ── Référence au processus attaché ───────────────────────────────────────
    private readonly ProcessManager _processManager;

    public MemoryManager(ProcessManager processManager)
    {
        _processManager = processManager;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  MÉTHODES DE LECTURE
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lit N octets bruts à une adresse donnée.
    /// C'est la méthode de base utilisée par toutes les autres.
    /// </summary>
    public byte[] ReadBytes(IntPtr address, int length)
    {
        var buffer = new byte[length];
        ReadProcessMemory(_processManager.Handle, address, buffer, length, out _);
        return buffer;
    }

    /// <summary>
    /// Lit un entier 32 bits (int) — le type le plus courant dans les jeux.
    /// Ex : vie = 100, munitions = 30, niveau = 15
    /// </summary>
    public int ReadInt(IntPtr address)
    {
        var bytes = ReadBytes(address, 4);
        return BitConverter.ToInt32(bytes, 0);
    }

    /// <summary>
    /// Lit un flottant 32 bits (float).
    /// Ex : position X = 128.5f, vitesse = 3.14f
    /// </summary>
    public float ReadFloat(IntPtr address)
    {
        var bytes = ReadBytes(address, 4);
        return BitConverter.ToSingle(bytes, 0);
    }

    /// <summary>
    /// Lit un double 64 bits.
    /// Ex : argent en dollars = 9999.99
    /// </summary>
    public double ReadDouble(IntPtr address)
    {
        var bytes = ReadBytes(address, 8);
        return BitConverter.ToDouble(bytes, 0);
    }

    /// <summary>
    /// Lit un long 64 bits.
    /// Ex : grandes valeurs de score, timestamps
    /// </summary>
    public long ReadLong(IntPtr address)
    {
        var bytes = ReadBytes(address, 8);
        return BitConverter.ToInt64(bytes, 0);
    }

    /// <summary>
    /// Lit un byte (1 octet).
    /// Ex : état d'un flag (0 = mort, 1 = vivant)
    /// </summary>
    public byte ReadByte(IntPtr address)
    {
        var bytes = ReadBytes(address, 1);
        return bytes[0];
    }

    /// <summary>
    /// Lit une chaîne de caractères en mémoire.
    /// Ex : nom du joueur, texte de dialogue
    /// </summary>
    public string ReadString(IntPtr address, int maxLength = 256, bool unicode = false)
    {
        var bytes = ReadBytes(address, maxLength * (unicode ? 2 : 1));
        if (unicode)
        {
            var str = System.Text.Encoding.Unicode.GetString(bytes);
            int nullIdx = str.IndexOf('\0');
            return nullIdx >= 0 ? str[..nullIdx] : str;
        }
        else
        {
            var str = System.Text.Encoding.ASCII.GetString(bytes);
            int nullIdx = str.IndexOf('\0');
            return nullIdx >= 0 ? str[..nullIdx] : str;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  MÉTHODES D'ÉCRITURE
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Écrit des octets bruts à une adresse.
    /// C'est la méthode de base pour toutes les modifications.
    /// </summary>
    public bool WriteBytes(IntPtr address, byte[] data)
    {
        return WriteProcessMemory(_processManager.Handle, address, data, data.Length, out _);
    }

    /// <summary>
    /// Écrit un entier 32 bits.
    /// Ex : mettre la vie à 9999
    /// </summary>
    public bool WriteInt(IntPtr address, int value)
        => WriteBytes(address, BitConverter.GetBytes(value));

    /// <summary>
    /// Écrit un flottant 32 bits.
    /// Ex : mettre la position Y à 0 (téléportation au sol)
    /// </summary>
    public bool WriteFloat(IntPtr address, float value)
        => WriteBytes(address, BitConverter.GetBytes(value));

    /// <summary>
    /// Écrit un double 64 bits.
    /// </summary>
    public bool WriteDouble(IntPtr address, double value)
        => WriteBytes(address, BitConverter.GetBytes(value));

    /// <summary>
    /// Écrit un long 64 bits.
    /// </summary>
    public bool WriteLong(IntPtr address, long value)
        => WriteBytes(address, BitConverter.GetBytes(value));

    /// <summary>
    /// Écrit un byte.
    /// </summary>
    public bool WriteByte(IntPtr address, byte value)
        => WriteBytes(address, new[] { value });

    // ══════════════════════════════════════════════════════════════════════════
    //  POINTEURS MULTI-NIVEAUX (POINTER CHAINS)
    //
    //  CONCEPT AVANCÉ : Dans les jeux modernes, les variables ne sont
    //  pas à des adresses fixes. Elles changent à chaque lancement !
    //  
    //  Solution : Les "pointer chains" (chaînes de pointeurs)
    //  Le jeu a une adresse de base STABLE (module de base) qui pointe
    //  vers d'autres adresses + des offsets.
    //
    //  Exemple :
    //  [BaseAddress + 0x001234] → 0x14A3F000
    //  [0x14A3F000 + 0x58]     → 0x14A3F058
    //  [0x14A3F058 + 0x14]     → VALEUR DE LA VIE ✓
    //
    //  On appelle ça : BaseAddress → Offset[0] → Offset[1] → Valeur
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Résout une chaîne de pointeurs et retourne l'adresse finale.
    /// </summary>
    public IntPtr ResolvePointerChain(IntPtr baseAddress, int[] offsets)
    {
        IntPtr current = baseAddress;

        for (int i = 0; i < offsets.Length - 1; i++)
        {
            // Lit l'adresse stockée à l'emplacement actuel
            var bytes = ReadBytes(current + offsets[i], _processManager.Is64Bit ? 8 : 4);

            if (_processManager.Is64Bit)
                current = (IntPtr)BitConverter.ToInt64(bytes, 0);
            else
                current = (IntPtr)BitConverter.ToInt32(bytes, 0);

            if (current == IntPtr.Zero)
                return IntPtr.Zero; // Pointeur null = chemin cassé
        }

        // Dernier offset : c'est l'adresse finale de la valeur
        return current + offsets[^1];
    }

    /// <summary>
    /// Lit une valeur via une chaîne de pointeurs.
    /// Ex : ReadInt(baseModule + 0x1234, new[]{0x58, 0x14, 0x0C})
    /// </summary>
    public int ReadIntPtr(IntPtr baseAddress, int[] offsets)
    {
        var addr = ResolvePointerChain(baseAddress, offsets);
        return addr == IntPtr.Zero ? 0 : ReadInt(addr);
    }

    public float ReadFloatPtr(IntPtr baseAddress, int[] offsets)
    {
        var addr = ResolvePointerChain(baseAddress, offsets);
        return addr == IntPtr.Zero ? 0f : ReadFloat(addr);
    }

    /// <summary>
    /// Écrit une valeur via une chaîne de pointeurs.
    /// </summary>
    public bool WriteIntPtr(IntPtr baseAddress, int[] offsets, int value)
    {
        var addr = ResolvePointerChain(baseAddress, offsets);
        return addr != IntPtr.Zero && WriteInt(addr, value);
    }

    public bool WriteFloatPtr(IntPtr baseAddress, int[] offsets, float value)
    {
        var addr = ResolvePointerChain(baseAddress, offsets);
        return addr != IntPtr.Zero && WriteFloat(addr, value);
    }
}
