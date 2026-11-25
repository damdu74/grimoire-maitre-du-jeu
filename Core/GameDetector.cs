// ============================================================
//  DÉTECTEUR DE JEUX — Les 4 sources
//
//  SOURCE 1 — Processus actifs (Task Manager)
//    Windows.Process.GetProcesses() liste tout ce qui tourne.
//    On filtre par une base de données d'exe connus (RDR2.exe,
//    Cyberpunk2077.exe...). Si un exe connu tourne → le jeu
//    est lancé en ce moment.
//
//  SOURCE 2 — Steam
//    Steam stocke ses bibliothèques dans un fichier texte
//    appelé "libraryfolders.vdf" (format Valve Data Format).
//    Chemin : C:\Program Files (x86)\Steam\steamapps\
//    Pour chaque bibliothèque, on lit les "appmanifest_XXXXX.acf"
//    qui contiennent le nom du jeu, son AppID, son dossier.
//
//  SOURCE 3 — Epic Games Store & GOG Galaxy
//    Epic stocke des fichiers .item dans :
//    C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests\
//    GOG utilise une base SQLite dans son dossier AppData.
//    Ces fichiers JSON contiennent le nom, l'exe, le dossier.
//
//  SOURCE 4 — Registre Windows
//    Beaucoup de jeux s'enregistrent dans :
//    HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall
//    Chaque clé contient "DisplayName" et "InstallLocation".
//    On filtre les entrées qui ressemblent à des jeux.
// ============================================================

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace GameTrainer.Core;

public class DetectedGame
{
    public string Name { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public string ExecutableName { get; set; } = "";
    public string InstallFolder { get; set; } = "";
    public string Source { get; set; } = "";       // "Steam", "Epic", "GOG", "Registre", "Actif"
    public string? CoverUrl { get; set; }           // URL de l'image (si disponible)
    public int? SteamAppId { get; set; }
    public bool IsRunning { get; set; } = false;
    public int? RunningPid { get; set; }
    public Image? GameImage { get; set; } = null;

    // Pour l'affichage dans la sidebar
    public string DisplaySource => Source switch
    {
        "Steam"    => "🎮 Steam",
        "Epic"     => "⚡ Epic",
        "GOG"      => "🌙 GOG",
        "Registre" => "🗂 Windows",
        "Actif"    => "▶ En cours",
        _          => Source
    };

    // URL de la jaquette Steam (si on a l'AppID)
    public string? SteamCoverUrl => SteamAppId.HasValue
        ? $"https://cdn.cloudflare.steamstatic.com/steam/apps/{SteamAppId}/header.jpg"
        : null;
}

public class GameDetector
{
    // ── Base de données d'exécutables connus ─────────────────────────────────
    // Associe nom_exe → nom_affiché
    // C'est cette liste qui permet de reconnaître un jeu dans les processus actifs
    private static readonly Dictionary<string, string> KnownGameExes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Rockstar
        ["RDR2.exe"]                  = "Red Dead Redemption 2",
        ["GTA5.exe"]                  = "Grand Theft Auto V",
        ["GTAIV.exe"]                 = "Grand Theft Auto IV",

        // CD Projekt Red
        ["Cyberpunk2077.exe"]         = "Cyberpunk 2077",
        ["witcher3.exe"]              = "The Witcher 3",
        ["witcher2.exe"]              = "The Witcher 2",

        // FromSoftware
        ["eldenring.exe"]             = "Elden Ring",
        ["sekiro.exe"]                = "Sekiro",
        ["DarkSoulsRemastered.exe"]   = "Dark Souls Remastered",
        ["DarkSoulsIII.exe"]          = "Dark Souls III",

        // Bethesda
        ["Skyrim.exe"]                = "The Elder Scrolls V: Skyrim",
        ["SkyrimSE.exe"]              = "Skyrim Special Edition",
        ["Fallout4.exe"]              = "Fallout 4",
        ["Starfield.exe"]             = "Starfield",

        // Ubisoft
        ["ACOdyssey.exe"]             = "Assassin's Creed Odyssey",
        ["ACOrigins.exe"]             = "Assassin's Creed Origins",
        ["ACValhalla.exe"]            = "Assassin's Creed Valhalla",
        ["FarCry6.exe"]               = "Far Cry 6",

