# Outil de développement : trouver une valeur dans un jeu et le chemin de pointeurs
# stable qui y mène, pour écrire un profil du Grimoire.
#
#   .\chercher.ps1 premier  -Jeu Napoleon -Valeur 12000   # 1er scan (int32)
#   .\chercher.ps1 suivant  -Jeu Napoleon -Valeur 11500   # affine après un changement en jeu
#   .\chercher.ps1 ecrire   -Jeu Napoleon -Adresse 0x1234ABCD -Valeur 99999
#   .\chercher.ps1 pointeurs -Jeu Napoleon -Adresse 0x1234ABCD   # chemin depuis Napoleon.exe
#   .\chercher.ps1 lire     -Jeu Napoleon -Chemin "Napoleon.exe+0x1A2B3C,0x10,0x4C"
#
# Les adresses candidates sont gardées dans %TEMP%\grimoire-scan.txt entre deux appels.
param(
    [Parameter(Mandatory)][ValidateSet('premier','suivant','ecrire','pointeurs','lire','ecrivain')] [string]$Action,
    [Parameter(Mandatory)][string]$Jeu,
    [long]$Valeur,
    [string]$Adresse,
    [string]$Chemin,
    [int]$ProcessId,
    [int]$Profondeur = 4,
    [int]$OffsetMax = 0x1000,
    [int]$Secondes = 20
)
$ErrorActionPreference = 'Stop'
$etat = Join-Path $env:TEMP 'grimoire-scan.txt'

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class Mem
{
    [StructLayout(LayoutKind.Sequential)]
    struct MBI { public ulong Base, AllocBase; public uint AllocProt, a1; public ulong Size; public uint State, Protect, Type, a2; }
    [StructLayout(LayoutKind.Sequential)]
    struct MODINFO { public IntPtr Base; public uint Size; public IntPtr Entry; }

    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint a, bool i, int pid);
    [DllImport("kernel32.dll")] static extern int VirtualQueryEx(IntPtr h, IntPtr a, out MBI m, uint l);
    [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr h, IntPtr a, byte[] b, IntPtr n, out IntPtr r);
    [DllImport("kernel32.dll")] static extern bool WriteProcessMemory(IntPtr h, IntPtr a, byte[] b, IntPtr n, out IntPtr w);
    [DllImport("psapi.dll")] static extern bool EnumProcessModulesEx(IntPtr h, IntPtr[] m, int cb, out int need, uint filter);
    [DllImport("psapi.dll", CharSet = CharSet.Unicode)] static extern uint GetModuleBaseName(IntPtr h, IntPtr m, StringBuilder s, int n);
    [DllImport("psapi.dll")] static extern bool GetModuleInformation(IntPtr h, IntPtr m, out MODINFO i, int cb);

    public static IntPtr H;
    public static void Open(int pid) { H = OpenProcess(0x0438, false, pid); if (H == IntPtr.Zero) throw new Exception("OpenProcess refusé"); }

    // Module principal (marche pour un jeu 32 bits lu depuis un processus 64 bits)
    public static ulong ModBase, ModSize; public static string ModName;
    public static void FindModule(string exe)
    {
        var mods = new IntPtr[1024]; int need;
        EnumProcessModulesEx(H, mods, mods.Length * IntPtr.Size, out need, 3);
        for (int i = 0; i < need / IntPtr.Size; i++)
        {
            var sb = new StringBuilder(260); GetModuleBaseName(H, mods[i], sb, 260);
            if (!sb.ToString().Equals(exe, StringComparison.OrdinalIgnoreCase)) continue;
            MODINFO mi; GetModuleInformation(H, mods[i], out mi, Marshal.SizeOf(typeof(MODINFO)));
            ModBase = (ulong)mi.Base.ToInt64(); ModSize = mi.Size; ModName = sb.ToString(); return;
        }
        throw new Exception("Module introuvable : " + exe);
    }

    // Régions lisibles et inscriptibles (données du jeu)
    public static List<ulong[]> Regions()
    {
        var res = new List<ulong[]>(); ulong a = 0; MBI m;
        while (a < 0x100000000UL && VirtualQueryEx(H, (IntPtr)(long)a, out m, (uint)Marshal.SizeOf(typeof(MBI))) != 0)
        {
            bool rw = (m.Protect & 0xCC) != 0 && (m.Protect & 0x100) == 0;
            if (m.State == 0x1000 && rw) res.Add(new[] { m.Base, m.Size });
            a = m.Base + m.Size;
        }
        return res;
    }

    public static byte[] Read(ulong a, int n) { var b = new byte[n]; IntPtr r; ReadProcessMemory(H, (IntPtr)(long)a, b, (IntPtr)n, out r); return b; }
    public static int ReadInt(ulong a) { return BitConverter.ToInt32(Read(a, 4), 0); }
    public static uint ReadPtr(ulong a) { return BitConverter.ToUInt32(Read(a, 4), 0); }
    public static bool WriteInt(ulong a, int v) { var b = BitConverter.GetBytes(v); IntPtr w; return WriteProcessMemory(H, (IntPtr)(long)a, b, (IntPtr)4, out w); }

    // Parcourt toute la mémoire par blocs de 16 Mo, valeur 32 bits alignée sur 4
    public delegate void Visit(ulong addr, uint value);
    public static void Each(Visit f)
    {
        foreach (var r in Regions())
            for (ulong off = 0; off < r[1]; off += 0x1000000)
            {
                int n = (int)Math.Min(0x1000000UL, r[1] - off);
                var b = Read(r[0] + off, n);
                for (int i = 0; i + 4 <= n; i += 4) f(r[0] + off + (ulong)i, BitConverter.ToUInt32(b, i));
            }
    }

    public static List<ulong> FirstScan(int v)
    {
        var res = new List<ulong>(); uint u = (uint)v;
        Each((a, x) => { if (x == u) res.Add(a); });
        return res;
    }

    // Remonte les pointeurs niveau par niveau jusqu'à un emplacement dans le module (statique).
    // Résultat : chemins au format profil Grimoire  "BaseOffset | 0, off_n, ..., off_1"
    public static List<string> PointerScan(ulong target, int depth, uint maxOff)
    {
        var found = new List<string>();
        // parent[loc] = (adresse pointée visée, offset) pour reconstruire les chemins
        var links = new Dictionary<ulong, List<ulong[]>>();
        var level = new List<ulong> { target };
        for (int d = 0; d < depth && level.Count > 0; d++)
        {
            level.Sort(); var arr = level.ToArray();
            var next = new HashSet<ulong>();
            Each((loc, v) =>
            {
                // cibles t telles que v <= t < v + maxOff
                int i = Array.BinarySearch(arr, (ulong)v); if (i < 0) i = ~i;
                for (; i < arr.Length && arr[i] < (ulong)v + maxOff; i++)
                {
                    if (loc == arr[i]) continue;
                    List<ulong[]> l; if (!links.TryGetValue(loc, out l)) links[loc] = l = new List<ulong[]>();
                    l.Add(new[] { arr[i], arr[i] - v });
                    next.Add(loc);
                }
            });
            foreach (var loc in next)
                if (loc >= ModBase && loc < ModBase + ModSize) Build(loc, links, target, new List<ulong>(), found, loc - ModBase, 0);
            // ponytail: plafond de 50 000 emplacements par niveau, sinon explosion combinatoire
            level = new List<ulong>(); foreach (var x in next) { if (!(x >= ModBase && x < ModBase + ModSize)) level.Add(x); if (level.Count >= 50000) break; }
            if (found.Count >= 200) break;
        }
        return found;
    }

    // ── « Qui écrit à cette adresse » : point d'arrêt matériel (comme Cheat Engine) ──
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool DebugActiveProcess(int pid);
    [DllImport("kernel32.dll")] static extern bool DebugActiveProcessStop(int pid);
    [DllImport("kernel32.dll")] static extern bool DebugSetProcessKillOnExit(bool k);
    [DllImport("kernel32.dll")] static extern bool WaitForDebugEvent(byte[] ev, uint ms);
    [DllImport("kernel32.dll")] static extern bool ContinueDebugEvent(int pid, int tid, uint status);
    [DllImport("kernel32.dll")] static extern bool Wow64GetThreadContext(IntPtr t, uint[] c);
    [DllImport("kernel32.dll")] static extern bool Wow64SetThreadContext(IntPtr t, uint[] c);
    [DllImport("kernel32.dll")] static extern int Wow64SuspendThread(IntPtr t);
    [DllImport("kernel32.dll")] static extern int ResumeThread(IntPtr t);

    // WOW64_CONTEXT vu comme 179 uint : [0]=flags [1]=Dr0 [5]=Dr6 [6]=Dr7 [46]=Eip
    static void SetDr(IntPtr t, uint addr, uint dr7)
    {
        var c = new uint[179]; c[0] = 0x10010;
        if (!Wow64GetThreadContext(t, c)) return;
        c[0] = 0x10010; c[1] = addr; c[5] = 0; c[6] = dr7;
        Wow64SetThreadContext(t, c);
    }

    // Renvoie, pour chaque instruction qui a écrit dans addr, l'EIP juste après elle et le nombre de fois
    public static Dictionary<uint, int> WhatWrites(int pid, uint addr, int seconds)
    {
        var hits = new Dictionary<uint, int>();
        var threads = new Dictionary<int, IntPtr>();
        const uint DR7 = 0x000D0001;   // Dr0 actif, écriture, 4 octets
        if (!DebugActiveProcess(pid)) throw new Exception("DebugActiveProcess refusé (" + Marshal.GetLastWin32Error() + ")");
        DebugSetProcessKillOnExit(false);   // si le script plante, le jeu continue
        var ev = new byte[176];
        var end = DateTime.Now.AddSeconds(seconds);
        try
        {
            while (DateTime.Now < end)
            {
                if (!WaitForDebugEvent(ev, 200)) continue;
                uint code = BitConverter.ToUInt32(ev, 0); int tid = BitConverter.ToInt32(ev, 8);
                uint status = 0x00010002; // DBG_CONTINUE
                if (code == 3) { var h = (IntPtr)BitConverter.ToInt64(ev, 32); threads[tid] = h; SetDr(h, addr, DR7); }
                else if (code == 2) { var h = (IntPtr)BitConverter.ToInt64(ev, 16); threads[tid] = h; SetDr(h, addr, DR7); }
                else if (code == 4) threads.Remove(tid);
                else if (code == 5) { ContinueDebugEvent(pid, tid, status); break; }
                else if (code == 1)
                {
                    uint ex = BitConverter.ToUInt32(ev, 16);
                    if (ex == 0x80000004 || ex == 0x4000001E)
                    {
                        IntPtr h;
                        if (threads.TryGetValue(tid, out h))
                        {
                            var c = new uint[179]; c[0] = 0x10011;
                            if (Wow64GetThreadContext(h, c))
                            {
                                int n; hits.TryGetValue(c[46], out n); hits[c[46]] = n + 1;
                                c[0] = 0x10010; c[5] = 0; Wow64SetThreadContext(h, c);
                            }
                        }
                    }
                    else if (ex != 0x80000003 && ex != 0x4000001F) status = 0x80010001; // pas pour nous
                }
                ContinueDebugEvent(pid, tid, status);
            }
        }
        finally
        {
            foreach (var h in threads.Values) { Wow64SuspendThread(h); SetDr(h, addr, 0); ResumeThread(h); }
            // vide les événements en attente avant de se détacher, sinon le jeu peut planter
            while (WaitForDebugEvent(ev, 300))
            {
                uint code = BitConverter.ToUInt32(ev, 0), ex = BitConverter.ToUInt32(ev, 16);
                bool ours = code != 1 || ex == 0x80000004 || ex == 0x4000001E || ex == 0x80000003 || ex == 0x4000001F;
                ContinueDebugEvent(pid, BitConverter.ToInt32(ev, 8), ours ? 0x00010002u : 0x80010001u);
            }
            DebugActiveProcessStop(pid);
        }
        return hits;
    }

    static void Build(ulong loc, Dictionary<ulong, List<ulong[]>> links, ulong target, List<ulong> offs, List<string> found, ulong baseOff, int guard)
    {
        if (guard > 8 || found.Count >= 200) return;
        List<ulong[]> l; if (!links.TryGetValue(loc, out l)) return;
        foreach (var e in l)
        {
            var o = new List<ulong>(offs); o.Add(e[1]);
            if (e[0] == target)
            {
                var parts = new List<string> { "0" }; foreach (var x in o) parts.Add("0x" + x.ToString("X"));
                found.Add("0x" + baseOff.ToString("X") + " | " + string.Join(", ", parts));
            }
            else Build(e[0], links, target, o, found, baseOff, guard + 1);
        }
    }
}
'@

