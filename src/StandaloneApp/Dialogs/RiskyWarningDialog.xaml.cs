using System.Windows;
using System.Windows.Input;

namespace Skippy.App.Dialogs;

public partial class RiskyWarningDialog : Window
{
    public RiskyWarningDialog()
    {
        this.InitializeComponent();
    }

    public bool DontShowAgain => this.DontShowAgainCheck.IsChecked == true;

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

    private void OnAgreeClick(object sender, RoutedEventArgs e)
    {
        this.DialogResult = true;
        this.Close();
    }
}