        // EA / Bioware
        ["MassEffect.exe"]            = "Mass Effect",
        ["MassEffect2.exe"]           = "Mass Effect 2",
        ["MassEffect3.exe"]           = "Mass Effect 3",
        ["DragonAge.exe"]             = "Dragon Age: Origins",
        ["DragonAgeInquisition.exe"]  = "Dragon Age: Inquisition",

        // Microsoft / Xbox
        ["Halo1.exe"]                 = "Halo: Combat Evolved",
        ["MCC.exe"]                   = "Halo: The Master Chief Collection",
        ["Forza.exe"]                 = "Forza Horizon 5",

        // 2K / Firaxis
        ["CivilizationVI.exe"]        = "Civilization VI",
        ["XCOM2.exe"]                 = "XCOM 2",
        ["Borderlands3.exe"]          = "Borderlands 3",

        // Valve
        ["hl2.exe"]                   = "Half-Life 2",
        ["portal2.exe"]               = "Portal 2",
        ["dota2.exe"]                 = "Dota 2",

        // Misc populaires
        ["RimWorldWin64.exe"]         = "RimWorld",
        ["Stardew Valley.exe"]        = "Stardew Valley",
        ["Minecraft.Windows.exe"]     = "Minecraft",
        ["MONSTER HUNTER WORLD.exe"]  = "Monster Hunter: World",
        ["ds3.exe"]                   = "Dark Souls III (alt)",
        ["Control_DX11.exe"]          = "Control",
        ["Control_DX12.exe"]          = "Control",
        ["HogwartsLegacy.exe"]        = "Hogwarts Legacy",
        ["DeathStranding.exe"]        = "Death Stranding",
        ["Returnal.exe"]              = "Returnal",
        ["GoW.exe"]                   = "God of War",
        ["SpiderMan.exe"]             = "Spider-Man Remastered",
        ["HorizonZeroDawn.exe"]       = "Horizon Zero Dawn",
        ["RatchetPC.exe"]             = "Ratchet & Clank",
        ["PPSSPP.exe"]                = "PPSSPP (émulateur)",
        ["pcsx2.exe"]                 = "PCSX2 (émulateur)",
    };

    // ══════════════════════════════════════════════════════════════════════════
    //  MÉTHODE PRINCIPALE — Détecte tous les jeux de toutes sources
    // ══════════════════════════════════════════════════════════════════════════

    public static async Task<List<DetectedGame>> DetectAllGamesAsync(
        IProgress<string>? progress = null)
    {
        var allGames = new Dictionary<string, DetectedGame>(StringComparer.OrdinalIgnoreCase);

        // Lance les 4 détections en parallèle pour aller plus vite
        progress?.Report("Détection en cours...");

        var tasks = new[]
        {
            Task.Run(() => { progress?.Report("Scan Steam..."); return ScanSteam(); }),
            Task.Run(() => { progress?.Report("Scan Epic..."); return ScanEpic(); }),
            Task.Run(() => { progress?.Report("Scan Registre Windows..."); return ScanRegistry(); }),
            Task.Run(() => { progress?.Report("Scan processus actifs..."); return ScanRunningProcesses(); }),
        };

        var results = await Task.WhenAll(tasks);

        // Fusionne tous les résultats sans doublons (clé = nom de l'exe)
        foreach (var list in results)
        {
            foreach (var game in list)
            {
                string key = game.ExecutableName.ToLower();
                if (!allGames.ContainsKey(key))
                    allGames[key] = game;
                else
                {
                    // Enrichit l'entrée existante si on a plus d'infos
                    var existing = allGames[key];
                    if (game.IsRunning)
                    {
                        existing.IsRunning = true;
                        existing.RunningPid = game.RunningPid;
                    }
                    if (existing.SteamAppId == null && game.SteamAppId != null)
                        existing.SteamAppId = game.SteamAppId;
                }
            }
        }

        progress?.Report($"{allGames.Count} jeux trouvés !");

        // Trie : jeux en cours d'abord, puis par nom
        return allGames.Values
            .OrderByDescending(g => g.IsRunning)
            .ThenBy(g => g.Name)
            .ToList();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SOURCE 1 — Processus Windows actifs
    //
    //  CONCEPT : Process.GetProcesses() donne tous les processus en cours.
    //  On compare chaque nom d'exe à notre dictionnaire KnownGameExes.
    //  Si on trouve une correspondance → le jeu tourne en ce moment !
    // ══════════════════════════════════════════════════════════════════════════

    public static List<DetectedGame> ScanRunningProcesses()
    {
        var found = new List<DetectedGame>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                string exeName = process.ProcessName + ".exe";

                // N'accepte QUE les exe présents dans la liste connue
                if (KnownGameExes.TryGetValue(exeName, out string? gameName))
                {
                    string exePath = "";
                    try { exePath = process.MainModule?.FileName ?? ""; } catch { }

                    found.Add(new DetectedGame
                    {
                        Name           = gameName,
                        ExecutableName = exeName,
                        ExecutablePath = exePath,
                        InstallFolder  = Path.GetDirectoryName(exePath) ?? "",
                        Source         = "Actif",
                        IsRunning      = true,
                        RunningPid     = process.Id
                    });
                }
            }
            catch { }
        }

