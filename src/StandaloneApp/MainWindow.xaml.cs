using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Skippy.App.Dialogs;
using Skippy.App.Services;
using Skippy.App.ViewModels;
using Skippy.App.Views;
using Skippy.Core.Skips;

namespace Skippy.App;

public partial class MainWindow : Window
{
    private sealed record NavDefinition(Category Category, string Title, string? Icon = null, bool Risky = false, bool SeparatorBefore = false);

    private sealed class NavEntry
    {
        public required NavDefinition Definition { get; init; }
        public required Button Button { get; init; }
        public Ellipse? Dot { get; init; }
        public TextBlock? Label { get; init; }
    }

    private static readonly NavDefinition[] Navigation =
    [
        new(Category.MSQRoulette, "MSQ Roulette"),
        new(Category.MassivePC, "Large-Scale Content"),
        new(Category.GoldSaucer, "Gold Saucer"),
        new(Category.CustomTalk, "NPC Dialogue"),
        new(Category.NormalCutscenes, "World Cutscenes"),
        new(Category.FeedBuddy, "Feed Buddy"),
        new(Category.RiskySkips, "Risky Skips", Icon: "\uE7BA", Risky: true, SeparatorBefore: true),
        new(Category.AutoParty, "Auto-Party Mode", Icon: "\uE716", SeparatorBefore: true),
        new(Category.Behaviour, "Behaviour", Icon: "\uE713"),
        new(Category.Logger, "Logger", Icon: "\uE7C3"),
        new(Category.Commands, "Commands", Icon: "\uE756", SeparatorBefore: true),
        new(Category.About, "About", Icon: "\uE946"),
    ];

    private readonly AppHost host;
    private readonly List<NavEntry> navEntries = [];
    private readonly DispatcherTimer toastTimer;
    private Category currentCategory = Category.MSQRoulette;

    private Brush accentBrush = Brushes.MediumPurple;
    private Brush mutedBrush = Brushes.Gray;
    private Brush textBrush = Brushes.White;
    private Brush navSelectedBrush = Brushes.DarkSlateBlue;

    public MainWindow(AppHost host)
    {
        this.host = host;

        this.InitializeComponent();

        this.accentBrush = (Brush)this.FindResource("AccentBrush");
        this.mutedBrush = (Brush)this.FindResource("MutedBrush");
        this.textBrush = (Brush)this.FindResource("TextBrush");
        this.navSelectedBrush = (Brush)this.FindResource("NavSelectedBg");

        this.toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        this.toastTimer.Tick += (_, _) =>
        {
            this.toastTimer.Stop();
            this.ToastPanel.Visibility = Visibility.Collapsed;
        };

        this.BuildNavigation();

        var startCategory = Enum.TryParse<Category>(host.Config.LastPage, out var parsed)
            ? parsed
            : Category.MSQRoulette;

        if (startCategory == Category.HookResearch && !host.Config.DevMode)
            startCategory = Category.MSQRoulette;

        this.Navigate(startCategory);

        host.StateChanged += this.OnHostStateChanged;
        host.ConfigChanged += this.OnHostConfigChanged;
        host.Notification += this.OnHostNotification;

        this.UpdatePowerButton();
        this.UpdateStatus();
        this.UpdateBadge();
    }

    // ══ 导航 ═══════════════════════════════════════════════════════════════

    private void BuildNavigation()
    {
        this.NavPanel.Children.Clear();
        this.navEntries.Clear();

        foreach (var definition in Navigation)
        {
            if (definition.SeparatorBefore)
            {
                this.NavPanel.Children.Add(new Border
                {
                    Height = 1,
                    Background = new SolidColorBrush(Color.FromArgb(0x66, 0x4D, 0x4D, 0x8C)),
                    Margin = new Thickness(6, 4, 6, 4),
                });
            }

            this.NavPanel.Children.Add(this.CreateNavButton(definition));
        }

        if (this.host.Config.DevMode)
        {
            var research = new NavDefinition(Category.HookResearch, "Hook Research", Icon: "\uE9D9");
            this.NavPanel.Children.Add(this.CreateNavButton(research));
        }

        this.RefreshNavigationState();
    }

    private Button CreateNavButton(NavDefinition definition)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Ellipse? dot = null;
        TextBlock? icon = null;

        if (definition.Icon is null)
        {
            dot = new Ellipse
            {
                Width = 9,
                Height = 9,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0),
                Fill = Brushes.Firebrick,
            };

