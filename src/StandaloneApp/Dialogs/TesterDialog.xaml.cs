using System.Windows;
using System.Windows.Input;

namespace Skippy.App.Dialogs;

public partial class TesterDialog : Window
{
    public TesterDialog(string nickname, string message)
    {
        this.InitializeComponent();

        this.TitleText.Text = $"★  Hi there {nickname}!  ★";

        this.BodyText.Text =
            "Thank you so much for all of the feedback and the time you've contributed to helping make this plugin better for everyone.\n\n"
            + "So... this is a lil' badge as a small token of my gratitude and appreciation, thanks lots!～";

        var hasMessage = !string.IsNullOrWhiteSpace(message)
                         && !message.Trim().Equals("N/A", StringComparison.OrdinalIgnoreCase);

        this.MessageText.Text = hasMessage ? $"“{message.Trim()}”" : string.Empty;
        this.MessageText.Visibility = hasMessage ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            this.DragMove();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => this.Close();
}
