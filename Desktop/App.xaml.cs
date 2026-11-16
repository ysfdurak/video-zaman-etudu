using System.Windows;

namespace VideoZamanEtudu;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Öz-deneme (CI): Desktop.exe --self-test <demo klasörü> <çıktı klasörü>
        if (e.Args.Length >= 3 && e.Args[0] == "--self-test")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            int code = SelfTest.Run(e.Args[1], e.Args[2]);
            Shutdown(code);
            return;
        }
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}
