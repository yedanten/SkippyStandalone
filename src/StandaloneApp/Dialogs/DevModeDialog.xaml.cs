using System.Windows;
using System.Windows.Input;
using Skippy.App.Services;

namespace Skippy.App.Dialogs;

public partial class DevModeDialog : Window
{
    private readonly AppHost host;
    private bool busy;

    public DevModeDialog(AppHost host)
    {
        this.host = host;
        this.InitializeComponent();

        this.Loaded += (_, _) => this.PasswordInput.Focus();
    }

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            this.DragMove();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        this.DialogResult = false;
        this.Close();
    }

    private void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            this.OnConfirmClick(sender, e);
    }

    private async void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        if (this.busy)
            return;

        this.busy = true;
        this.ConfirmButton.IsEnabled = false;
        this.ConfirmButton.Content = "Checking…";
        this.StatusText.Visibility = Visibility.Visible;
        this.StatusText.Text = "正在验证密码…";

        var (ok, message) = await this.host.TryEnableDevModeAsync(this.PasswordInput.Password);

        this.StatusText.Text = message;
        this.StatusText.Foreground = ok
            ? (System.Windows.Media.Brush)this.FindResource("SuccessBrush")
            : (System.Windows.Media.Brush)this.FindResource("DangerBrush");

        this.busy = false;
        this.ConfirmButton.IsEnabled = true;
        this.ConfirmButton.Content = "Confirm";

        if (ok)
        {
            this.host.Notify(message, ToastKind.Success);
            this.DialogResult = true;
            this.Close();
        }
        else
        {
            this.host.Notify(message, ToastKind.Error);
        }
    }
}
