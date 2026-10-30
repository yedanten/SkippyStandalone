using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Skippy.App.ViewModels;

/// <summary>设置页里的一个开关项。修改 <see cref="IsChecked"/> 会立刻触发 <see cref="Changed"/>。</summary>
public sealed class ToggleItem : INotifyPropertyChanged
{
    private bool isChecked;

    private ToggleItem(string label, string description, bool initialValue, Action<bool>? changed)
    {
        this.Label = label;
        this.Description = description;
        this.isChecked = initialValue;
        this.Changed = changed;
    }

    public string Label { get; }

    public string Description { get; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>可选的行尾提示（例如 “[Work in Progress]”）。</summary>
    public string? Suffix { get; set; }

    public Action<bool>? Changed { get; }

    public bool IsChecked
    {
        get => this.isChecked;
        set
        {
            if (this.isChecked == value)
                return;

            this.isChecked = value;
            this.OnPropertyChanged();
            this.Changed?.Invoke(value);
        }
    }

    public static ToggleItem Create(string label, string description, bool initialValue, Action<bool>? changed) =>
        new(label, description, initialValue, changed);

    /// <summary>用于原插件里的 [Work in Progress] 占位项。</summary>
    public static ToggleItem WorkInProgress(string label) => new(label, "该功能尚未实现。", false, null)
    {
        IsEnabled = false,
        Suffix = "[Work in Progress]",
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>设置页的一个分区（标题 + 说明 + 开关列表）。</summary>
public sealed class SettingsSection
{
    public string Header { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public List<ToggleItem> Items { get; init; } = [];
}

/// <summary>一个完整的设置页。</summary>
public sealed class SettingsPageModel
{
    public string Title { get; init; } = string.Empty;

    public string Intro { get; init; } = string.Empty;

    public string? Notice { get; init; }

    public string? NoticeColor { get; init; }

    public bool ShowRiskyBanner { get; init; }

    public List<SettingsSection> Sections { get; init; } = [];
}