$proc = Get-Process $Jeu -ErrorAction SilentlyContinue | Where-Object MainWindowHandle -ne 0 | Select-Object -First 1
if ($ProcessId) { $proc = Get-Process -Id $ProcessId }
if (-not $proc) { $proc = Get-Process $Jeu | Select-Object -First 1 }
[Mem]::Open($proc.Id)
[Mem]::FindModule("$($proc.ProcessName).exe")
"Jeu : $($proc.ProcessName) (PID $($proc.Id))  module 0x{0:X} taille 0x{1:X}" -f [Mem]::ModBase, [Mem]::ModSize
function Num($s) { if ($s -match '^0x') { [Convert]::ToUInt64($s.Substring(2), 16) } else { [uint64]$s } }

switch ($Action) {
  'premier' {
    $r = [Mem]::FirstScan([int]$Valeur)
    $r | ForEach-Object { '0x{0:X}' -f $_ } | Set-Content $etat
    "$($r.Count) adresses contiennent $Valeur"
  }
  'suivant' {
    $avant = Get-Content $etat
    $r = $avant | Where-Object { [Mem]::ReadInt((Num $_)) -eq [int]$Valeur }
    $r | Set-Content $etat
    "$($r.Count) adresses restantes (sur $($avant.Count))"
    if ($r.Count -le 20) { $r }
  }
  'ecrire' {
    $a = Num $Adresse
    "avant : $([Mem]::ReadInt($a))"; [void][Mem]::WriteInt($a, [int]$Valeur); "après : $([Mem]::ReadInt($a))"
  }
  'pointeurs' {
    $t = [Mem]::PointerScan((Num $Adresse), $Profondeur, [uint32]$OffsetMax)
    "$($t.Count) chemins trouvés (BaseOffset | Offsets du profil) :"; $t | Select-Object -First 40
  }
  'ecrivain' {
    # Pendant $Secondes, note chaque instruction qui écrit dans l'adresse (fais varier la valeur en jeu)
    $a = [uint32](Num $Adresse)
    "Écoute pendant $Secondes s : dépense ou gagne de l'argent en jeu..."
    $h = [Mem]::WhatWrites($proc.Id, $a, $Secondes)
    "$($h.Count) instruction(s) ont écrit dans 0x{0:X}" -f $a
    foreach ($k in $h.Keys) {
      $avant = [Mem]::Read([uint64]$k - 16, 24)
      '{0}+0x{1:X}  ({2} fois)  octets [eip-16..eip+8] : {3}' -f [Mem]::ModName, ($k - [Mem]::ModBase), $h[$k], (($avant | ForEach-Object { $_.ToString('X2') }) -join ' ')
    }
  }
  'lire' {
    # "Napoleon.exe+0xB | 0, 0x10, 0x4C" ou "0xB,0,0x10,0x4C"
    $p = $Chemin -replace '^[^+]*\+','' -split '[|,]' | ForEach-Object { $_.Trim() } | Where-Object { $_ }
    $cur = [Mem]::ModBase + (Num $p[0]); $offs = $p[1..($p.Count-1)] | ForEach-Object { Num $_ }
    for ($i = 0; $i -lt $offs.Count - 1; $i++) { $cur = [uint64][Mem]::ReadPtr($cur + $offs[$i]); if ($cur -eq 0) { 'pointeur nul'; return } }
    $fin = $cur + $offs[-1]; 'adresse 0x{0:X} = {1}' -f $fin, [Mem]::ReadInt($fin)
  }
}