            grid.Children.Add(dot);
        }
        else
        {
            icon = new TextBlock
            {
                Text = definition.Icon,
                FontFamily = (FontFamily)this.FindResource("IconFont"),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(3, 0, 0, 0),
                Foreground = definition.Risky ? (Brush)this.FindResource("DangerBrush") : this.mutedBrush,
            };

            grid.Children.Add(icon);
        }

        var label = new TextBlock
        {
            Text = definition.Title,
            FontSize = 12.5,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = definition.Risky ? (Brush)this.FindResource("DangerBrush") : this.textBrush,
        };

        Grid.SetColumn(label, 1);
        grid.Children.Add(label);

        var button = new Button
        {
            Style = (Style)this.FindResource("NavButton"),
            Content = grid,
            Margin = new Thickness(0, 1, 0, 1),
        };

        button.Click += (_, _) => this.RequestNavigate(definition.Category);

        this.navEntries.Add(new NavEntry
        {
            Definition = definition,
            Button = button,
            Dot = dot,
            Label = label,
        });

        return button;
    }

    private void RequestNavigate(Category category)
    {
        if (category == Category.RiskySkips && !this.host.Config.HideWarning)
        {
            var dialog = new RiskyWarningDialog { Owner = this };
            if (dialog.ShowDialog() != true)
                return;

            if (dialog.DontShowAgain)
            {
                this.host.Config.HideWarning = true;
                this.host.SaveConfig();
            }
        }

        this.Navigate(category);
    }

    private void Navigate(Category category)
    {
        if (category == Category.HookResearch && !this.host.Config.DevMode)
            category = Category.MSQRoulette;

        this.currentCategory = category;
        this.PageHost.Content = this.CreatePage(category);

        this.host.Config.LastPage = category.ToString();
        this.host.SaveConfig();

        this.RefreshNavigationState();
    }

    private UIElement CreatePage(Category category) => category switch
    {
        Category.Logger => new LoggerView(this.host),
        Category.Commands => new CommandsView(this.host),
        Category.About => new AboutView(this.host),
        Category.HookResearch => new SettingsPageView(this.host, Category.HookResearch),
        _ => new SettingsPageView(this.host, category),
    };

    private void RefreshNavigationState()
    {
        foreach (var entry in this.navEntries)
        {
            var selected = entry.Definition.Category == this.currentCategory;

            entry.Button.Background = selected ? this.navSelectedBrush : Brushes.Transparent;

            if (entry.Label is not null)
            {
                entry.Label.Foreground = entry.Definition.Risky
                    ? (Brush)this.FindResource("DangerBrush")
                    : selected ? this.accentBrush : this.textBrush;
            }

            if (entry.Dot is not null)
                entry.Dot.Fill = this.DotBrushFor(entry.Definition.Category);
        }
    }

    private Brush DotBrushFor(Category category)
    {
        var config = this.host.Config;

        bool active = category switch
        {
            Category.MSQRoulette => config.SkipMSQRoulette || config.ExemptPrae || config.ExemptCastrum || config.ExemptPorta,
            Category.MassivePC => config.SkipMassivePC,
            Category.GoldSaucer => config.SkipGoldSaucer || config.ExemptChocoboRace || config.ExemptVerminion
                                   || config.ExemptTripleTriad || config.ExemptFallGuys || config.ExemptAirForceOne
                                   || config.ExemptMahjong || config.ExemptKeyboundBrawler,
            Category.CustomTalk => config.SkipCustomTalk || config.ExemptSubmarines,
            Category.NormalCutscenes => config.SkipNormalCutscenes,
            Category.FeedBuddy => config.SkipFeedBuddy,
            _ => false,
        };

        if (category == Category.MSQRoulette && config.AutoEnable4Man)
            return new SolidColorBrush(Color.FromRgb(0xE6, 0xD9, 0x8C));

        if (this.host.NativeError != Core.Interop.SharedStateLayout.NativeError.None && active)
            return (Brush)this.FindResource("WarningBrush");

        return active
            ? (Brush)this.FindResource("SuccessBrush")
            : new SolidColorBrush(Color.FromRgb(0xCC, 0x33, 0x33));
    }

    // ══ 状态更新 ═══════════════════════════════════════════════════════════

    private void OnHostStateChanged()
    {
        this.UpdatePowerButton();
        this.UpdateStatus();
        this.UpdateBadge();
        this.RefreshNavigationState();
    }

    private void OnHostConfigChanged()
    {
        this.Dispatcher.BeginInvoke(() =>
        {
            if (this.currentCategory == Category.HookResearch && !this.host.Config.DevMode)
                this.Navigate(Category.MSQRoulette);
            else
                this.PageHost.Content = this.CreatePage(this.currentCategory);

            this.BuildNavigation();
            this.UpdateStatus();
        }, DispatcherPriority.Background);
    }

    private void OnHostNotification(string message, ToastKind kind)
    {
        this.Dispatcher.BeginInvoke(() => this.ShowToast(message, kind), DispatcherPriority.Background);
    }

    private void UpdatePowerButton()
    {
        var hasError = this.host.NativeError == Core.Interop.SharedStateLayout.NativeError.SignatureScanFailed
                       || this.host.NativeError == Core.Interop.SharedStateLayout.NativeError.UnsupportedGameVersion;

        this.PowerImage.Opacity = hasError ? 0.9 : this.host.Config.IsEnabled ? 1.0 : 0.55;
        this.PowerDim.Visibility = this.host.Config.IsEnabled || hasError ? Visibility.Collapsed : Visibility.Visible;
        this.PowerBorder.BorderBrush = hasError
            ? (Brush)this.FindResource("WarningBrush")
            : this.host.Config.IsEnabled
                ? this.accentBrush
                : new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x5A));

        this.PowerBorder.ToolTip = hasError
            ? "[ERROR] — Cutscene offsets not found"
            : this.host.Config.IsEnabled
                ? "[ENABLED] — Click to Disable Skippy"
                : "[DISABLED] — Click to Enable Skippy";
    }

    private void UpdateStatus()
    {
        var monitor = this.host.Monitor;
        var snapshot = this.host.Snapshot;

        string text;

        if (this.host.NativeError is Core.Interop.SharedStateLayout.NativeError.SignatureScanFailed
            or Core.Interop.SharedStateLayout.NativeError.UnsupportedGameVersion)
        {
            text = "原生组件：关键签名缺失，Skippy 无法工作（游戏可能已更新）。请导出日志反馈。";
        }
        else if (!monitor.GameRunning)
        {
            text = this.host.Config.AutoInject
                ? "等待游戏启动…（检测到 ffxiv_dx11.exe 后将自动注入）"
                : "等待游戏启动…（自动注入已关闭）";
        }
        else if (monitor.InjectionError is not null)
        {
            text = $"游戏已运行（PID {monitor.GameProcessId}）— {monitor.InjectionError}";
        }
        else if (!this.host.NativeReady)
        {
            text = $"游戏已运行（PID {monitor.GameProcessId}）— 正在等待原生组件就绪…";
        }
        else
        {
            var territory = this.host.TerritoryName;
            var use = snapshot.IntendedUse;
            var party = snapshot.PartySize > 1 ? $"  ·  小队 {snapshot.PartySize} 人" : string.Empty;
            var tag = this.host.Coordination.Decision.ActiveTag;
            var tagText = tag is null ? "无活跃跳过" : $"跳过类别：{tag}";

            text = this.host.Config.IsEnabled
                ? $"已连接  ·  地域 {snapshot.TerritoryId}（{territory}） · IntendedUse {use}（{(byte)use}）{party}  ·  {tagText}"
                : $"已连接  ·  Skippy 已禁用  ·  地域 {snapshot.TerritoryId}（{territory}）";
        }

        this.StatusText.Text = text;
        this.StatusText.Foreground = (Brush)this.FindResource("MutedBrush");
    }

    private void UpdateBadge()
    {
        this.BadgeOverlayButton.Visibility = this.host.Config.HideBadge ? Visibility.Collapsed : Visibility.Visible;
        this.BadgeImage.Source = new System.Windows.Media.Imaging.BitmapImage(
            new Uri(this.host.TesterInfo is null ? "pack://application:,,,/Assets/badge_grey.png" : "pack://application:,,,/Assets/badge.png"));
        this.BadgeToggleButton.ToolTip = this.host.Config.HideBadge ? "Show Badge" : "Hide Badge";
    }

    private void ShowToast(string message, ToastKind kind)
    {
        this.ToastText.Text = message;
        this.ToastText.Foreground = kind switch
        {
            ToastKind.Error => (Brush)this.FindResource("DangerBrush"),
            ToastKind.Warning => (Brush)this.FindResource("WarningBrush"),
            ToastKind.Success => (Brush)this.FindResource("SuccessBrush"),
            _ => this.textBrush,
        };

        this.ToastPanel.Visibility = Visibility.Visible;
        this.toastTimer.Stop();
        this.toastTimer.Start();
    }

    // ══ 交互 ═══════════════════════════════════════════════════════════════

    private void OnPowerClick(object sender, MouseButtonEventArgs e)
    {
        if (this.host.NativeError is Core.Interop.SharedStateLayout.NativeError.SignatureScanFailed
            or Core.Interop.SharedStateLayout.NativeError.UnsupportedGameVersion)
        {
            this.ShowToast("当前游戏版本的签名缺失，无法启用。请导出日志反馈。", ToastKind.Error);
            return;
        }

        this.host.TogglePower();
    }

    private void OnKofiClick(object sender, RoutedEventArgs e) => this.host.OpenUrl("https://ko-fi.com/boxu_chan");

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => this.Close();

    private void OnBadgeToggleClick(object sender, RoutedEventArgs e)
    {
        this.host.Config.HideBadge = !this.host.Config.HideBadge;
        this.host.NotifyConfigChanged();
        this.UpdateBadge();
    }

    private void OnDevModeClick(object sender, RoutedEventArgs e)
    {
        if (this.host.Config.DevMode)
        {
            this.host.DisableDevMode();
            this.BuildNavigation();
            return;
        }

        var dialog = new DevModeDialog(this.host) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            this.BuildNavigation();
            this.Navigate(Category.HookResearch);
        }
    }

    private void OnBadgeOverlayClick(object sender, RoutedEventArgs e)
    {
        if (this.host.TesterInfo is { } tester)
        {
            new TesterDialog(tester.Nickname, tester.Message) { Owner = this }.ShowDialog();
        }
        else
        {
            this.host.OpenUrl("https://ko-fi.com/boxu_chan");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        this.host.StateChanged -= this.OnHostStateChanged;
        this.host.ConfigChanged -= this.OnHostConfigChanged;
        this.host.Notification -= this.OnHostNotification;
        this.toastTimer.Stop();

        base.OnClosed(e);
        Application.Current.Shutdown();
    }
}



