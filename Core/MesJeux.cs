namespace GameTrainer.Core;

// La liste des jeux affichés dans le Grimoire, choisie par l'utilisateur.
// La détection (Steam, Epic, GOG...) sert seulement à savoir si chacun est installé.
public static class MesJeux
{
    public record Jeu(string Nom, string Exe, int? SteamAppId = null);

    public static readonly List<Jeu> Liste = new()
    {
        new("Total War: NAPOLEON – Definitive Edition", "Napoleon.exe", 34030),
    };

    // Ma liste, chaque jeu relié à sa détection s'il est installé
    public static List<DetectedGame> Construire(List<DetectedGame> detectes)
    {
        var res = new List<DetectedGame>();
        foreach (var j in Liste)
        {
            var d = detectes.FirstOrDefault(g => j.SteamAppId != null && g.SteamAppId == j.SteamAppId)
                 ?? detectes.FirstOrDefault(g => g.ExecutableName.Equals(j.Exe, StringComparison.OrdinalIgnoreCase));
            if (d != null)
            {
                d.Name = j.Nom;
                d.ExecutableName = j.Exe;
                d.SteamAppId ??= j.SteamAppId;
                res.Add(d);
            }
            else res.Add(new DetectedGame
            {
                Name = j.Nom, ExecutableName = j.Exe, SteamAppId = j.SteamAppId,
                Source = "Non installé", IsInstalled = false,
            });
        }
        return res;
    }
}
