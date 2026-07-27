using System.IO;
using System.Windows;
using BlueCrane.Core;

namespace BlueCrane;

public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(AppInfo.DataFolder, "error.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // A crash before the window exists has nowhere to surface, so record it.
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log(args.ExceptionObject as Exception);

        DispatcherUnhandledException += (_, args) =>
        {
            Log(args.Exception);
            MessageBox.Show(args.Exception.Message, AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };
    }

    private static void Log(Exception? ex)
    {
        if (ex is null) return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"{DateTime.Now:u}  {ex}\n\n");
        }
        catch
        {
            // Logging must never be the thing that takes the process down.
        }
    }
}
