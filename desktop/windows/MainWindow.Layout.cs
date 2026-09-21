using static SmartSearch.Desktop.Localization;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.System;

namespace SmartSearch.Desktop;

public sealed partial class MainWindow
{
    private readonly Dictionary<string, double> _splitWidths = [];
    private string _noticeTitle = string.Empty;
    private bool _hasNotice;
    private bool _detailsOpen;

    private static StackPanel PagePanel() => new()
    {
        Spacing = 24, MaxWidth = 840, HorizontalAlignment = HorizontalAlignment.Stretch
    };

    private ScrollViewer Scroll(UIElement content) => PaneScroll(content, _currentPage, 24);

    private ScrollViewer PaneScroll(UIElement content, string key, double padding = 20)
    {
        var scroll = new ScrollViewer
        {
            Content = content, Padding = new Thickness(padding),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        scroll.Loaded += (_, _) => scroll.ChangeView(null, _pageOffsets.GetValueOrDefault(key), null, true);
        scroll.ViewChanged += (_, _) => _pageOffsets[key] = scroll.VerticalOffset;
        return scroll;
    }

    private Grid SplitWorkspace(string key, FrameworkElement leading, FrameworkElement detail,
        double initialWidth, double minimumWidth, double maximumWidth, double detailMinimum = 400)
    {
        const double dividerWidth = 8;
        if (!_splitWidths.ContainsKey(key))
            _splitWidths[key] = double.TryParse(ReadSetting("split:" + key), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var saved) ? Math.Clamp(saved, minimumWidth, maximumWidth) : initialWidth;
        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_splitWidths[key]) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(dividerWidth) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0) });
        var divider = new Thumb { Style = UiStyle("PaneDividerStyle") };
        AutomationProperties.SetName(divider, L("调整分栏宽度"));
        ToolTipService.SetToolTip(divider, L("拖动调整宽度；方向键微调"));
        layout.Children.Add(leading);
        layout.Children.Add(divider);
        layout.Children.Add(detail);
        var compact = false;
        void Arrange()
        {
            if (layout.ActualWidth <= 0) return;
            compact = layout.ActualWidth < minimumWidth + detailMinimum + dividerWidth;
            layout.ColumnDefinitions[0].Width = compact ? new GridLength(1, GridUnitType.Star) :
                new GridLength(Math.Clamp(_splitWidths[key], minimumWidth,
                    Math.Min(maximumWidth, layout.ActualWidth - detailMinimum - dividerWidth)));
            layout.ColumnDefinitions[1].Width = new GridLength(compact ? 0 : dividerWidth);
            layout.ColumnDefinitions[2].Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            layout.RowDefinitions[0].Height = new GridLength(compact ? 0.36 : 1, GridUnitType.Star);
            layout.RowDefinitions[1].Height = compact ? new GridLength(0.64, GridUnitType.Star) : new GridLength(0);
            Grid.SetColumn(divider, 1);
            Grid.SetColumn(detail, compact ? 0 : 2);
            Grid.SetRow(detail, compact ? 1 : 0);
            divider.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        }
        void Resize(double delta)
        {
            if (compact || layout.ActualWidth <= 0) return;
            _splitWidths[key] = Math.Clamp(layout.ColumnDefinitions[0].ActualWidth + delta, minimumWidth,
                Math.Min(maximumWidth, layout.ActualWidth - detailMinimum - dividerWidth));
            Arrange();
        }
        void Persist() => SaveSetting("split:" + key, _splitWidths[key].ToString(CultureInfo.InvariantCulture));
        divider.DragDelta += (_, args) => Resize(args.HorizontalChange);
        divider.DragCompleted += (_, _) => Persist();
        divider.KeyDown += (_, args) =>
        {
            if (args.Key is not (VirtualKey.Left or VirtualKey.Right)) return;
            Resize(args.Key == VirtualKey.Left ? -16 : 16);
            Persist();
            args.Handled = true;
        };
        layout.SizeChanged += (_, _) => Arrange();
        return layout;
    }

    private static Border Divider() => new() { Style = UiStyle("SectionDividerStyle") };

    private static UIElement SettingRow(string title, string description, UIElement control)
    {
        var label = new StackPanel { Spacing = 4 };
        label.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        if (description.Length > 0) label.Children.Add(Secondary(description));
        var row = new Grid { ColumnSpacing = 20, RowSpacing = 8, Padding = new Thickness(0, 4, 0, 4) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        row.Children.Add(label);
        row.Children.Add(control);
        if (control is FrameworkElement input)
        {
            input.VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetName(input, title);
            row.SizeChanged += (_, args) =>
            {
                var stacked = args.NewSize.Width < 460 && control is not ToggleSwitch;
                Grid.SetColumn(input, stacked ? 0 : 1);
                Grid.SetRow(input, stacked ? 1 : 0);
                input.HorizontalAlignment = stacked ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            };
        }
        return row;
    }

    private static ToggleSwitch CompactSwitch(string label, bool value)
    {
        var toggle = new ToggleSwitch { IsOn = value, OnContent = string.Empty, OffContent = string.Empty, MinWidth = 0 };
        AutomationProperties.SetName(toggle, label);
        return toggle;
    }

    private static StackPanel SettingsSection(string title, string subtitle, params UIElement[] content)
    {
        var section = new StackPanel { Spacing = 12 };
        section.Children.Add(SectionHeading(title));
        if (subtitle.Length > 0) section.Children.Add(Secondary(subtitle));
        foreach (var child in content) section.Children.Add(child);
        return section;
    }

    private async Task ShowDetailsAsync(string title, UIElement content)
    {
        if (_detailsOpen) return;
        var dialog = new ContentDialog
        {
            XamlRoot = DialogRoot, RequestedTheme = ((FrameworkElement)Content).ActualTheme,
            Title = title, CloseButtonText = L("完成"),
            Content = new ScrollViewer { Content = content, MaxHeight = 540, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        };
        _detailsOpen = true;
        try { await dialog.ShowAsync(); }
        finally
        {
            if (dialog.Content is ScrollViewer scroll) scroll.Content = null;
            dialog.Content = null;
            _detailsOpen = false;
        }
    }

    private void OnClearFeedbackClick(object sender, RoutedEventArgs args) => ClearNotice();

    private void ClearNotice()
    {
        FeedbackFlyout.Hide();
        _hasNotice = false;
        _noticeTitle = string.Empty;
        FeedbackTitle.Text = FeedbackMessage.Text = string.Empty;
        FeedbackButton.IsEnabled = false;
    }

    private async void OnRefreshWorkspaceClick(object sender, RoutedEventArgs args)
    {
        await RunOperationAsync(_backend.IsConnected ? "state" : "connect",
            _backend.IsConnected ? () => RefreshStateAsync() : ConnectAsync);
    }

    private void ShowNotice(string title, string message, InfoBarSeverity severity)
    {
        _hasNotice = true;
        _noticeTitle = title;
        FeedbackTitle.Text = title;
        FeedbackMessage.Text = message;
        FeedbackIcon.Symbol = severity is InfoBarSeverity.Error or InfoBarSeverity.Warning ? Symbol.Important : Symbol.Message;
        FeedbackButton.IsEnabled = true;
        if (FeedbackButton.IsLoaded) FeedbackFlyout.ShowAt(FeedbackButton);
        else DispatcherQueue.TryEnqueue(() => { if (_hasNotice && FeedbackButton.IsLoaded) FeedbackFlyout.ShowAt(FeedbackButton); });
    }

    private void UpdateWorkspaceHeader()
    {
        if (WorkspaceTitle is null) return;
        WorkspaceTitle.Text = _currentPage switch
        {
            "providers" => L("服务商"), "search" => L("搜索与研究"), "activity" => L("活动"),
            "ai" => L("更新 Skills"), "settings" => L("设置与关于"), _ => L("概览")
        };
        FeedbackHeading.Text = L("操作提示");
        ClearFeedbackButton.Content = L("清除提示");
        AutomationProperties.SetName(FeedbackButton, L("操作提示"));
        ToolTipService.SetToolTip(FeedbackButton, L("查看操作提示"));
        AutomationProperties.SetName(RefreshWorkspaceButton, L("刷新本机状态"));
        ToolTipService.SetToolTip(RefreshWorkspaceButton, L("刷新本机状态"));
    }
}