        return found;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SOURCE 2 — Steam
    //
    //  CONCEPT : Steam stocke ses infos dans des fichiers texte ".vdf"
    //  (Valve Data Format, proche du JSON mais avec un format propriétaire).
    //
    //  Étapes :
    //  1. Trouve le dossier Steam via le registre Windows
    //  2. Lit libraryfolders.vdf → liste des dossiers de bibliothèque
    //  3. Dans chaque dossier, lit les appmanifest_XXXXX.acf
    //  4. Chaque manifest = un jeu installé avec son nom, AppID, dossier
    //
    //  Exemple de appmanifest_1174180.acf (Red Dead Redemption 2) :
    //  "AppState" {
    //    "appid" "1174180"
    //    "name"  "Red Dead Redemption 2"
    //    "installdir" "Red Dead Redemption 2"
    //  }
    // ══════════════════════════════════════════════════════════════════════════

    public static List<DetectedGame> ScanSteam()
    {
        var found = new List<DetectedGame>();

        try
        {
            // Étape 1 : Trouve Steam via le registre
            string? steamPath = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",
                "InstallPath", null) as string
                ?? Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam",
                "InstallPath", null) as string;

            if (steamPath == null || !Directory.Exists(steamPath)) return found;

            // Étape 2 : Lit les dossiers de bibliothèque
            var libraryFolders = new List<string>();
            libraryFolders.Add(Path.Combine(steamPath, "steamapps")); // Dossier principal

