using GameTrainer.Core;
using GameTrainer.UI;

namespace GameTrainer;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Capture toutes les exceptions non gérées
        Application.ThreadException += (s, e) =>
            MessageBox.Show($"Erreur UI :\n\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
                "Grimoire - Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            MessageBox.Show($"Erreur critique :\n\n{e.ExceptionObject}",
                "Grimoire - Erreur critique", MessageBoxButtons.OK, MessageBoxIcon.Error);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        try
        {
            CheckForUpdateAsync();
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Impossible de démarrer :\n\n{ex.Message}\n\n{ex.StackTrace}",
                "Grimoire - Erreur fatale",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static async void CheckForUpdateAsync()
    {
        try
        {
            await Task.Delay(2000);
            var update = await AutoUpdater.CheckForUpdateAsync();
            if (update == null) return;
            Application.OpenForms[0]?.Invoke(() =>
            {
                using var form = new UpdateForm(update);
                form.ShowDialog();
            });
        }
        catch { }
    }
}
