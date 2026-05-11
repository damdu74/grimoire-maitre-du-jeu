using GameTrainer.Core;
using GameTrainer.UI;

namespace GameTrainer;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        CheckForUpdateAsync();
        Application.Run(new MainForm());
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
