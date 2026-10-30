using System.Windows;
using System.Windows.Threading;
using Skippy.App.Services;

namespace Skippy.App;

public partial class App : Application
{
    public static AppHost Host { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        this.DispatcherUnhandledException += this.OnDispatcherUnhandledException;

        Host = new AppHost(this.Dispatcher);

        var window = new MainWindow(Host);
        this.MainWindow = window;

        Host.Start();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Host?.Dispose();
        }
        catch
        {
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            Host?.Logger.Error(e.Exception, "未处理的 UI 异常");
        }
        catch
        {
        }

        MessageBox.Show(
            $"发生未处理的异常：\n\n{e.Exception.Message}\n\n详细信息已写入日志。",
            "Skippy",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}
