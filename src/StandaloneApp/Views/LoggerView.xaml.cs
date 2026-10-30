using System.Windows;
using System.Windows.Controls;
using Skippy.App.Services;

namespace Skippy.App.Views;

public partial class LoggerView : UserControl
{
    private readonly AppHost host;

    public LoggerView(AppHost host)
    {
        this.host = host;
        this.InitializeComponent();

        this.UserIdText.Text = this.host.GetUserId();
        this.LogPathText.Text = this.host.Logger.LogFile ?? "（本次运行尚未创建日志文件）";
    }

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        var path = this.host.ExportTroubleshootingLog();
        if (path is not null)
            this.LogPathText.Text = path;
    }

    private void OnContactClick(object sender, RoutedEventArgs e) =>
        this.host.OpenUrl("https://discord.gg/punishxiv");

    private void OnCopyIdClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(this.host.GetUserId());
            this.host.Notify("User ID 已复制到剪贴板。", ToastKind.Success);
        }
        catch (Exception ex)
        {
            this.host.Notify($"复制失败：{ex.Message}", ToastKind.Error);
        }
    }
}
