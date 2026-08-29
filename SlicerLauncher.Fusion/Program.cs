using System.Diagnostics;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            string? modelPath = null;

            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                try
                {
                    modelPath = Path.GetFullPath(args[0]);
                }
                catch
                {
                    ShowError("Fusion 360 passed an invalid file path.");
                    return;
                }

                if (!File.Exists(modelPath))
                {
                    ShowError(
                        "The model file received from Fusion 360 could not be found:\n\n" +
                        modelPath);
                    return;
                }

                string extension = Path.GetExtension(modelPath);
                if (!extension.Equals(".stl", StringComparison.OrdinalIgnoreCase) &&
                    !extension.Equals(".3mf", StringComparison.OrdinalIgnoreCase))
                {
                    ShowError(
                        "Fusion 360 passed an unsupported file type.\n\n" +
                        "SlicerLauncher supports STL and 3MF files.");
                    return;
                }
            }

            string aliasPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "WindowsApps",
                "SlicerLauncher.exe");

            if (!File.Exists(aliasPath))
            {
                ShowError(
                    "The Microsoft Store version of SlicerLauncher could not be found.\n\n" +
                    "Please install SlicerLauncher from the Microsoft Store first.");
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = aliasPath,
                UseShellExecute = false
            };

            if (!string.IsNullOrWhiteSpace(modelPath))
                psi.ArgumentList.Add(modelPath);

            Process? process = Process.Start(psi);

            if (process is null)
                ShowError("SlicerLauncher could not be started.");
        }
        catch (Exception ex)
        {
            ShowError("SlicerLauncher could not be started.\n\n" + ex.Message);
        }
    }

    private static void ShowError(string message)
    {
        MessageBox.Show(
            message,
            "SlicerLauncher Fusion Helper",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