            string vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdfPath))
            {
                // Parse le fichier VDF pour trouver les bibliothèques secondaires
                // Format : "path" "D:\\SteamLibrary"
                string vdfContent = File.ReadAllText(vdfPath);
                var pathMatches = Regex.Matches(vdfContent, @"""path""\s+""([^""]+)""");
                foreach (Match m in pathMatches)
                {
                    string libPath = m.Groups[1].Value.Replace(@"\\", @"\");
                    string steamAppsPath = Path.Combine(libPath, "steamapps");
                    if (Directory.Exists(steamAppsPath))
                        libraryFolders.Add(steamAppsPath);
                }
            }

            // Étape 3 & 4 : Lit les manifests dans chaque bibliothèque
            foreach (string libFolder in libraryFolders)
            {
                if (!Directory.Exists(libFolder)) continue;

                foreach (string acfFile in Directory.GetFiles(libFolder, "appmanifest_*.acf"))
                {
                    try
                    {
                        string content = File.ReadAllText(acfFile);

                        string? appId      = ExtractVdfValue(content, "appid");
                        string? name       = ExtractVdfValue(content, "name");
                        string? installDir = ExtractVdfValue(content, "installdir");

                        if (name == null || installDir == null) continue;

                        // ── Filtre les composants Steam qui ne sont PAS des jeux ──
                        string nameLower = name.ToLower();
                        string[] notGames = {
                            "redistributable", "redist", "steamworks",
                            "directx", "vcredist", "runtime", "common",
                            "proton", "steam linux", "openxr", "vulkan",
                            "physx", "dotnet", "microsoft visual"
                        };
                        if (notGames.Any(k => nameLower.Contains(k))) continue;

                        string fullInstallPath = Path.Combine(libFolder, "common", installDir);
                        if (!Directory.Exists(fullInstallPath)) continue;

                        string exeName = FindMainExecutable(fullInstallPath, name);

                        found.Add(new DetectedGame
                        {
                            Name           = name,
                            ExecutableName = exeName,
                            ExecutablePath = Path.Combine(fullInstallPath, exeName),
                            InstallFolder  = fullInstallPath,
                            Source         = "Steam",
                            SteamAppId     = int.TryParse(appId, out int id) ? id : null
                        });
                    }
                    catch { }
                }
            }
        }
        catch { }

        return found;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SOURCE 3 — Epic Games Store & GOG Galaxy
    //
    //  CONCEPT Epic :
    //  Epic stocke un fichier .item (JSON) par jeu installé dans :
    //  C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests\
    //
    //  CONCEPT GOG :
    //  GOG stocke des fichiers .json dans :
    //  C:\ProgramData\GOG.com\Galaxy\storage\galaxy-2.0.db  (SQLite)
    //  Ou des fichiers JSON dans :
    //  C:\ProgramData\GOG.com\Galaxy\storage\
    // ══════════════════════════════════════════════════════════════════════════

    public static List<DetectedGame> ScanEpic()
    {
        var found = new List<DetectedGame>();

        // ── Epic Games ────────────────────────────────────────────────────────
        try
        {
            string epicManifestsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests");

            if (Directory.Exists(epicManifestsPath))
            {
                foreach (string itemFile in Directory.GetFiles(epicManifestsPath, "*.item"))
                {
                    try
                    {
                        string json = File.ReadAllText(itemFile);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        // Un jeu Epic a toujours ces champs
                        if (!root.TryGetProperty("DisplayName", out var displayName)) continue;
                        if (!root.TryGetProperty("InstallLocation", out var installLocation)) continue;
                        if (!root.TryGetProperty("LaunchExecutable", out var launchExe)) continue;

                        string name = displayName.GetString() ?? "";
                        string installPath = installLocation.GetString() ?? "";
                        string exeRelPath = launchExe.GetString() ?? "";

                        if (string.IsNullOrEmpty(name) || !Directory.Exists(installPath)) continue;

                        string exeName = Path.GetFileName(exeRelPath);
                        string fullExePath = Path.Combine(installPath, exeRelPath);

                        found.Add(new DetectedGame
                        {
                            Name = name,
                            ExecutableName = exeName,
                            ExecutablePath = fullExePath,
                            InstallFolder = installPath,
                            Source = "Epic"
                        });
                    }
                    catch { }
                }
            }
        }
        catch { }

        // ── GOG Galaxy ────────────────────────────────────────────────────────
        try
        {
            // GOG stocke les infos dans le registre aussi
            string gogRegPath = @"SOFTWARE\WOW6432Node\GOG.com\Games";
            using var gogKey = Registry.LocalMachine.OpenSubKey(gogRegPath);

            if (gogKey != null)
            {
                foreach (string gameId in gogKey.GetSubKeyNames())
                {
                    try
                    {
                        using var gameKey = gogKey.OpenSubKey(gameId);
                        if (gameKey == null) continue;

                        string? name = gameKey.GetValue("GAMENAME") as string;
                        string? exePath = gameKey.GetValue("EXE") as string;
                        string? installPath = gameKey.GetValue("PATH") as string;

                        if (name == null || exePath == null) continue;

                        found.Add(new DetectedGame
                        {
                            Name = name,
                            ExecutableName = Path.GetFileName(exePath),
                            ExecutablePath = exePath,
                            InstallFolder = installPath ?? Path.GetDirectoryName(exePath) ?? "",
                            Source = "GOG"
                        });
                    }
                    catch { }
                }
            }
        }
        catch { }

        return found;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SOURCE 4 — Registre Windows
    //  FILTRE STRICT : uniquement les éditeurs de jeux connus
    // ══════════════════════════════════════════════════════════════════════════

    // Éditeurs de jeux connus (whitelist)
    private static readonly HashSet<string> KnownGamePublishers = new(StringComparer.OrdinalIgnoreCase)
    {
        "rockstar games", "cd projekt", "bethesda", "ubisoft", "electronic arts",
        "ea games", "2k games", "activision", "blizzard", "square enix",
        "bandai namco", "capcom", "konami", "sega", "atlus", "koei tecmo",
        "focus entertainment", "505 games", "thq nordic", "deep silver",
        "paradox", "warhorse studios", "larian studios", "obsidian",
        "insomniac", "naughty dog", "santa monica", "guerrilla", "fromsoftware",
        "valve", "epic games", "gog", "devolver digital", "annapurna",
        "raw fury", "coffee stain", "klei entertainment", "supergiant",
        "double fine", "team17", "kalypso", "nacon", "microids",
        "bigben", "focus home", "cyanide", "spiders studio",
    };

    // Dossiers typiques des jeux (whitelist chemins)
    private static readonly string[] KnownGameFolders =
    {
        @"C:\Program Files\Steam\steamapps",
        @"C:\Program Files (x86)\Steam\steamapps",
        @"C:\Program Files\Epic Games",
        @"C:\Program Files\GOG Galaxy\Games",
        @"C:\GOG Games",
        @"D:\Games", @"E:\Games", @"F:\Games",
        @"D:\SteamLibrary", @"E:\SteamLibrary", @"F:\SteamLibrary",
        @"D:\Steam", @"E:\Steam",
        @"C:\Games", @"C:\Jeux",
    };

    public static List<DetectedGame> ScanRegistry()
    {
        var found = new List<DetectedGame>();

        string[] uninstallPaths = {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        // Mots-clés système à exclure absolument
        string[] systemKeywords = {
            "microsoft", "visual c++", "directx", "redistributable", "runtime",
            ".net", "driver", "update", "windows", "office", "adobe", "google",
            "chrome", "firefox", "steam", "epic games launcher", "gog galaxy",
            "ubisoft connect", "ea app", "battlenet", "nvidia", "amd", "intel",
            "realtek", "logitech", "corsair", "razer", "asus", "msi installer",
            "vcredist", "physx", "openal", "webview", "edge", "teams",
            "onedrive", "skype", "zoom", "discord", "obs", "vlc", "7-zip",
            "winrar", "notepad", "vscode", "python", "node", "git ", "java",
            "sql", "dotnet", "setup", "inno", "installer", "uninstall",
        };

        foreach (string regPath in uninstallPaths)
        {
            try
            {
                using var uninstallKey = Registry.LocalMachine.OpenSubKey(regPath);
                if (uninstallKey == null) continue;

                foreach (string subKeyName in uninstallKey.GetSubKeyNames())
                {
                    try
                    {
                        using var appKey = uninstallKey.OpenSubKey(subKeyName);
                        if (appKey == null) continue;

                        string? name        = appKey.GetValue("DisplayName") as string;
                        string? installPath = appKey.GetValue("InstallLocation") as string;
                        string? publisher   = (appKey.GetValue("Publisher") as string ?? "").ToLower();

                        if (string.IsNullOrWhiteSpace(name) ||
                            string.IsNullOrWhiteSpace(installPath) ||
                            !Directory.Exists(installPath)) continue;

                        string nameLower = name.ToLower();

                        // Exclusion stricte des programmes système
                        bool isSystem = systemKeywords.Any(kw =>
                            nameLower.Contains(kw) || publisher.Contains(kw));
                        if (isSystem) continue;

                        // FILTRE 1 : éditeur connu dans la whitelist
                        bool knownPublisher = KnownGamePublishers.Any(p =>
                            publisher.Contains(p.ToLower()));

                        // FILTRE 2 : installé dans un dossier de jeux connu
                        bool knownFolder = KnownGameFolders.Any(f =>
                            installPath.StartsWith(f, StringComparison.OrdinalIgnoreCase));

                        // FILTRE 3 : dossier contient un gros exe (> 5 MB = probablement un jeu)
                        bool hasGameExe = false;
                        try
                        {
                            hasGameExe = Directory
                                .GetFiles(installPath, "*.exe", SearchOption.TopDirectoryOnly)
                                .Any(f => new FileInfo(f).Length > 5 * 1024 * 1024);
                        }
                        catch { }

                        // N'accepte que si au moins 2 filtres passent
                        int score = (knownPublisher ? 1 : 0) +
                                    (knownFolder    ? 1 : 0) +
                                    (hasGameExe     ? 1 : 0);
                        if (score < 2) continue;

                        string exeName = FindMainExecutable(installPath, name);
                        if (string.IsNullOrEmpty(exeName)) continue;

                        found.Add(new DetectedGame
                        {
                            Name           = name,
                            ExecutableName = exeName,
                            ExecutablePath = Path.Combine(installPath, exeName),
                            InstallFolder  = installPath,
                            Source         = "Registre"
                        });
                    }
                    catch { }
                }
            }
            catch { }
        }

        return found;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  UTILITAIRES PRIVÉS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Extrait une valeur d'un fichier VDF Steam.
    /// Format : "clé" "valeur"
    /// </summary>
    private static string? ExtractVdfValue(string content, string key)
    {
        var match = Regex.Match(content,
            $@"""{Regex.Escape(key)}""\s+""([^""]+)""",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Trouve l'exécutable principal d'un jeu dans son dossier.
    ///
    /// Stratégie :
    /// 1. Vérifie si l'exe de KnownGameExes est dans ce dossier
    /// 2. Prend l'exe le plus gros à la racine (souvent le jeu)
    /// 3. Ignore les outils (launcher, crash_handler, setup, etc.)
    /// </summary>
    public static string FindMainExecutable(string folder, string gameName)
    {
        if (!Directory.Exists(folder)) return "";

        try
        {
            // Cherche d'abord dans notre base de données
            foreach (var (exeName, _) in KnownGameExes)
            {
                if (File.Exists(Path.Combine(folder, exeName)))
                    return exeName;
                // Cherche aussi dans les sous-dossiers (profondeur 1)
                foreach (var sub in Directory.GetDirectories(folder))
                {
                    if (File.Exists(Path.Combine(sub, exeName)))
                        return exeName;
                }
            }

            // Mots-clés d'exe à ignorer (pas le jeu principal)
            string[] ignoredKeywords = {
                "launcher", "setup", "install", "uninstall", "crash",
                "report", "helper", "tool", "updater", "redist",
                "vc_redist", "dxsetup", "uplay", "easyanticheat"
            };

            // Prend les exe à la racine et dans Binaries/
            var searchDirs = new[] { folder }
                .Concat(Directory.GetDirectories(folder)
                    .Where(d => new[] { "bin", "binaries", "game", "win64", "win32", "x64" }
                        .Any(k => Path.GetFileName(d).ToLower().Contains(k))))
                .ToArray();

            var exeFiles = searchDirs
                .SelectMany(dir => Directory.GetFiles(dir, "*.exe"))
                .Where(f => {
                    string fn = Path.GetFileNameWithoutExtension(f).ToLower();
                    return !ignoredKeywords.Any(k => fn.Contains(k));
                })
                .OrderByDescending(f => new FileInfo(f).Length) // Le plus gros = jeu principal
                .ToList();

            return exeFiles.Count > 0 ? Path.GetFileName(exeFiles[0]) : "";
        }
        catch { return ""; }
    }

    /// <summary>
    /// Vérifie en temps réel quels jeux de la liste sont en cours d'exécution.
    /// À appeler régulièrement (ex: toutes les 5 secondes) pour détecter
    /// quand un jeu est lancé ou fermé.
    /// </summary>
    public static void RefreshRunningStatus(List<DetectedGame> games)
    {
        // GroupBy pour éviter les doublons d'exe (ex: csrss.exe lancé plusieurs fois)
        var runningExes = Process.GetProcesses()
            .GroupBy(p => p.ProcessName + ".exe", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        foreach (var game in games)
        {
            if (runningExes.TryGetValue(game.ExecutableName, out int pid))
            {
                game.IsRunning = true;
                game.RunningPid = pid;
            }
            else
            {
                game.IsRunning = false;
                game.RunningPid = null;
            }
        }
    }
}
