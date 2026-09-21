using static SmartSearch.Desktop.Localization;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.System;

namespace SmartSearch.Desktop;

public sealed partial class MainWindow
{
    private string? _settingsAnchor;

    private UIElement BuildSettingsPage()
    {
        var panel = PagePanel();
        var identity = new Grid { ColumnSpacing = 16 };
        identity.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        identity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        identity.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        identity.Children.Add(new Image { Source = new BitmapImage(new Uri("ms-appx:///Assets/smart-search.png")), Width = 48, Height = 48 });
        var product = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        product.Children.Add(SectionHeading("Smart Search"));
        product.Children.Add(Secondary(L("版本 {0}", Text(_state, "version", L("未连接")))));
        product.Children.Add(Secondary("konbakuyomu/smartsearch"));
        Grid.SetColumn(product, 1);
        identity.Children.Add(product);
        var project = new HyperlinkButton { Content = "GitHub", NavigateUri = new Uri("https://github.com/konbakuyomu/smartsearch"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(project, 2);
        identity.Children.Add(project);
        panel.Children.Add(Card(identity));

        var theme = new ComboBox { MinWidth = 144 };
        foreach (var label in new[] { L("跟随系统"), L("浅色"), L("深色") }) theme.Items.Add(label);
        theme.SelectedIndex = (ReadSetting("theme") ?? "auto") switch { "light" => 1, "dark" => 2, _ => 0 };
        theme.SelectionChanged += (_, _) =>
        {
            var value = theme.SelectedIndex switch { 1 => "light", 2 => "dark", _ => "auto" };
            SaveSetting("theme", value);
            ApplyTheme(value);
        };
        var preferences = new StackPanel { Spacing = 8 };
        preferences.Children.Add(SettingRow(L("界面语言"), L("App 与独立 CLI 分别保存语言选择。环境写入期间请等待操作完成。"), BuildLanguagePicker()));
        preferences.Children.Add(Divider());
        preferences.Children.Add(SettingRow(L("外观"), string.Empty, theme));
        preferences.Children.Add(Divider());
        preferences.Children.Add(SettingRow(L("当前配置目录"), Text(_state, "config_dir", Text(_state, "config_path", L("未连接"))),
            ActionButton(L("选择配置目录…"), SelectConfigDirectoryAsync, operationKey: "profile", busyText: L("切换中…"))));
        panel.Children.Add(SettingsSection(L("通用"), L("管理语言和配置目录。"), Card(preferences)));

        _autoUpdateSwitch = CompactSwitch(L("自动检查更新"), Bool(_updates, "auto_check", true));
        _autoUpdateSwitch.Toggled += async (_, _) =>
        {
            if (!_settingAutoUpdate) await UpdateRequestAsync("updates.auto", new { enabled = _autoUpdateSwitch.IsOn });
        };
        _updateCheckSummary = Secondary(string.Empty);
        _appUpdateSummary = Body(string.Empty);
        _cliUpdateSummary = Body(string.Empty);
        _downloadSummary = Secondary(string.Empty);
        _cliUpdateLog = DataText(string.Empty);
        _downloadProgress = new ProgressBar { Minimum = 0, Maximum = 100, Visibility = Visibility.Collapsed };
        var updates = new StackPanel { Spacing = 8 };
        updates.Children.Add(SettingRow(L("自动检查更新"), L("每 24 小时检查，点击才下载"), _autoUpdateSwitch));
        updates.Children.Add(ActionRow(ActionButton(L("检查更新"), CheckForUpdateAsync, operationKey: "updates-check", busyText: L("检查中…")),
            ActionButton(L("刷新已安装版本"), () => RefreshStateAsync(), operationKey: "state", busyText: L("刷新中…"))));
        updates.Children.Add(_updateCheckSummary);
        updates.Children.Add(Divider());
        updates.Children.Add(_appUpdateSummary);
        updates.Children.Add(_downloadSummary);
        updates.Children.Add(_downloadProgress);
        updates.Children.Add(ActionRow(
            ActionButton(L("下载安装包"), () => UpdateRequestAsync("updates.download"), primary: true, operationKey: "updates-download", busyText: L("下载中…")),
            ActionButton(L("取消下载"), () => UpdateRequestAsync("updates.cancel"), operationKey: "updates-cancel", busyText: L("正在取消…")),
            ActionButton(L("退出并打开安装器"), InstallUpdateAsync, operationKey: "updates-install")));
        updates.Children.Add(ActionRow(ActionButton(L("打开下载目录"), OpenUpdateDirectoryAsync, operationKey: "updates-folder"),
            new HyperlinkButton { Content = L("查看版本说明"), NavigateUri = new Uri("https://github.com/konbakuyomu/smartsearch/releases") }));
        updates.Children.Add(Secondary(L("安装包会校验 SHA256，尚未验证系统代码签名。安装器启动后按提示完成安装，重新打开 App 核对版本。")));
        panel.Children.Add(SettingsSection(L("App 更新"), L("更新 App 和内置引擎。"), Card(updates)));

        var cli = new StackPanel { Spacing = 8 };
        cli.Children.Add(_cliUpdateSummary);
        cli.Children.Add(ActionRow(ActionButton(L("更新 CLI"), UpdateCliAsync, primary: true, operationKey: "updates-cli", busyText: L("更新中…")),
            ActionButton(L("复制更新命令"), () => { CopyText(Text(Property(_updates, "cli"), "command")); return Task.CompletedTask; }, operationKey: "updates-copy"),
            DetailsButton(L("更新日志与命令"), () => ShowDetailsAsync(L("更新日志与命令"), DataText(_cliUpdateLog.Text)))));
        var runtime = BuildRuntimePanel();
        if (_settingsAnchor == "runtime") runtime.Loaded += (_, _) => { runtime.StartBringIntoView(); _settingsAnchor = null; };
        panel.Children.Add(SettingsSection(L("独立 CLI"), L("管理独立安装的命令行工具。"), Card(cli), runtime));

        _cliSummary = DataText(L("正在读取本机 CLI 状态…"));
        var bundled = new StackPanel { Spacing = 8 };
        bundled.Children.Add(SectionHeading(L("内置入口")));
        bundled.Children.Add(Secondary(L("内置入口随 App 卸载失效。上方的独立 CLI 接入不使用此入口。")));
        bundled.Children.Add(_cliSummary);
        bundled.Children.Add(ActionRow(ActionButton(L("复制内置 CLI 调用"), CopyBundledCli),
            ActionButton(L("启用内置命令"), EnableBundledCliAsync, operationKey: "cli-enable", busyText: L("启用中…"))));
        var diagnostics = new StackPanel { Spacing = 8 };
        diagnostics.Children.Add(SectionHeading(L("引擎与诊断")));
        diagnostics.Children.Add(ActionRow(DetailsButton(L("查看诊断信息"), () => ShowDetailsAsync(L("本地引擎"), Section(L("本地引擎"),
            [KeyValue(L("协议"), Text(_state, "protocol_version", "1")), KeyValue(L("路径"), _backend.BackendPath ?? L("未启动"))]))),
            ActionButton(L("重置服务商健康记录"), ResetProvidersAsync, busyText: L("重置中…"))));
        panel.Children.Add(SettingsSection(L("高级"), string.Empty, Card(bundled), Card(diagnostics)));
        RenderUpdateState();
        RenderEnvironmentState();
        _ = RunOperationAsync("cli-status", LoadCliStatusAsync);
        return Scroll(panel);
    }

    private ComboBox BuildLanguagePicker()
    {
        var language = new ComboBox { MinWidth = 144,
            IsEnabled = !_operations.IsBusy("language") && !EnvironmentBusy && Text(Property(_updates, "cli_update"), "status") != "running" };
        foreach (var label in new[] { L("跟随系统"), L("简体中文"), "English" }) language.Items.Add(label);
        language.SelectedIndex = Localization.Preference switch { "zh" => 1, "en" => 2, _ => 0 };
        language.SelectionChanged += async (_, _) =>
        {
            if (!_operations.Begin("language")) return;
            language.IsEnabled = false;
            try
            {
                var preference = language.SelectedIndex switch { 1 => "zh", 2 => "en", _ => "auto" };
                var previous = Localization.Preference;
                Localization.Preference = preference;
                if (_backend.IsConnected)
                {
                    var state = await RequestAsync("language.set", new { lang = Localization.Language }, L("无法切换界面语言。"));
                    if (state is null) { Localization.Preference = previous; return; }
                    ApplyState(state.Value);
                }
                ClearNotice();
                if (!SaveSetting("language", preference))
                    ShowNotice(L("语言"), L("本次语言切换已生效，但无法保存；重新打开 App 后可能恢复原选择。"), InfoBarSeverity.Warning);
                ApplyNavigationLanguage();
            }
            finally
            {
                _operations.EndRequest("language");
                RenderCurrentPage();
            }
        };
        return language;
    }

    private ContentControl BuildRuntimePanel()
    {
        _environmentSummary = Body(string.Empty);
        _environmentPlan = Body(string.Empty);
        _environmentDetails = DataText(string.Empty);
        _environmentSteps = new StackPanel { Spacing = 12 };
        _environmentProgress = new ProgressBar { Minimum = 0, Maximum = 100, Visibility = Visibility.Collapsed };
        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(SectionHeading(L("运行环境")));
        content.Children.Add(_environmentSummary);
        content.Children.Add(_environmentProgress);
        content.Children.Add(_environmentPlan);
        content.Children.Add(ActionRow(
            ActionButton(L("检测环境"), () => EnvironmentRequestAsync("environment.check"), operationKey: "environment-check", busyText: L("检测中…")),
            ActionButton(L("安装缺少的组件"), PrepareEnvironmentAsync, primary: true, operationKey: "environment-install", busyText: L("准备中…"), label: EnvironmentActionLabel),
            ActionButton(L("验证可用性"), () => EnvironmentRequestAsync("environment.verify"), operationKey: "environment-verify", busyText: L("验证中…")),
            ActionButton(L("取消下载"), () => EnvironmentRequestAsync("environment.cancel"), operationKey: "environment-cancel")));
        content.Children.Add(ActionRow(DetailsButton(L("安装位置与检查详情"), () =>
        {
            var details = new StackPanel { Spacing = 12 };
            foreach (var step in Items(Property(_environment, "steps")))
                details.Children.Add(Section(Text(step, "name"), [Body(Text(step, "status_label")), Secondary(Text(step, "message"))]));
            details.Children.Add(DataText(_environmentDetails.Text));
            return ShowDetailsAsync(L("安装位置与检查详情"), details);
        }), ActionButton(L("复制 AI 测试指引"), CopyEnvironmentTestAsync, operationKey: "environment-copy")));
        return Card(content);
    }

    private Task OpenRuntimeSettingsAsync()
    {
        _settingsAnchor = "runtime";
        return NavigateToAsync("settings");
    }
}
