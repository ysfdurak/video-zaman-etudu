using System.Windows;

namespace VideoZamanEtudu;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Öz-deneme (CI): VideoZamanEtudu.exe --self-test <demo klasörü> <çıktı klasörü>
        if (e.Args.Length >= 3 && e.Args[0] == "--self-test")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            int code = SelfTest.Run(e.Args[1], e.Args[2]);
            Shutdown(code);
            return;
        }
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            ShowError(args.Exception);
        };
        try
        {
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            ShowError(ex);
            Shutdown(1);
        }
    }

    /// <summary>Ham .NET mesajı yerine ne yapılacağını söyler; ayrıntı en altta kalır.</summary>
    internal static void ShowError(Exception ex)
    {
        var root = ex;
        while (root.InnerException != null && root is System.Reflection.TargetInvocationException or TypeInitializationException) root = root.InnerException;
        bool missing = root is System.IO.FileNotFoundException or System.IO.FileLoadException or DllNotFoundException or BadImageFormatException;
        string advice = missing
            ? "Uygulamanın yanındaki dosyalardan biri bulunamadı. ZIP'in içinden çalıştırıyorsanız önce ZIP'e sağ tıklayıp \"Tümünü ayıkla\" deyin ve ayıklanan klasördeki VideoZamanEtudu.exe dosyasını açın.\n\n" +
              "A file next to the application is missing. If you are running it from inside the ZIP, right-click the ZIP, choose \"Extract all\" and open VideoZamanEtudu.exe in the extracted folder."
            : "Beklenmeyen bir hata oluştu. Etütleriniz bu bilgisayarda kayıtlı kalır; uygulamayı yeniden açın. Sorun sürerse aşağıdaki ayrıntıyı bildirin.\n\n" +
              "An unexpected error occurred. Your studies stay saved on this computer; reopen the application. If it keeps happening, report the detail below.";
        MessageBox.Show(advice + "\n\n" + root.GetType().Name + ": " + root.Message, "Video İş Etüdü", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
