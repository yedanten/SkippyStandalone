using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Skippy.App.Services;
using Skippy.App.ViewModels;

namespace Skippy.App.Views;

public partial class SettingsPageView : UserControl
{
    private readonly AppHost host;

    public SettingsPageView(AppHost host, Category category)
    {
        this.host = host;
        this.InitializeComponent();

        var autoParty = host.Config.AutoEnable4Man;
        var model = SettingsPageBuilder.Build(category, host, autoParty);

        this.BuildPage(model);
    }

    private void BuildPage(SettingsPageModel model)
    {
        this.PageContent.Children.Clear();

        if (!string.IsNullOrEmpty(model.Title))
        {
            this.PageContent.Children.Add(new TextBlock
            {
                Text = model.Title,
                Style = (Style)this.FindResource("PageTitleText"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                TextAlignment = TextAlignment.Center,
            });

            this.PageContent.Children.Add(new Separator { Style = (Style)this.FindResource("SeparatorLine") });
            this.PageContent.Children.Add(new Border { Height = 6 });
        }

        if (model.ShowRiskyBanner)
            this.PageContent.Children.Add(this.CreateRiskyBanner());

        if (!string.IsNullOrEmpty(model.Intro))
            this.PageContent.Children.Add(this.CreateBodyText(model.Intro));

        if (!string.IsNullOrEmpty(model.Notice))
        {
            this.PageContent.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x1E, 0xB0, 0xB0, 0x50)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xB0, 0xB0, 0x50)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 10, 0, 8),
                Child = this.CreateBodyText(model.Notice, model.NoticeColor),
            });
        }

        foreach (var section in model.Sections)
            this.AddSection(section);
    }

    private void AddSection(SettingsSection section)
    {
        if (!string.IsNullOrEmpty(section.Header))
        {
            this.PageContent.Children.Add(new TextBlock
            {
                Text = section.Header,
                Style = (Style)this.FindResource("SectionTitleText"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 18, 0, 4),
            });

            this.PageContent.Children.Add(new Separator { Style = (Style)this.FindResource("SeparatorLine") });
        }

        if (!string.IsNullOrEmpty(section.Description))
        {
            this.PageContent.Children.Add(this.CreateBodyText(section.Description));
            this.PageContent.Children.Add(new Border { Height = 2 });
        }

        foreach (var item in section.Items)
            this.PageContent.Children.Add(this.CreateToggleRow(item));
    }

    private FrameworkElement CreateToggleRow(ToggleItem item)
    {
        var checkBox = new CheckBox
        {
            Style = (Style)this.FindResource("FeatureCheckBox"),
            Content = new TextBlock
            {
                Text = item.Label,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center,
            },
            IsEnabled = item.IsEnabled,
            DataContext = item,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 0),
        };

        checkBox.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(ToggleItem.IsChecked))
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        });

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 3, 0, 10),
        };

        row.Children.Add(checkBox);

        if (!string.IsNullOrEmpty(item.Suffix))
        {
            row.Children.Add(new TextBlock
            {
                Text = item.Suffix,
                Foreground = (Brush)this.FindResource("MutedBrush"),
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
            });
        }

        var help = new TextBlock
        {
            Style = (Style)this.FindResource("HelpIconText"),
            ToolTip = item.Description,
        };

        row.Children.Add(help);

        return row;
    }

    private FrameworkElement CreateRiskyBanner()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x59, 0x14, 0x0A)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0xE6, 0xCC, 0x33, 0x1A)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(14, 10, 14, 12),
            Margin = new Thickness(0, 8, 0, 18),
        };

        var panel = new StackPanel();

        var title = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        title.Children.Add(new TextBlock
        {
            Text = "\uE7BA",
            FontFamily = (FontFamily)this.FindResource("IconFont"),
            FontSize = 20,
            Foreground = (Brush)this.FindResource("DangerBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });

        title.Children.Add(new TextBlock
        {
            Text = "Warning",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)this.FindResource("DangerBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        });

        panel.Children.Add(title);

        panel.Children.Add(new TextBlock
        {
            Text = "All of the skips underneath are experimental and may carry a ban risk.",
            Foreground = new SolidColorBrush(Color.FromArgb(0xE6, 0xE6, 0xBF, 0xB2)),
            FontSize = 12.5,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 8, 0, 2),
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Please only enable them if you accept full responsibility.",
            Foreground = new SolidColorBrush(Color.FromArgb(0xE6, 0xE6, 0xBF, 0xB2)),
            FontSize = 12.5,
            TextAlignment = TextAlignment.Center,
        });

        border.Child = panel;
        return border;
    }

    private TextBlock CreateBodyText(string text, string? color = null)
    {
        var block = new TextBlock
        {
            Text = text,
            Style = (Style)this.FindResource("BodyText"),
            Margin = new Thickness(0, 6, 0, 6),
        };

        if (!string.IsNullOrWhiteSpace(color))
        {
            try
            {
                block.Foreground = (Brush)new BrushConverter().ConvertFromString(color)!;
            }
            catch
            {
                // 颜色解析失败时保持默认前景色。
            }
        }

        return block;
    }
}



