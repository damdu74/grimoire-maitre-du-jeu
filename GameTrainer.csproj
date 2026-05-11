// ============================================================
//  ÉTAPE 1 — GESTION DES PROCESSUS WINDOWS
//
//  CONCEPT : Qu'est-ce qu'un processus ?
//  Quand tu lances un jeu, Windows crée un "processus" : un
//  programme en cours d'exécution avec sa propre zone mémoire.
//  Pour modifier la mémoire du jeu, on doit d'abord obtenir un
//  "handle" (une poignée) sur ce processus via OpenProcess().
//
//  DROITS NÉCESSAIRES :
//  - PROCESS_VM_READ    → lire la mémoire
//  - PROCESS_VM_WRITE   → écrire dans la mémoire
//  - PROCESS_VM_OPERATION → nécessaire pour WriteProcessMemory
//  - PROCESS_QUERY_INFORMATION → lire les infos du processus
// ============================================================

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GameTrainer.Core;

public class ProcessManager : IDisposable
{
    // ── Imports des fonctions Windows (kernel32.dll) ──────────────────────────

    // OpenProcess : obtenir un accès au processus ciblé
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(
        uint dwDesiredAccess,   // Droits demandés
        bool bInheritHandle,    // Héritage (toujours false pour nous)
        int dwProcessId         // PID du processus cible
    );

    // CloseHandle : libérer le handle quand on a fini
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    // IsWow64Process : savoir si le processus est 32 ou 64 bits
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);

    // ── Constantes de droits d'accès ──────────────────────────────────────────
    private const uint PROCESS_ALL_ACCESS = 0x1F0FFF; // Tous les droits (admin requis)

    // ── Propriétés publiques ──────────────────────────────────────────────────
    public IntPtr Handle { get; private set; } = IntPtr.Zero;
    public Process? AttachedProcess { get; private set; }
    public bool IsAttached => Handle != IntPtr.Zero && AttachedProcess != null;
    public bool Is64Bit { get; private set; }

    // ── Méthodes publiques ────────────────────────────────────────────────────

    /// <summary>
    /// Retourne la liste de tous les processus en cours sur le PC,
    /// filtrés pour exclure les processus système sans fenêtre.
    /// </summary>
    public static List<ProcessInfo> GetRunningProcesses()
    {
        var result = new List<ProcessInfo>();

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                // On garde uniquement les processus avec une fenêtre visible
                // (les jeux ont toujours une fenêtre principale)
                if (p.MainWindowHandle != IntPtr.Zero)
                {
                    result.Add(new ProcessInfo
                    {
                        PID = p.Id,
                        Name = p.ProcessName,
                        Title = p.MainWindowTitle,
                        MemoryMB = (int)(p.WorkingSet64 / 1024 / 1024)
                    });
                }
            }
            catch { /* Certains processus système refusent d'être lus */ }
        }

        return result.OrderBy(p => p.Name).ToList();
    }

    /// <summary>
    /// S'attache au processus ciblé (par PID).
    /// Après cette étape, on peut lire/écrire sa mémoire.
    /// </summary>
    public bool Attach(int pid)
    {
        // D'abord, on libère un éventuel processus précédent
        Detach();

        try
        {
            AttachedProcess = Process.GetProcessById(pid);

            // On demande à Windows l'accès complet au processus
            Handle = OpenProcess(PROCESS_ALL_ACCESS, false, pid);

            if (Handle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    $"OpenProcess échoué. Erreur Windows : {error}\n" +
                    "→ Lance le trainer en tant qu'Administrateur !");
            }

            // Détecte si le jeu est 32 ou 64 bits
            // (important pour calculer les adresses mémoire)
            IsWow64Process(Handle, out bool isWow64);
            Is64Bit = !isWow64 && Environment.Is64BitOperatingSystem;

            return true;
        }
        catch (Exception ex)
        {
            Detach();
            throw new InvalidOperationException($"Impossible de s'attacher : {ex.Message}");
        }
    }

    /// <summary>
    /// Attache par nom de processus (ex: "RDR2", "Cyberpunk2077")
    /// </summary>
    public bool AttachByName(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        if (processes.Length == 0)
            throw new InvalidOperationException($"Processus '{processName}' introuvable. Le jeu est-il lancé ?");

        return Attach(processes[0].Id);
    }

    /// <summary>
    /// Vérifie si le processus attaché est toujours actif.
    /// Important : les jeux peuvent planter ou être fermés.
    /// </summary>
    public bool IsAlive()
    {
        if (!IsAttached) return false;
        try
        {
            return !AttachedProcess!.HasExited;
        }
        catch { return false; }
    }

    /// <summary>
    /// Se détache du processus et libère le handle Windows.
    /// </summary>
    public void Detach()
    {
        if (Handle != IntPtr.Zero)
        {
            CloseHandle(Handle);
            Handle = IntPtr.Zero;
        }
        AttachedProcess = null;
    }

    public void Dispose() => Detach();
}

// ── Modèle de données pour afficher les processus dans l'UI ──────────────────
public class ProcessInfo
{
    public int PID { get; set; }
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    public int MemoryMB { get; set; }

    public override string ToString() => $"[{PID}] {Name} — {Title} ({MemoryMB} MB)";
}
