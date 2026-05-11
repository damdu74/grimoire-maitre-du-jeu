// ============================================================
//  SYSTÈME DE MISE À JOUR AUTOMATIQUE
//
//  FONCTIONNEMENT :
//  1. Au démarrage, le Grimoire télécharge version.json
//     depuis GitHub (ou n'importe quel serveur web).
//
//  2. Il compare la version en ligne avec la version locale
//     (définie dans le .csproj → AssemblyInformationalVersion).
//
//  3. Si une nouvelle version existe :
//     → Affiche une fenêtre de mise à jour dans le style RetroKeys
//     → L'utilisateur clique "Mettre à jour"
//     → Télécharge le nouvel installateur en arrière-plan
//     → Lance l'installateur et ferme le Grimoire
//
//  FICHIER version.json (à mettre sur GitHub) :
//  {
//    "version": "1.2.0",
//    "releaseDate": "2025-01-15",
//    "downloadUrl": "https://github.com/TON_USER/grimoire/releases/download/v1.2.0/GrimoireInstaller.exe",
//    "releaseNotes": [
//      "Triches pour Elden Ring ajoutées",
//      "Correction du scanner mémoire",
//      "Nouvelle interface sidebar"
//    ],
//    "mandatory": false
//  }
//
//  URL du version.json à configurer ci-dessous ↓
// ============================================================

using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace GameTrainer.Core;

public class UpdateInfo
{
    public string Version      { get; set; } = "0.0.0";
    public string ReleaseDate  { get; set; } = "";
    public string DownloadUrl  { get; set; } = "";
    public List<string> ReleaseNotes { get; set; } = new();
    public bool Mandatory      { get; set; } = false;
}

public class AutoUpdater
{
    // ══════════════════════════════════════════════════════════════════════════
    //  ⚙️  CONFIGURATION — À MODIFIER AVEC TES PROPRES LIENS
    // ══════════════════════════════════════════════════════════════════════════

    // URL de ton fichier version.json sur GitHub
    // Format : https://raw.githubusercontent.com/TON_NOM/TON_REPO/main/version.json
    private const string VERSION_URL =
        "https://raw.githubusercontent.com/damdu74/grimoire-maitre-du-jeu/main/version.json";

    // Temps d'attente max pour la vérification (ms)
    private const int TIMEOUT_MS = 5000;

    // ══════════════════════════════════════════════════════════════════════════

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromMilliseconds(TIMEOUT_MS)
    };

    /// <summary>
    /// Retourne la version actuelle du Grimoire installé.
    /// Lue depuis l'assembly (définie dans le .csproj).
    /// </summary>
    public static Version GetCurrentVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var ver = asm.GetName().Version;
        return ver ?? new Version(1, 0, 0);
    }

    /// <summary>
    /// Vérifie si une mise à jour est disponible.
    /// Retourne null si pas de réseau ou déjà à jour.
    /// </summary>
    public static async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            // Télécharge version.json
            string json = await _http.GetStringAsync(VERSION_URL);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var info = JsonSerializer.Deserialize<UpdateInfo>(json, options);

            if (info == null) return null;

            // Compare les versions
            var current = GetCurrentVersion();
            if (!Version.TryParse(info.Version, out Version? latest)) return null;

            // Mise à jour disponible si la version en ligne est plus récente
            return latest > current ? info : null;
        }
        catch
        {
            // Pas de réseau, serveur down, JSON invalide → on ignore silencieusement
            return null;
        }
    }

    /// <summary>
    /// Télécharge le nouvel installateur dans le dossier Temp.
    /// Rapporte la progression (0-100%).
    /// </summary>
    public static async Task<string?> DownloadInstallerAsync(
        string downloadUrl,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            string tempPath = Path.Combine(
                Path.GetTempPath(),
                "GrimoireUpdate_" + Path.GetRandomFileName() + ".exe");

            using var response = await _http.GetAsync(
                downloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                ct);

            response.EnsureSuccessStatusCode();

            long? totalBytes = response.Content.Headers.ContentLength;
            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var file   = File.Create(tempPath);

            byte[] buffer = new byte[8192];
            long downloaded = 0;
            int read;

            while ((read = await stream.ReadAsync(buffer, ct)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
                downloaded += read;

                if (totalBytes.HasValue && totalBytes > 0)
                    progress?.Report((int)(downloaded * 100 / totalBytes.Value));
            }

            progress?.Report(100);
            return tempPath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Lance l'installateur téléchargé et ferme le Grimoire.
    /// L'installateur Inno Setup détecte la version précédente
    /// et la remplace automatiquement.
    /// </summary>
    public static void LaunchInstallerAndExit(string installerPath)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName  = installerPath,
                Arguments = "/SILENT",  // Installation silencieuse (Inno Setup)
                UseShellExecute = true,
                Verb = "runas"          // Admin pour installer dans Program Files
            });
        }
        catch { }

        Application.Exit();
    }
}
