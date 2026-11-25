// ============================================================
//  ÉDITEUR DE FICHIER DE SAUVEGARDE
//
//  Certains jeux (comme Adventure Capitalist) stockent leurs
//  données dans des fichiers JSON lisibles et modifiables
//  directement — sans avoir besoin de scanner la mémoire.
//
//  PROCÉDURE :
//  1. Ferme le jeu
//  2. Le Grimoire modifie le fichier .sav
//  3. Relance le jeu → les modifications sont appliquées
// ============================================================

using System.Text.Json;
using System.Text.Json.Nodes;

namespace GameTrainer.Core;

public class SaveFileEditor
{
    private readonly string _savePath;
    private JsonNode? _saveData;

    public bool IsLoaded => _saveData != null;

    public SaveFileEditor(string savePath)
    {
        _savePath = savePath;
    }

    // ── Charge le fichier de sauvegarde ──────────────────────
    public bool Load()
    {
        try
        {
            if (!File.Exists(_savePath)) return false;
            string json = File.ReadAllText(_savePath);
            _saveData = JsonNode.Parse(json);
            return _saveData != null;
        }
        catch { return false; }
    }

    // ── Sauvegarde les modifications ─────────────────────────
    public bool Save()
    {
        try
        {
            if (_saveData == null) return false;
            // Backup automatique avant modification
            string backup = _savePath + ".grimoire_backup";
            if (!File.Exists(backup))
                File.Copy(_savePath, backup);

            string json = _saveData.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_savePath, json);
            return true;
        }
        catch { return false; }
    }

    // ── Lit une valeur string ─────────────────────────────────
    public string? GetString(string key)
    {
        try { return _saveData?[key]?.GetValue<string>(); }
        catch { return null; }
    }

    // ── Modifie une valeur ────────────────────────────────────
    public bool SetValue(string key, string value)
    {
        try
        {
            if (_saveData == null) return false;
            // Essaie d'abord en double, puis en string
            if (double.TryParse(value, out double d))
                _saveData[key] = JsonValue.Create(d);
            else
                _saveData[key] = JsonValue.Create(value);
            return true;
        }
        catch { return false; }
    }

    public bool SetValue(string key, double value)
    {
        try
        {
            if (_saveData == null) return false;
            _saveData[key] = JsonValue.Create(value);
            return true;
        }
        catch { return false; }
    }

    // ── Parcourt les ventures (entreprises) ──────────────────
    public bool SetAllVentureValues(string ventureKey, double value)
    {
        try
        {
            if (_saveData == null) return false;
            // Les ventures sont dans un tableau "ventures" ou "ventureData"
            var ventures = _saveData["ventures"] as JsonArray
                        ?? _saveData["ventureData"] as JsonArray;
            if (ventures == null) return false;
            foreach (var v in ventures)
            {
                if (v == null) continue;
                v[ventureKey] = JsonValue.Create(value);
            }
            return true;
        }
        catch { return false; }
    }

    // ── Trouve le fichier de save d'Adventure Capitalist ─────
    public static string? FindAdventureCapitalistSave()
    {
        string[] steamIds = { "99183920", "102814417" };
        string[] steamPaths = {
            @"C:\Program Files (x86)\Steam\userdata",
            @"C:\Program Files\Steam\userdata",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                         "Steam", "userdata"),
        };

        foreach (string steamPath in steamPaths)
        {
            if (!Directory.Exists(steamPath)) continue;

            // Cherche dans tous les dossiers userdata
            var userDirs = Directory.GetDirectories(steamPath);
            foreach (var userDir in userDirs)
            {
                string savePath = Path.Combine(userDir, "346900", "remote",
                    "earth-gamestate-3.sav");
                if (File.Exists(savePath)) return savePath;

                // Ancien nom de fichier
                savePath = Path.Combine(userDir, "346900", "remote",
                    "earth-gamestate.sav");
                if (File.Exists(savePath)) return savePath;
            }
        }
        return null;
    }

    // ── Restaure le backup ────────────────────────────────────
    public bool RestoreBackup()
    {
        try
        {
            string backup = _savePath + ".grimoire_backup";
            if (!File.Exists(backup)) return false;
            File.Copy(backup, _savePath, overwrite: true);
            _saveData = null;
            return true;
        }
        catch { return false; }
    }
}
