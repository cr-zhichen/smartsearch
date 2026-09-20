using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using WinRT.Interop;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace SmartSearch.Desktop;

public sealed partial class MainWindow : Window
{
    private const int SwHide = 0;
    private const int SwRestore = 9;

    private readonly BackendClient _backend;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _activityTimer;
    private readonly NativeTray _tray;
    private readonly Dictionary<string, FieldEditor> _fieldEditors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Control> _commandControls = new(StringComparer.Ordinal);
    private readonly List<CommandArgument> _commandArguments = [];
    private readonly HashSet<string> _ownedRuns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _ownedRunStatus = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _ownedRunKinds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonElement> _ownedRunResults = new(StringComparer.Ordinal);
    private readonly List<string> _extraActivityDirectories = [];
    private readonly Dictionary<string, string> _preferences = new(StringComparer.Ordinal);
    private readonly OperationState _operations = new();
    private readonly Dictionary<Button, ActionBinding> _actionButtons = [];
    private readonly Dictionary<string, StackPanel> _providerStatusPanels = [];
    private readonly Dictionary<string, bool> _disclosures = [];
    private readonly Dictionary<string, double> _pageOffsets = [];
    private readonly HashSet<string> _handlingRunResults = [];
    private readonly Dictionary<string, Dictionary<string, CommandValue>> _commandDrafts = [];
    private string? _renderedCommandId;
    private Dictionary<string, FieldDraft> _providerDraft = [];
    private ScrollViewer? _pageScroll;
    private string _renderedPage = "overview";
    private TextBlock? _saveSummary;
    private bool _connecting;
    private JsonElement? _state;
    private JsonElement? _updates;
    private TextBlock? _appUpdateSummary, _cliUpdateSummary, _downloadSummary, _updateCheckSummary, _cliUpdateLog;
    private ProgressBar? _downloadProgress;
    private ToggleSwitch? _autoUpdateSwitch;
    private bool _settingAutoUpdate;
    private AppWindow? _appWindow;
    private nint _windowHandle;
    private string _currentPage = "overview";
    private bool _started;
    private bool _activityRefreshing;
    private bool _settingActivityEnabled;
    private bool _allowClose;
    private bool _shuttingDown;
    private StackPanel? _activityRows;
    private readonly Dictionary<string, ActivityRowView> _activityViews = [];
    private TextBlock? _activityHint;
    private ToggleSwitch? _activityEnabledSwitch;
    private StackPanel? _skillRows;
    private TextBlock? _cliSummary;
    private ComboBox? _commandPicker;
    private StackPanel? _commandFieldPanel;
    private TextBox? _resultText;
    private TextBox? _rawResult;
    private StackPanel? _sourceRows;
    private Expander? _sourceDisclosure;
    private string _lastResultExport = string.Empty;
    private string? _selectedCommandId;
    private string? _selectedResultRunId;

    public MainWindow()
    {
        InitializeComponent();
        var backendLaunch = ReadBackendLaunch();
        _backend = new BackendClient(backendLaunch.Path, backendLaunch.Arguments);
        _backend.EventReceived += OnBackendEvent;
        _backend.Disconnected += OnBackendDisconnected;
        _activityTimer = DispatcherQueue.CreateTimer();
        _activityTimer.Interval = TimeSpan.FromSeconds(2);
        _activityTimer.Tick += async (_, _) => await RefreshActivityAsync(silent: true);
        LoadLocalPreferences();
        InitializeAppWindow();
        _tray = new NativeTray(_windowHandle, ShowMainWindow);
        Activated += OnWindowActivated;
        ApplyTheme(ReadSetting("theme") ?? "auto");
    }

    private void InitializeAppWindow()
    {
        _windowHandle = WindowNative.GetWindowHandle(this);
        _appWindow = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_windowHandle));
        _appWindow.Closing += OnAppWindowClosing;
        _appWindow.Title = "Smart Search";
        _appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "smart-search.ico"));
    }

    private async void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (_started)
            return;
        _started = true;
        await RunOperationAsync("connect", ConnectAsync);
    }

    private async Task ConnectAsync()
    {
        if (_operations.IsBusy("updates-cli") || Text(Property(_updates, "cli_update"), "status") == "running") return;
        _connecting = true;
        RenderCurrentPage();
        try
        {
            var state = await _backend.StartAsync(configDirectory: null, CancellationToken.None);
            ApplyState(state);
            _activityTimer.Start();
            NoticeBar.IsOpen = false;
        }
        catch (Exception error)
        {
            ShowNotice("后端不可用", SafeMessage(error), InfoBarSeverity.Error);
        }
        _connecting = false;
        RenderCurrentPage();
    }

    private async Task RefreshStateAsync(bool preserveDraft = true)
    {
        var result = await RequestAsync("get_state", new { }, "无法刷新本机状态。");
        if (result is null)
            return;
        ApplyState(result.Value);
        if (!preserveDraft) _providerDraft.Clear();
        RenderCurrentPage();
    }

    private async Task<JsonElement?> RequestAsync(string method, object parameters, string failure)
    {
        try
        {
            return await _backend.CallAsync(method, parameters, CancellationToken.None);
        }
        catch (Exception error)
        {
            ShowNotice(failure, SafeMessage(error), InfoBarSeverity.Error);
            return null;
        }
    }

    private void ApplyState(JsonElement candidate)
    {
        var state = candidate.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.Object
            ? status
            : candidate;
        if (state.ValueKind != JsonValueKind.Object)
            return;
        _state = state.Clone();
        _updates = Property(state, "updates").Clone();
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            _currentPage = tag;
            RenderCurrentPage();
        }
    }

    private void RenderCurrentPage(Dictionary<string, FieldDraft>? preservedDraft = null)
    {
        CaptureCommandInputs();
        if (_pageScroll is not null)
            _pageOffsets[_renderedPage] = _pageScroll.VerticalOffset;
        _renderedPage = _currentPage;
        _actionButtons.Clear();
        _providerStatusPanels.Clear();
        if (preservedDraft is not null) _providerDraft = preservedDraft;
        if (_commandPicker?.SelectedItem is CommandOption selected)
            _selectedCommandId = selected.Id;
        ContentFrame.Content = _currentPage switch
        {
            "providers" => BuildProvidersPage(_providerDraft),
            "search" => BuildSearchPage(),
            "activity" => BuildActivityPage(),
            "ai" => BuildAiPage(),
            "settings" => BuildSettingsPage(),
            _ => BuildOverviewPage()
        };
        RefreshActionButtons();
    }

    private UIElement BuildOverviewPage()
    {
        var panel = PagePanel();
        panel.Children.Add(PageTitle("概览"));
        if (_state is not { } state)
        {
            panel.Children.Add(Card(Section(_connecting ? "正在读取本机配置…" : "连接本地引擎",
                [Body(_connecting ? "读取配置不会调用外部服务商。" : "本地引擎尚未连接，请重新连接或检查安装是否完整。"),
                 ActionButton("重新连接", ConnectAsync, primary: true, operationKey: "connect", busyText: "连接中…")])));
            return Scroll(panel);
        }

        var minimum = Property(state, "minimum_profile");
        var profileOk = Bool(minimum, "ok");
        panel.Children.Add(Secondary("配置、运行与结果，都在这台电脑上管理。"));
        var next = new StackPanel { Spacing = 12 };
        next.Children.Add(HeadingWithStatus(profileOk ? "可以开始搜索了" : "先完成基础配置",
            profileOk ? "配置齐全" : "待配置", profileOk ? "Success" : "Warning"));
        next.Children.Add(Body(profileOk ? "主搜索、文档检索和网页抓取已满足使用条件。" : $"还需要配置：{MissingText(minimum)}。每类任选一个服务商即可。"));
        next.Children.Add(ActionButton(profileOk ? "开始搜索" : "去配置", () => NavigateToAsync(profileOk ? "search" : "providers"), primary: true));
        next.Children.Add(Secondary("配置齐全表示可以发起请求；连接是否正常，以你主动测试的结果为准。"));
        panel.Children.Add(Card(next));
        var capabilityRows = CapabilityRows(state).ToList();
        if (capabilityRows.Count > 0)
            panel.Children.Add(Card(Section("当前能力", capabilityRows)));
        panel.Children.Add(ActionButton("刷新本机状态", () => RefreshStateAsync(), operationKey: "state"));
        panel.Children.Add(Disclosure("overview-details", "配置与路由详情", Section("本机配置",
            [KeyValue("配置目录", Text(state, "config_dir", Text(state, "config_path", "未返回"))),
             KeyValue("配置版本", Text(state, "revision", "未返回")), .. CapabilityChains(state)])));
        return Scroll(panel);
    }

    private UIElement BuildProvidersPage(Dictionary<string, FieldDraft>? preservedDraft)
    {
        _fieldEditors.Clear();
        var panel = PagePanel();
        panel.Children.Add(PageTitle("配置与服务商"));
        panel.Children.Add(Secondary("先配齐三类能力，其余按需展开。修改后可先测试，再保存。"));
        if (_state is not { } state)
        {
            panel.Children.Add(OfflineHint());
            return Scroll(panel);
        }

        var fields = Items(Property(Property(state, "metadata"), "fields")).ToList();
        if (fields.Count == 0)
        {
            panel.Children.Add(Body("后端没有返回可编辑字段。请刷新状态或检查协议版本。"));
            return Scroll(panel);
        }

        var providerGroups = fields.Where(field => !string.IsNullOrWhiteSpace(Text(field, "provider")))
            .GroupBy(field => Text(field, "provider")).ToList();
        var shown = new HashSet<string>();
        var step = 0;
        foreach (var capability in new[] { "main_search", "docs_search", "web_fetch" })
        {
            var groups = providerGroups.Where(group => !shown.Contains(group.Key) && group.Any(field =>
                Text(field, "tier") == "essential" && Items(field, "capabilities").Any(value => value.GetString() == capability))).ToList();
            var content = new StackPanel { Spacing = 20 };
            var ready = Bool(Property(Property(state, "capability_status"), capability), "ok");
            content.Children.Add(HeadingWithStatus($"{++step}. {CapabilityLabel(capability)}", ready ? "已配置" : "待配置", ready ? "Success" : "Warning"));
            content.Children.Add(Secondary("下面的服务商任选一个即可。"));
            bool Configured(IGrouping<string, JsonElement> group) => group.Any(field => IsSecret(field) && !string.IsNullOrWhiteSpace(DisplayValue(Property(state, "values"), Text(field, "key"))));
            foreach (var group in groups.OrderByDescending(Configured))
            {
                shown.Add(group.Key);
                var providerForm = BuildProviderGroup(state, group.Key, group.ToList(), preservedDraft);
                content.Children.Add(Configured(group) || !groups.Any(Configured)
                    ? providerForm
                    : Disclosure("alternative:" + group.Key, ProviderLabel(group.Key) + " · 可选", providerForm));
            }
            panel.Children.Add(Card(content));
        }
        panel.Children.Add(SectionHeading("更多服务商"));
        panel.Children.Add(Secondary("按需启用网页搜索和实验性检索。测试会发送真实请求，可能计费。"));
        foreach (var group in providerGroups.Where(group => !shown.Contains(group.Key)))
            panel.Children.Add(Disclosure("provider:" + group.Key, ProviderLabel(group.Key),
                BuildProviderGroup(state, group.Key, group.ToList(), preservedDraft)));

        var metadata = Property(state, "metadata");
        foreach (var section in Items(metadata, "sections").OrderBy(section => Number(section, "order")))
        {
            var id = Text(section, "id");
            var sectionFields = fields.Where(field => string.IsNullOrWhiteSpace(Text(field, "provider")) && Text(field, "section") == id).ToList();
            if (sectionFields.Count == 0) continue;
            var content = new StackPanel { Spacing = 16 };
            content.Children.Add(Secondary(Text(section, "blurb_zh")));
            foreach (var field in sectionFields) content.Children.Add(BuildFieldEditor(field, preservedDraft));
            panel.Children.Add(Disclosure("section:" + id, Text(section, "label_zh", id), content));
        }
        _saveSummary = Secondary("");
        var footer = new StackPanel { Spacing = 8 };
        footer.Children.Add(_saveSummary);
        footer.Children.Add(ActionRow(
            ActionButton("保存更改", SaveDraftAsync, primary: true, operationKey: "config-save", busyText: "保存中…"),
            ActionButton("预览", PreviewDraftAsync, operationKey: "config-preview", busyText: "预览中…"),
            ActionButton("刷新", () => RefreshStateAsync(), operationKey: "state", busyText: "刷新中…")));
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.Children.Add(Scroll(panel));
        var footerCard = Card(footer);
        footerCard.Margin = new Thickness(24, 8, 24, 12);
        Grid.SetRow(footerCard, 1);
        layout.Children.Add(footerCard);
        return layout;
    }

    private UIElement BuildProviderGroup(JsonElement state, string provider, List<JsonElement> fields, Dictionary<string, FieldDraft>? draft)
    {
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock { Text = ProviderLabel(provider), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 16 });
        foreach (var field in fields.Where(field => !IsAdvanced(field))) content.Children.Add(BuildFieldEditor(field, draft));
        var advanced = fields.Where(IsAdvanced).ToList();
        if (advanced.Count > 0)
        {
            var options = new StackPanel { Spacing = 16 };
            foreach (var field in advanced) options.Children.Add(BuildFieldEditor(field, draft));
            content.Children.Add(Disclosure("advanced:" + provider, $"更多设置（{advanced.Count}）", options));
        }
        var status = BuildProviderStatus(state, provider);
        _providerStatusPanels[provider] = status;
        content.Children.Add(status);
        content.Children.Add(ActionButton("测试", () => TestProviderDraftAsync(provider), operationKey: "test:" + provider,
            busyText: Text(Property(state, "probe_kinds"), provider) == "presence" ? "检查中…" : "测试中…", label: () => ProviderTestLabel(provider)));
        return content;
    }

    private UIElement BuildFieldEditor(JsonElement field, Dictionary<string, FieldDraft>? preservedDraft)
    {
        var key = Text(field, "key");
        var source = Text(Property(_state!.Value, "sources"), key, "default");
        var value = DisplayValue(Property(_state!.Value, "values"), key);
        var initialValue = string.IsNullOrWhiteSpace(value) ? Text(field, "default") : value;
        var isSecret = IsSecret(field);
        var isLocked = source.Equals("environment", StringComparison.OrdinalIgnoreCase);
        var label = new StackPanel { Spacing = 6 };
        label.Children.Add(new TextBlock { Text = Label(field), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        var help = Text(field, "help_zh", Text(field, "help_en"));
        if (!string.IsNullOrWhiteSpace(help))
            label.Children.Add(Secondary(help));
        var links = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var (property, text) in new[] { ("key_url", "申请 Key"), ("docs_url", "文档") })
            if (Uri.TryCreate(Text(field, property), UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
                links.Children.Add(new HyperlinkButton { Content = text, NavigateUri = uri, Padding = new Thickness(0), FontSize = 12 });
        if (links.Children.Count > 0) label.Children.Add(links);
        var box = new StackPanel { Spacing = 8 };
        var saved = DisplayValue(Property(_state!.Value, "saved_values"), key);

        var input = CreateFieldInput(field, isSecret, initialValue, isLocked);
        AutomationProperties.SetName(input, Label(field));
        input.HorizontalAlignment = HorizontalAlignment.Stretch;
        box.Children.Add(input);
        var detail = new StackPanel { Spacing = 6 };
        detail.Children.Add(DataText(key));
        detail.Children.Add(DataText($"当前生效：{(string.IsNullOrWhiteSpace(initialValue) ? "未设置" : initialValue)}"));
        if (saved != initialValue) detail.Children.Add(DataText($"配置文件：{(string.IsNullOrWhiteSpace(saved) ? "未设置" : saved)}"));
        box.Children.Add(Badge(SourceLabel(source), "Neutral"));
        if (isSecret && !string.IsNullOrWhiteSpace(value)) box.Children.Add(DataText("当前密钥：" + value));
        CheckBox? clear = null;
        if (!isLocked)
        {
            clear = new CheckBox
            {
                Content = isSecret ? "清除已保存的密钥" : "恢复默认值",
                IsEnabled = true
            };
            detail.Children.Add(clear);
        }
        else
        {
            box.Children.Add(Secondary("由环境变量提供，在此处只读。"));
        }
        box.Children.Add(Disclosure("field:" + key, "来源与重置", detail));

        var editor = new FieldEditor(field.Clone(), input, clear, ReadControl(input), isSecret, isLocked);
        _fieldEditors[key] = editor;
        if (preservedDraft is not null && preservedDraft.TryGetValue(key, out var draft))
            RestoreDraft(editor, draft);
        void Changed() { _providerDraft = CaptureDraft(); RefreshActionButtons(); }
        switch (input)
        {
            case TextBox textBox: textBox.TextChanged += (_, _) => Changed(); break;
            case PasswordBox passwordBox: passwordBox.PasswordChanged += (_, _) => Changed(); break;
            case ComboBox comboBox: comboBox.SelectionChanged += (_, _) => Changed(); break;
            case ToggleSwitch toggle: toggle.Toggled += (_, _) => Changed(); break;
        }
        if (clear is not null) { clear.Checked += (_, _) => Changed(); clear.Unchecked += (_, _) => Changed(); }
        return FieldRow(label, box);
    }

    private static IEnumerable<UIElement> CapabilityRows(JsonElement state)
    {
        var capabilities = Property(state, "capability_status");
        if (capabilities.ValueKind != JsonValueKind.Object)
            yield break;
        foreach (var capability in capabilities.EnumerateObject().OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            var configured = Items(capability.Value, "configured")
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
            var available = Bool(capability.Value, "ok");
            var status = available ? "已配置" : configured.Length > 0 ? "需要调整" : "未配置";
            var experimental = Bool(capability.Value, "experimental") ? "（实验性）" : string.Empty;
            var row = new StackPanel { Spacing = 6, Margin = new Thickness(0, 4, 0, 8) };
            row.Children.Add(HeadingWithStatus(CapabilityLabel(capability.Name) + experimental, status, available ? "Success" : "Neutral"));
            row.Children.Add(Secondary(configured.Length == 0 ? "选择一个服务商开始配置。" : string.Join(" · ", configured.Select(value => ProviderLabel(value!)))));
            yield return row;
        }
    }

    private StackPanel BuildProviderStatus(JsonElement state, string provider)
    {
        var panel = new StackPanel { Spacing = 6 };
        var busy = _operations.IsBusy("test:" + provider);
        var presence = Text(Property(state, "probe_kinds"), provider) == "presence";
        var check = Property(Property(state, "provider_checks"), provider);
        var status = Text(check, "status");
        panel.Children.Add(Badge(busy ? presence ? "检查中" : "测试中" : check.ValueKind == JsonValueKind.Object ? ProviderCheckLabel(status) : presence ? "尚未检查" : "尚未测试",
            busy ? "Active" : StatusTone(status)));
        if (busy)
            panel.Children.Add(Secondary(presence ? "正在检查配置是否填写。" : "正在等待服务商响应，请稍候。可在活动页取消。"));
        else if (check.ValueKind == JsonValueKind.Object)
        {
            var scope = Text(check, "scope");
            panel.Children.Add(Secondary($"{(scope == "draft" ? "未保存修改的测试" : "测试时的有效配置")} · {TimestampOrText(check, "checked_at")}"));
            if (scope == "draft") panel.Children.Add(Secondary("此结果对应测试时尚未保存的修改；再次测试可确认当前配置。"));
            if (!string.IsNullOrWhiteSpace(Text(check, "probe")))
                panel.Children.Add(Secondary("检查方式：" + ProviderCheckLabel(Text(check, "probe"))));
            var message = Text(check, "message");
            if (!string.IsNullOrWhiteSpace(message)) panel.Children.Add(Disclosure("test-detail:" + provider, "技术详情", DataText(message)));
        }
        var health = Property(state, "provider_health");
        var healthRow = Items(health, "providers").FirstOrDefault(item => Text(item, "provider").Equals(provider, StringComparison.OrdinalIgnoreCase));
        var cooling = Text(healthRow, "state") == "cooldown" ||
            Items(health, "cooldown_providers").Any(item => item.ValueKind == JsonValueKind.String && item.GetString() == provider);
        if (cooling) panel.Children.Add(Badge($"调用保护：冷却中 · {CooldownText(healthRow)}", "Warning"));
        return panel;
    }

    private UIElement BuildSearchPage()
    {
        _commandControls.Clear();
        _commandArguments.Clear();
        var panel = PagePanel();
        panel.Children.Add(PageTitle("搜索与研究"));
        panel.Children.Add(Secondary("选择工具、填写问题，完成后在这里查看结果与来源。"));
        if (_state is not { } state)
        {
            panel.Children.Add(OfflineHint());
            return Scroll(panel);
        }

        _commandPicker = new ComboBox { Header = "工具", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var command in Items(state, "commands"))
        {
            var id = Text(command, "id");
            if (!string.IsNullOrWhiteSpace(id))
                _commandPicker.Items.Add(new CommandOption(id, Text(command, "label", id), Text(command, "description"), Bool(command, "experimental"), command.Clone()));
        }
        _commandPicker.SelectionChanged += (_, _) => RenderCommandFields();
        var form = new StackPanel { Spacing = 16 };
        form.Children.Add(_commandPicker);
        _commandFieldPanel = new StackPanel { Spacing = 10 };
        form.Children.Add(_commandFieldPanel);
        form.Children.Add(ActionButton("运行", StartSelectedCommandAsync, primary: true, busyText: "运行中…",
            dynamicKey: () => "run:" + _selectedCommandId));
        panel.Children.Add(Card(form));

        panel.Children.Add(new TextBlock { Text = "结果", FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 0) });
        _resultText = new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            MinHeight = 160,
            PlaceholderText = "运行后会在这里显示可读结果和下一步。"
        };
        _sourceRows = new StackPanel { Spacing = 4 };
        _sourceDisclosure = Disclosure("result-sources", "来源链接", _sourceRows);
        _sourceDisclosure.Visibility = Visibility.Collapsed;
        var resultActions = ActionRow(ActionButton("复制结果", CopyResult), ActionButton("导出结果", ExportResultAsync, busyText: "导出中…"));
        _rawResult = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MinHeight = 120 };
        panel.Children.Add(Card(new StackPanel { Spacing = 12, Children =
        {
            _resultText, _sourceDisclosure, resultActions, Disclosure("result-json", "高级 JSON", _rawResult)
        } }));
        if (_commandPicker.Items.Count > 0)
        {
            _commandPicker.SelectedItem = _commandPicker.Items.OfType<CommandOption>()
                .FirstOrDefault(command => command.Id == _selectedCommandId) ?? _commandPicker.Items[0];
        }
        if (_selectedResultRunId is not null && _ownedRunResults.TryGetValue(_selectedResultRunId, out var cachedResult))
            RenderResult(cachedResult);
        return Scroll(panel);
    }

    private UIElement BuildActivityPage()
    {
        var panel = PagePanel();
        panel.Children.Add(PageTitle("实时活动"));
        panel.Children.Add(Secondary("查看 App、终端与 AI 的运行记录。App 发起的任务可在这里取消。"));
        _activityEnabledSwitch = new ToggleSwitch { Header = "记录活动", IsOn = Bool(Property(_state, "activity"), "enabled", true) };
        _activityEnabledSwitch.Toggled += async (_, _) =>
        {
            if (!_settingActivityEnabled)
                await RunOperationAsync("activity-setting", () => SetActivityEnabledAsync(_activityEnabledSwitch.IsOn));
        };
        panel.Children.Add(_activityEnabledSwitch);
        var actions = ActionRow(ActionButton("立即刷新", () => RefreshActivityAsync(silent: false), busyText: "刷新中…"),
            ActionButton("清除已结束记录", ClearActivityAsync, busyText: "清除中…"));
        panel.Children.Add(actions);
        _activityViews.Clear();
        _activityHint = Body("");
        _activityHint.Visibility = Visibility.Collapsed;
        panel.Children.Add(_activityHint);
        _activityRows = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(_activityRows);
        _ = RefreshActivityAsync(silent: true);
        return Scroll(panel);
    }

    private UIElement BuildAiPage()
    {
        var panel = PagePanel();
        panel.Children.Add(PageTitle("AI 接入"));
        panel.Children.Add(Secondary("App 与终端命令独立运行、独立更新。选择需要接入的 AI 工具。"));
        _cliSummary = Body("正在读取本机 CLI 状态…");
        _cliSummary.Style = UiStyle("DataCopyStyle");
        var cliActions = ActionRow(ActionButton("复制内置 CLI 调用", CopyBundledCli),
            ActionButton("启用内置命令", EnableBundledCliAsync, primary: true, busyText: "启用中…"));
        panel.Children.Add(Card(Section("终端命令", [_cliSummary, cliActions])));
        panel.Children.Add(new TextBlock { Text = "Skills", FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 0) });
        _skillRows = new StackPanel { Spacing = 8 };
        var skillActions = ActionRow(ActionButton("检查状态", LoadSkillsAsync, operationKey: "skills-status", busyText: "检查中…"),
            ActionButton("安装/更新选择项", InstallSelectedSkillsAsync, primary: true, operationKey: "skills-install", busyText: "安装中…"));
        panel.Children.Add(Card(Section("选择要接入的工具", [_skillRows, skillActions])));
        _ = RunOperationAsync("cli-status", LoadCliStatusAsync);
        _ = RunOperationAsync("skills-status", LoadSkillsAsync);
        return Scroll(panel);
    }

    private UIElement BuildSettingsPage()
    {
        var panel = PagePanel();
        panel.Children.Add(PageTitle("设置与关于"));
        panel.Children.Add(Secondary("管理本机配置目录、显示方式和更新。"));
        var theme = new ComboBox { Header = "外观", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var label in new[] { "跟随系统", "浅色", "深色" }) theme.Items.Add(label);
        theme.SelectedIndex = (ReadSetting("theme") ?? "auto") switch { "light" => 1, "dark" => 2, _ => 0 };
        theme.SelectionChanged += (_, _) =>
        {
            var value = theme.SelectedIndex switch { 1 => "light", 2 => "dark", _ => "auto" };
            SaveSetting("theme", value);
            ApplyTheme(value);
        };
        panel.Children.Add(Card(Section("本机偏好",
            [KeyValue("配置目录", Text(_state, "config_dir", Text(_state, "config_path", "未连接"))),
             ActionButton("选择配置目录", SelectConfigDirectoryAsync, operationKey: "profile", busyText: "切换中…"), theme])));
        var directoryRows = new StackPanel { Spacing = 8 };
        RenderExtraDirectories(directoryRows);
        panel.Children.Add(Card(Section("活动观察范围",
            [Secondary("默认观察当前配置目录。可以添加其他目录，不会自动扫描你的文件。"), directoryRows,
             ActionButton("添加活动目录", async () =>
             {
                 var folder = await PickFolderAsync();
                 if (folder is not null && !_extraActivityDirectories.Contains(folder.Path, StringComparer.OrdinalIgnoreCase))
                 {
                     _extraActivityDirectories.Add(folder.Path);
                     SaveExtraDirectories();
                     RenderCurrentPage();
                 }
             })])));
        _autoUpdateSwitch = new ToggleSwitch { Header = "自动检查更新", OnContent = "每 24 小时检查，点击才下载", OffContent = "已关闭" };
        _autoUpdateSwitch.IsOn = Bool(_updates, "auto_check", true);
        _autoUpdateSwitch.Toggled += async (_, _) =>
        {
            if (_settingAutoUpdate) return;
            await UpdateRequestAsync("updates.auto", new { enabled = _autoUpdateSwitch.IsOn });
        };
        _updateCheckSummary = Secondary("");
        _appUpdateSummary = Body("");
        _cliUpdateSummary = Body("");
        _downloadSummary = Secondary("");
        _cliUpdateLog = DataText("");
        _downloadProgress = new ProgressBar { Minimum = 0, Maximum = 100 };
        panel.Children.Add(Card(Section("版本与更新",
            [_autoUpdateSwitch, _updateCheckSummary,
             ActionRow(ActionButton("检查更新", CheckForUpdateAsync, operationKey: "updates-check", busyText: "检查中…"),
                       ActionButton("刷新已安装版本", () => RefreshStateAsync(), operationKey: "state", busyText: "刷新中…")),
             Secondary("App 和内置引擎一起更新；独立 CLI 使用原管理器单独更新。")])));
        panel.Children.Add(Card(Section("App 与内置引擎", [_appUpdateSummary, _downloadSummary, _downloadProgress,
             ActionRow(ActionButton("下载安装包", () => UpdateRequestAsync("updates.download"), primary: true, operationKey: "updates-download", busyText: "下载中…"),
                       ActionButton("取消下载", () => UpdateRequestAsync("updates.cancel"), operationKey: "updates-cancel", busyText: "正在取消…"),
                       ActionButton("退出并打开安装器", InstallUpdateAsync, operationKey: "updates-install")),
             ActionRow(ActionButton("打开下载目录", OpenUpdateDirectoryAsync, operationKey: "updates-folder"),
                       ActionButton("查看版本说明", async () => { await Launcher.LaunchUriAsync(new Uri("https://github.com/konbakuyomu/smartsearch/releases")); })),
             Secondary("安装包会校验 SHA256，尚未验证系统代码签名。安装器启动后按提示完成安装，重新打开 App 核对版本。")])));
        panel.Children.Add(Card(Section("独立 CLI", [_cliUpdateSummary,
             ActionRow(ActionButton("更新 CLI", UpdateCliAsync, primary: true, operationKey: "updates-cli", busyText: "更新中…"),
                       ActionButton("复制更新命令", () => { CopyText(Text(Property(_updates, "cli"), "command")); return Task.CompletedTask; }, operationKey: "updates-copy")),
             Disclosure("update-cli-log", "更新日志与命令", _cliUpdateLog)])));
        panel.Children.Add(Card(Section("引擎与诊断",
            [Disclosure("diagnostics", "查看诊断信息", Section("本地引擎",
                 [KeyValue("协议", Text(_state, "protocol_version", "1")), KeyValue("路径", _backend.BackendPath ?? "未启动"),
                  ActionButton("重置服务商健康记录", ResetProvidersAsync, busyText: "重置中…")]))])));
        RenderUpdateState();
        return Scroll(panel);
    }

    private void RenderCommandFields()
    {
        if (_commandFieldPanel is null || _commandPicker?.SelectedItem is not CommandOption command)
            return;
        CaptureCommandInputs();
        _selectedCommandId = _renderedCommandId = command.Id;
        _commandControls.Clear();
        _commandArguments.Clear();
        _commandFieldPanel.Children.Clear();
        _commandFieldPanel.Children.Add(Body(command.Experimental
            ? $"{command.Description}（实验性：只会在你点击运行后执行。）"
            : command.Description));
        var fields = Items(command.Definition, "fields").ToList();
        foreach (var field in fields.Where(field => !IsCommandAdvanced(field)))
            AddCommandField(field, _commandFieldPanel);
        var advanced = fields.Where(IsCommandAdvanced).ToList();
        if (advanced.Count > 0)
        {
            var advancedPanel = new StackPanel { Spacing = 10 };
            foreach (var field in advanced)
                AddCommandField(field, advancedPanel);
            _commandFieldPanel.Children.Add(new Expander { Header = "高级参数", Content = advancedPanel });
        }
        RefreshActionButtons();
    }

    private void AddCommandField(JsonElement field, Panel target)
    {
        var name = Text(field, "name");
        if (string.IsNullOrWhiteSpace(name))
            return;
        var kind = Text(field, "kind");
        var flags = Items(field, "flags").Select(value => value.ValueKind == JsonValueKind.String ? value.GetString()! : string.Empty).Where(flag => !string.IsNullOrWhiteSpace(flag)).ToList();
        _commandArguments.Add(new CommandArgument(name, flags, kind.Equals("bool", StringComparison.OrdinalIgnoreCase), Bool(field, "multiple"), Bool(field, "required")));
        var group = new StackPanel { Spacing = 4 };
        group.Children.Add(new TextBlock { Text = Text(field, "label", name), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        var help = Text(field, "help");
        if (!string.IsNullOrWhiteSpace(help))
            group.Children.Add(Secondary(help));
        var input = CreateCommandInput(field);
        AutomationProperties.SetName(input, Text(field, "label", name));
        if (_selectedCommandId is not null && _commandDrafts.TryGetValue(_selectedCommandId, out var draft) && draft.TryGetValue(name, out var saved))
            RestoreControl(input, saved);
        _commandControls[name] = input;
        group.Children.Add(input);
        target.Children.Add(group);
    }

    private async Task StartSelectedCommandAsync()
    {
        if (_commandPicker?.SelectedItem is not CommandOption command)
        {
            ShowNotice("请选择工具", "后端尚未提供命令目录。", InfoBarSeverity.Warning);
            return;
        }
        foreach (var argument in _commandArguments.Where(argument => argument.Required && !argument.IsBoolean))
        {
            if (string.IsNullOrWhiteSpace(ReadControl(argument.Name).Text))
            {
                ShowNotice("缺少必要参数", $"请填写“{argument.Name}”。", InfoBarSeverity.Warning);
                return;
            }
        }
        var arguments = ProtocolArguments.Build(_commandArguments, ReadControl).ToArray();
        var result = await RequestAsync("run.start", new { command = command.Id, arguments }, "无法启动该任务。");
        if (result is null)
            return;
        var runId = Text(result.Value, "run_id");
        RegisterOwnedRun(runId, command.Id, "run:" + command.Id);
        _selectedResultRunId = runId;
        _lastResultExport = string.Empty;
        if (_resultText is not null)
            _resultText.Text = "任务正在运行。完成后会显示可读结果和来源。";
        if (_rawResult is not null)
            _rawResult.Text = string.Empty;
        _sourceRows?.Children.Clear();
        if (_sourceDisclosure is not null) _sourceDisclosure.Visibility = Visibility.Collapsed;
        ShowNotice("正在运行", "结果完成后会显示在本页，也可到活动页查看或取消。", InfoBarSeverity.Informational);
    }

    private async Task PreviewDraftAsync()
    {
        var draft = CollectDraft();
        var result = await RequestAsync("config.preview", new { set = draft.Set, unset = draft.Unset }, "无法预览修改。");
        if (result is null)
            return;
        var ready = Bool(result.Value, "minimum_profile_ok");
        ShowNotice("修改预览", ready ? "修改后满足基础配置条件。尚未保存。" : $"修改后仍缺少：{MissingText(result.Value)}", ready ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
    }

    private async Task SaveDraftAsync()
    {
        if (_state is not { } state)
            return;
        var draft = CollectDraft();
        if (draft.Set.Count == 0 && draft.Unset.Count == 0)
        {
            ShowNotice("没有更改", "尚未输入新值或选择清除。", InfoBarSeverity.Informational);
            return;
        }
        var result = await RequestAsync("config.apply", new { set = draft.Set, unset = draft.Unset, revision = Text(state, "revision") }, "无法保存配置。");
        if (result is null)
            return;
        if (!Bool(result.Value, "ok"))
        {
            var errorType = Text(result.Value, "error_type", "unknown_error");
            if (errorType.Contains("conflict", StringComparison.OrdinalIgnoreCase))
                ShowNotice("配置已变更", "其他进程更新了配置。你的修改已保留，请刷新后重新确认。", InfoBarSeverity.Warning);
            else
                ShowNotice("未保存", $"{Text(result.Value, "error", $"后端拒绝了本次配置（{errorType}）。")} 原配置未被 App 覆盖。", InfoBarSeverity.Error);
            return;
        }
        _providerDraft.Clear();
        await RefreshStateAsync(preserveDraft: false);
        ShowNotice("已保存", "新配置会用于下一次任务；已经开始的任务继续使用它自己的配置快照。", InfoBarSeverity.Success);
    }

    private async Task TestProviderDraftAsync(string provider)
    {
        var draft = CollectDraft(provider);
        var overrides = draft.Set.ToDictionary(item => item.Key, item => Convert.ToString(item.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
        foreach (var key in draft.Unset)
            overrides[key] = string.Empty;
        var result = await RequestAsync("provider.test", new { provider, overrides }, "无法启动测试。");
        if (result is null)
            return;
        RegisterOwnedRun(Text(result.Value, "run_id"), "provider.test", "test:" + provider);
        if (overrides.Count > 0)
            ShowNotice("正在测试未保存的修改", "这次测试不会保存配置。结果会显示在对应服务商下方。", InfoBarSeverity.Informational);
    }

    private async Task RefreshActivityAsync(bool silent)
    {
        if (_activityRefreshing || !_backend.IsConnected)
            return;
        _activityRefreshing = true;
        try
        {
            var result = await _backend.CallAsync("activity.list", new { directories = ActivityDirectories(), limit = 200 }, CancellationToken.None);
            foreach (var runId in _ownedRunStatus.Where(item => !IsTerminal(item.Value)).Select(item => item.Key).ToArray())
                await RecoverRunAsync(runId);
            RenderActivity(result);
        }
        catch (Exception error)
        {
            if (!silent)
                ShowNotice("活动记录不可用", SafeMessage(error), InfoBarSeverity.Error);
        }
        finally
        {
            _activityRefreshing = false;
        }
    }

    private void RenderActivity(JsonElement result)
    {
        if (_currentPage != "activity" || _activityRows is null) return;
        if (_activityEnabledSwitch is not null)
        {
            _settingActivityEnabled = true;
            _activityEnabledSwitch.IsOn = Bool(result, "enabled", true);
            _settingActivityEnabled = false;
        }
        // A heartbeat changes elapsed time, not row identity or chronological order.
        var runs = Items(result, "runs").OrderByDescending(run => Number(run, "started_at"))
            .ThenBy(run => Text(run, "run_id"), StringComparer.Ordinal).ToList();
        var ids = runs.Select(run => Text(run, "run_id")).ToHashSet();
        foreach (var id in _activityViews.Keys.Where(id => !ids.Contains(id)).ToArray())
        {
            _activityRows.Children.Remove(_activityViews[id].Row);
            _activityViews.Remove(id);
            foreach (var (button, binding) in _actionButtons.ToArray())
                if (new[] { "details:", "cancel:", "result:" }.Any(prefix => binding.Key() == prefix + id))
                    _actionButtons.Remove(button);
        }
        for (var index = 0; index < runs.Count; index++)
        {
            var run = runs[index];
            var id = Text(run, "run_id");
            if (!_activityViews.TryGetValue(id, out var view))
                _activityViews[id] = view = BuildActivityRow(run);
            view.Update(run);
            var position = _activityRows.Children.IndexOf(view.Row);
            if (position == index) continue;
            if (position >= 0) _activityRows.Children.RemoveAt(position);
            _activityRows.Children.Insert(index, view.Row);
        }
        var messages = new List<string>();
        if (Items(result, "errors").Any()) messages.Add("部分活动目录不可读取。请在设置中检查已添加的目录；这不表示没有活动。");
        if (runs.Count == 0) messages.Add("目前没有可见记录。旧 CLI、未启用观测或未添加的配置目录不会被伪造为“空闲”。");
        _activityHint!.Text = string.Join("\n", messages);
        _activityHint.Visibility = messages.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private ActivityRowView BuildActivityRow(JsonElement initial)
    {
        var runId = Text(initial, "run_id");
        var current = initial;
        var summary = new StackPanel { Spacing = 3 };
        var progress = Secondary("");
        var providerLine = Body("");
        var location = DataText("");
        var revision = DataText("");
        var error = Secondary("");
        var details = ActionButton("运行详情", () => ShowActivityDetailsAsync(current), operationKey: "details:" + runId);
        var cancel = ActionButton("取消任务", () => CancelOwnedRunAsync(runId), operationKey: "cancel:" + runId, busyText: "取消中…");
        var result = ActionButton("查看结果", () => ShowRunResultAsync(runId), operationKey: "result:" + runId);
        foreach (var child in new UIElement[] { progress, providerLine, location, revision, error, details, cancel, result })
            summary.Children.Add(child);
        var row = Disclosure("activity:" + runId, "", summary);
        row.Tag = runId;
        var header = "";
        return new ActivityRowView(row, run =>
        {
            current = run;
            var status = Text(run, "status", "unknown");
            var owned = _ownedRuns.Contains(runId);
            if (owned) _ownedRunStatus[runId] = status;
            var nextHeader = Text(run, "command") + ":" + status;
            if (header != nextHeader)
            {
                row.Header = HeadingWithStatus(CommandLabel(Text(run, "command")), StatusLabel(status), StatusTone(status));
                header = nextHeader;
            }
            progress.Text = $"{(Text(run, "origin") == "app" ? "桌面 App" : "终端 / AI")} · {PhaseLabel(Text(run, "phase"))} · 用时 {Elapsed(run)}";
            var provider = Text(run, "provider");
            var model = Text(run, "model");
            providerLine.Text = ActivityPresentation.ProviderModel(provider, model);
            providerLine.Visibility = string.IsNullOrWhiteSpace(provider) && string.IsNullOrWhiteSpace(model) ? Visibility.Collapsed : Visibility.Visible;
            location.Text = $"开始：{Timestamp(run, "started_at")}\n{Text(run, "config_dir", "未返回")}";
            revision.Text = $"配置版本：{Text(run, "config_revision")}";
            revision.Visibility = string.IsNullOrWhiteSpace(Text(run, "config_revision")) ? Visibility.Collapsed : Visibility.Visible;
            error.Text = $"错误：{ProviderCheckLabel(Text(run, "error_type"))}";
            error.Visibility = string.IsNullOrWhiteSpace(Text(run, "error_type")) ? Visibility.Collapsed : Visibility.Visible;
            cancel.Visibility = owned && !IsTerminal(status) ? Visibility.Visible : Visibility.Collapsed;
            result.Visibility = owned && IsTerminal(status) ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private async Task ShowActivityDetailsAsync(JsonElement run)
    {
        var runId = Text(run, "run_id");
        var configDirectory = Text(run, "config_dir", Text(_state, "config_dir"));
        if (string.IsNullOrWhiteSpace(runId) || string.IsNullOrWhiteSpace(configDirectory))
        {
            ShowNotice("无法读取详情", "该活动记录缺少运行标识或配置目录。", InfoBarSeverity.Warning);
            return;
        }
        var details = await RequestAsync("activity.details", new { run_id = runId, config_dir = configDirectory }, "无法读取活动详情。");
        if (details is null)
            return;
        if (!Bool(details.Value, "ok"))
        {
            ShowNotice("活动详情不可用", Text(details.Value, "error", "后端没有保存这条活动记录。"), InfoBarSeverity.Warning);
            return;
        }

        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(Body("这里只显示本地活动元数据和阶段事件，不包含查询、回答正文、请求头或密钥。"));
        var detailedRun = Property(details.Value, "run");
        content.Children.Add(Body($"状态：{StatusLabel(Text(detailedRun, "status", "unknown"))}  阶段：{PhaseLabel(Text(detailedRun, "phase"))}"));
        var detailProvider = Text(detailedRun, "provider");
        var detailModel = Text(detailedRun, "model");
        if (!string.IsNullOrWhiteSpace(detailProvider) || !string.IsNullOrWhiteSpace(detailModel))
            content.Children.Add(Body(ActivityPresentation.ProviderModel(detailProvider, detailModel)));
        if (!string.IsNullOrWhiteSpace(Text(detailedRun, "config_revision")))
            content.Children.Add(Body($"配置版本：{Text(detailedRun, "config_revision")}"));
        if (!string.IsNullOrWhiteSpace(Text(details.Value, "note")))
            content.Children.Add(Body(Text(details.Value, "note")));

        content.Children.Add(new TextBlock { Text = "阶段事件", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var events = Items(details.Value, "events").ToList();
        if (events.Count == 0)
            content.Children.Add(Body("没有可持久读取的事件；当前 App 自有任务仍可保留其内存状态。"));
        foreach (var activityEvent in events)
        {
            var eventProvider = Text(activityEvent, "provider");
            var eventModel = Text(activityEvent, "model");
            var suffix = string.IsNullOrWhiteSpace(eventProvider) && string.IsNullOrWhiteSpace(eventModel)
                ? string.Empty
                : $" · {eventProvider}{(string.IsNullOrWhiteSpace(eventModel) ? string.Empty : $" / {eventModel}")}";
            var error = Text(activityEvent, "error_type");
            content.Children.Add(Body($"{Timestamp(activityEvent, "timestamp")} · {PhaseLabel(Text(activityEvent, "phase"))} · {StatusLabel(Text(activityEvent, "status", "unknown"))}{suffix}{(string.IsNullOrWhiteSpace(error) ? string.Empty : $" · {ProviderCheckLabel(error)}")}"));
        }
        var raw = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MinHeight = 120, Text = JsonSerializer.Serialize(details.Value, new JsonSerializerOptions { WriteIndented = true }) };
        content.Children.Add(new Expander { Header = "高级 JSON（仅活动元数据）", Content = raw });
        var dialog = new ContentDialog
        {
            XamlRoot = DialogRoot,
            RequestedTheme = ((FrameworkElement)Content).ActualTheme,
            Title = $"运行详情：{CommandLabel(Text(run, "command"))}",
            Content = new ScrollViewer { Content = content, MaxHeight = 620, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            CloseButtonText = "关闭"
        };
        await dialog.ShowAsync();
    }

    private async Task SetActivityEnabledAsync(bool enabled)
    {
        var result = await RequestAsync("activity.enabled", new { enabled }, "无法更新活动记录设置。");
        if (result is not null)
            ShowNotice("活动记录设置已更新", enabled ? "新的可观测任务会记录活动元数据。" : "后端会停止新的活动记录；现有历史不受本操作删除。", InfoBarSeverity.Success);
    }

    private async Task ClearActivityAsync()
    {
        if (!await ConfirmAsync("清除已结束活动记录", "这只清除后端保存的已结束活动元数据，不会删除配置、研究证据或导出文件。", "清除"))
            return;
        var result = await RequestAsync("activity.clear", new { }, "无法清除活动记录。");
        if (result is not null)
        {
            ShowNotice("已清除", "已结束活动记录已清除；运行中的任务仍保留。", InfoBarSeverity.Success);
            await RefreshActivityAsync(silent: true);
        }
    }

    private async Task CancelOwnedRunAsync(string runId)
    {
        if (!_ownedRuns.Contains(runId))
            return;
        var result = await RequestAsync("run.cancel", new { run_id = runId }, "无法取消该任务。");
        if (result is not null)
        {
            _ownedRunStatus[runId] = Text(result.Value, "status", "cancelling");
            if (!IsTerminal(_ownedRunStatus[runId])) _operations.TrackRun("cancel:" + runId, runId);
            ShowNotice("正在取消", "取消请求已发送。最终状态会由后端运行事件确认。", InfoBarSeverity.Informational);
        }
    }

    private async Task LoadCliStatusAsync()
    {
        var result = await RequestAsync("cli.status", new { }, "无法读取 CLI 状态。");
        if (result is null || _cliSummary is null)
            return;
        var externalProtocol = Text(result.Value, "external_activity_protocol_version", "未返回");
        var observation = externalProtocol == "1" ? "已接入实时活动" : "尚未接入实时活动；升级后才能在活动页看到新的外部调用。";
        _cliSummary.Text = $"内置：{Text(result.Value, "bundled_path", "未返回")}（{Text(result.Value, "version", "版本未知")}）\n" +
                           $"外部：{Text(result.Value, "external_path", "未发现")}（{Text(result.Value, "external_version", "版本未知")}）\n" +
                           $"外部 CLI：{Text(result.Value, "external_status", "观测能力未知")}\n" +
                           $"活动观测：{observation}（协议：{externalProtocol}）";
    }

    private async Task LoadSkillsAsync()
    {
        var definitions = Items(Property(_state, "skill_targets"))
            .Where(item => !string.IsNullOrWhiteSpace(Text(item, "id")))
            .ToDictionary(item => Text(item, "id"), item => item, StringComparer.Ordinal);
        var parameters = new Dictionary<string, object?>();
        if (definitions.Count > 0)
            parameters["targets"] = definitions.Keys.ToArray();
        var result = await RequestAsync("skills.status", parameters, "无法读取 Skills 状态。");
        if (result is null || _skillRows is null)
            return;
        _skillRows.Children.Clear();
        foreach (var target in Items(result.Value, "targets"))
        {
            var id = Text(target, "target", Text(target, "id"));
            if (string.IsNullOrWhiteSpace(id))
                continue;
            definitions.TryGetValue(id, out var definition);
            var status = Text(target, "status", "unknown");
            _skillRows.Children.Add(new CheckBox
            {
                Content = $"{Text(target, "label", Text(definition, "label", id))}：{SkillStatusLabel(status)}",
                Tag = id,
                IsChecked = Bool(definition, "default")
            });
        }
        if (_skillRows.Children.Count == 0)
            _skillRows.Children.Add(Body("后端没有返回可管理的 Skill 目标。"));
    }

    private async Task InstallSelectedSkillsAsync()
    {
        if (_skillRows is null)
            return;
        var targets = _skillRows.Children.OfType<CheckBox>()
            .Where(check => check.IsChecked == true && check.Tag is string id && !string.IsNullOrWhiteSpace(id))
            .Select(check => (string)check.Tag)
            .ToArray();
        if (targets.Length == 0)
        {
            ShowNotice("请选择目标", "至少选择一个 Skill 后才能安装或更新。", InfoBarSeverity.Warning);
            return;
        }
        var result = await RequestAsync("skills.install", new { targets }, "无法启动 Skills 安装。");
        if (result is not null)
        {
            RegisterOwnedRun(Text(result.Value, "run_id"), "skills.install", "skills-install");
            ShowNotice("正在安装", "完成后会自动刷新所选工具的安装状态。", InfoBarSeverity.Informational);
        }
    }

    private async Task CopyBundledCli()
    {
        var result = await RequestAsync("cli.status", new { }, "无法读取内置 CLI 路径。");
        if (result is null)
            return;
        var path = Text(result.Value, "bundled_path");
        if (string.IsNullOrWhiteSpace(path))
        {
            ShowNotice("没有可复制的路径", "后端没有报告内置 CLI 路径。", InfoBarSeverity.Warning);
            return;
        }
        CopyText($"& \"{path}\" --help");
        ShowNotice("已复制", "已复制不含密钥的 PowerShell 调用示例。", InfoBarSeverity.Success);
    }

    private async Task EnableBundledCliAsync()
    {
        if (!await ConfirmAsync("启用 App 内置命令", "此操作会让当前用户显式启用 App 内置命令。若发现同名外部 CLI，后端会拒绝覆盖；App 不会静默修改 PATH。", "启用"))
            return;
        var result = await RequestAsync("cli.enable", new { confirm = true }, "无法启用内置命令。");
        if (result is not null)
        {
            if (Bool(result.Value, "ok"))
                ShowNotice("已启用", "已按你的确认启用内置命令。", InfoBarSeverity.Success);
            else
                ShowNotice("未启用", Text(result.Value, "error", "后端拒绝了该操作，外部 CLI 未被覆盖。"), InfoBarSeverity.Warning);
            await LoadCliStatusAsync();
        }
    }

    private async Task SelectConfigDirectoryAsync()
    {
        var folder = await PickFolderAsync();
        if (folder is null)
            return;
        if (_providerDraft.Count > 0 && !await ConfirmAsync("切换配置目录", "当前还有未保存的修改。切换目录将放弃这些修改。", "放弃并切换")) return;
        var result = await RequestAsync("profile.select", new { config_dir = folder.Path }, "无法切换配置目录。");
        if (result is not null)
        {
            _providerDraft.Clear();
            _fieldEditors.Clear();
            ApplyState(result.Value);
            ShowNotice("已切换配置目录", "后端会以这个目录重新读取配置和活动状态。", InfoBarSeverity.Success);
            RenderCurrentPage();
        }
    }

    private async Task CheckForUpdateAsync()
    {
        await UpdateRequestAsync("app.update-check");
    }

    private async Task UpdateRequestAsync(string method, object? parameters = null)
    {
        var result = await RequestAsync(method, parameters ?? new { }, "更新操作未完成。");
        if (result is not null) { _updates = result.Value.Clone(); RenderUpdateState(); }
    }

    private void RenderUpdateState()
    {
        if (_currentPage != "settings" || _appUpdateSummary is null) return;
        var app = Property(_updates, "app");
        var cli = Property(_updates, "cli");
        var installed = Property(_state, "cli");
        var download = Property(_updates, "download");
        var cliUpdate = Property(_updates, "cli_update");
        _settingAutoUpdate = true;
        if (_autoUpdateSwitch is not null) _autoUpdateSwitch.IsOn = Bool(_updates, "auto_check", true);
        _settingAutoUpdate = false;
        _updateCheckSummary!.Text = Bool(_updates, "checking") ? "正在检查官方稳定版本…" :
            Text(_updates, "error") is { Length: > 0 } error ? error :
            Number(Property(_updates, "app"), "checked_at") > 0 ? $"App 检查时间：{Timestamp(app, "checked_at")} · CLI：{Timestamp(cli, "checked_at")}" : "尚未检查更新。";
        var appStatus = Number(app, "checked_at") <= 0 ? "尚未检查" : Bool(app, "available") ? "有新版可下载" : Bool(app, "version_known") ? "没有更高的可安装版本" : "当前为开发版或版本未知";
        _appUpdateSummary.Text = $"App：{Text(app, "current_version", "未知")} · 内置引擎：{Text(_state, "version", "未知")}\n" +
            $"可安装稳定版：{Text(app, "latest_version", "未就绪")} · {appStatus}" +
            (Bool(app, "package_pending") ? "\n较新的发行版尚未提供本平台完整安装包。" : "");
        _cliUpdateSummary!.Text = $"实际版本：{Text(installed, "external_version", "未安装或未知")} · npm 稳定版：{Text(cli, "latest_version", "尚未检查")}\n" +
            $"来源：{Text(installed, "manager_label", "未确认")}\n生效路径：{Text(installed, "resolved_path", Text(installed, "external_path", "未发现"))}\n入口：{Text(installed, "external_path", "未发现")}\n{Text(installed, "update_note")}";
        var downloadStatus = Text(download, "status");
        _downloadSummary!.Text = downloadStatus switch
        {
            "downloading" => $"正在下载：{Number(download, "received") / 1048576:F1} / {Number(download, "total") / 1048576:F1} MiB",
            "cancelling" => "正在取消下载…",
            "ready" => $"已下载并校验：{Text(Property(download, "asset"), "version")}；尚未安装。",
            "failed" or "cancelled" => Text(download, "error"), _ => "点击后下载，不会自动安装。"
        };
        _downloadProgress!.Visibility = downloadStatus is "downloading" or "cancelling" ? Visibility.Visible : Visibility.Collapsed;
        _downloadProgress.Value = Number(download, "total") > 0 ? 100 * Number(download, "received") / Number(download, "total") : 0;
        _cliUpdateLog!.Text = Text(cli, "command") + "\n" + Text(cliUpdate, "log") + "\n" + Text(cliUpdate, "error");
        if (Text(cliUpdate, "status") == "running") _cliUpdateSummary.Text += "\n正在更新，请保持 App 打开。";
        if (Text(cliUpdate, "status") == "finished") _cliUpdateSummary.Text += "\n已更新并验证实际版本。";
        if (Text(cliUpdate, "status") == "failed") _cliUpdateSummary.Text += "\n更新未完成，请查看日志。";
        RefreshActionButtons();
    }

    private async Task OpenUpdateDirectoryAsync()
    {
        var path = Text(Property(_updates, "download"), "path");
        if (!string.IsNullOrWhiteSpace(path) && Path.GetDirectoryName(path) is { } directory)
            await Launcher.LaunchFolderAsync(await StorageFolder.GetFolderFromPathAsync(directory));
    }

    private async Task UpdateCliAsync()
    {
        var version = Text(Property(_updates, "cli"), "latest_version");
        var installed = Property(_state, "cli");
        if (!await ConfirmAsync("更新独立 CLI", $"来源：{Text(installed, "manager_label")}\n生效路径：{Text(installed, "resolved_path", Text(installed, "external_path"))}\n{Text(installed, "external_version")} → {version}\n\n只更新 Smart Search。请先结束其他终端中的 CLI 调用；更新期间请保持 App 打开。", "更新 CLI")) return;
        await UpdateRequestAsync("cli.update", new { confirm = true, version });
    }

    private async Task InstallUpdateAsync()
    {
        if (_providerDraft.Count > 0 || ConfigOperationBusy || HasActiveOwnedRuns || Text(Property(_updates, "cli_update"), "status") == "running")
        {
            ShowNotice("暂不能安装", "请先保存或处理配置修改，并等待或取消 App 自有任务。", InfoBarSeverity.Warning);
            return;
        }
        if (!await ConfirmAsync("打开安装器并退出 App", "安装包已校验 SHA256，但系统代码签名尚未验证。继续会关闭本 App 并启动当前用户安装器；按安装器提示完成后重新打开核对版本。", "继续安装")) return;
        var result = await RequestAsync("updates.installer", new { }, "安装包未就绪。");
        if (result is null) return;
        var path = Text(result.Value, "path");
        if (!Path.IsPathFullyQualified(path) || !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return;
        // Release the installer presence handle only after our backend is stopped.
        _shuttingDown = true;
        _activityTimer.Stop();
        await _backend.StopAsync();
        App.ReleaseInstallerMutex();
        try
        {
            using var installer = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            if (installer is null) throw new IOException("Installer did not start.");
        }
        catch
        {
            App.RestoreInstallerMutex();
            _shuttingDown = false;
            await ConnectAsync();
            ShowNotice("安装器未启动", "已恢复 App 连接。请稍后重试，或从下载目录手动打开安装器。", InfoBarSeverity.Error);
            return;
        }
        await _backend.DisposeAsync();
        _tray.Dispose();
        _allowClose = true;
        Close();
    }

    private async Task ResetProvidersAsync()
    {
        if (!await ConfirmAsync("重置服务商健康状态", "这会清除本地保存的服务商健康记录，不能撤销。不会删除密钥或配置。", "重置"))
            return;
        var result = await RequestAsync("providers.reset", new { }, "无法重置服务商状态。");
        if (result is not null)
        {
            ShowNotice("已重置", "服务商健康状态已清除；下次用户主动测试或调用会建立新的状态。", InfoBarSeverity.Success);
            await RefreshStateAsync();
        }
    }

    private void OnBackendEvent(object? sender, BackendEvent backendEvent)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            if (backendEvent.Name == "updates")
            {
                _updates = backendEvent.Data.Clone();
                var installed = Property(Property(_updates, "cli_update"), "cli");
                if (_state is { } state && installed.ValueKind == JsonValueKind.Object)
                {
                    var snapshot = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(state.GetRawText())!;
                    snapshot["cli"] = installed.Clone();
                    _state = JsonSerializer.SerializeToElement(snapshot);
                }
                RenderUpdateState();
            }
            if (backendEvent.Name.Equals("run", StringComparison.OrdinalIgnoreCase))
                await FinishOwnedRunAsync(backendEvent.Data);
            if (_currentPage == "activity") await RefreshActivityAsync(silent: true);
        });
    }

    private async Task FinishOwnedRunAsync(JsonElement data)
    {
        var runId = Text(data, "run_id");
        if (!_ownedRuns.Contains(runId) || !_handlingRunResults.Add(runId)) return;
        try
        {
            var status = Text(data, "status", "running");
            _ownedRunStatus[runId] = status;
            if (!IsTerminal(status)) return;
            await LoadRunResultAsync(runId, data);
            _operations.EndRun(runId);
            if (NoticeBar.Title?.ToString() == "正在测试未保存的修改" &&
                !_ownedRunStatus.Any(item => _ownedRunKinds.GetValueOrDefault(item.Key) == "provider.test" && !IsTerminal(item.Value)))
                NoticeBar.IsOpen = false;
            if (_ownedRunKinds.GetValueOrDefault(runId) == "provider.test")
                await RefreshProviderStateAfterTestAsync();
            else if (_ownedRunKinds.GetValueOrDefault(runId) == "skills.install" && _currentPage == "ai")
                await RunOperationAsync("skills-status", LoadSkillsAsync);
            RefreshActionButtons();
        }
        finally { _handlingRunResults.Remove(runId); }
    }

    private async Task RefreshProviderStateAfterTestAsync()
    {
        var refreshed = await RequestAsync("get_state", new { }, "无法刷新测试结果。");
        if (refreshed is null) return;
        if (_state is { } current)
        {
            var snapshot = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(current.GetRawText())!;
            snapshot["provider_checks"] = Property(refreshed.Value, "provider_checks").Clone();
            snapshot["provider_health"] = Property(refreshed.Value, "provider_health").Clone();
            _state = JsonSerializer.SerializeToElement(snapshot);
        }
        // Keep the live editors, focus, disclosure state and scroll position intact.
        RefreshActionButtons();
    }

    private void OnBackendDisconnected(object? sender, string message) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            _activityTimer.Stop();
            _operations.Clear();
            foreach (var runId in _ownedRunStatus.Keys.ToArray())
                if (!IsTerminal(_ownedRunStatus[runId])) _ownedRunStatus[runId] = "interrupted";
            _state = null;
            ShowNotice("本地引擎已断开", message, InfoBarSeverity.Error);
            RenderCurrentPage();
        });

    private async Task LoadRunResultAsync(string runId, JsonElement eventData)
    {
        JsonElement result;
        if (eventData.TryGetProperty("result", out var embedded) && embedded.ValueKind != JsonValueKind.Null)
            result = embedded.Clone();
        else
        {
            var response = await RequestAsync("run.result", new { run_id = runId }, "无法读取任务结果。");
            if (response is null || !response.Value.TryGetProperty("result", out var fetched) || fetched.ValueKind == JsonValueKind.Null)
                return;
            result = fetched.Clone();
        }
        _ownedRunResults[runId] = result.Clone();
        if (IsSearchRun(runId) && _selectedResultRunId == runId && _resultText is not null)
        {
            _selectedResultRunId = runId;
            RenderResult(result);
        }
    }

    private async Task ShowRunResultAsync(string runId)
    {
        if (!_ownedRuns.Contains(runId))
            return;
        if (!_ownedRunResults.TryGetValue(runId, out var result))
        {
            var response = await RequestAsync("run.result", new { run_id = runId }, "无法读取任务结果。");
            if (response is null || !response.Value.TryGetProperty("result", out var fetched) || fetched.ValueKind == JsonValueKind.Null)
                return;
            result = fetched.Clone();
            _ownedRunResults[runId] = result;
        }
        _selectedResultRunId = runId;
        await NavigateToAsync("search");
        if (_resultText is not null)
            RenderResult(result);
    }

    private void RenderResult(JsonElement result)
    {
        if (_resultText is null || _rawResult is null || _sourceRows is null)
            return;
        _resultText.Text = ReadableResult(result);
        _rawResult.Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        _lastResultExport = _resultText.Text;
        _sourceRows.Children.Clear();
        foreach (var source in Items(result, "sources"))
        {
            var url = Text(source, "url", Text(source, "link"));
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                var title = Text(source, "title", Text(source, "name", uri.Host));
                var label = title == uri.Host ? title : $"{title} · {uri.Host}";
                var link = new HyperlinkButton
                {
                    Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap }, NavigateUri = uri,
                    HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left
                };
                ToolTipService.SetToolTip(link, uri.AbsoluteUri);
                _sourceRows.Children.Add(link);
            }
        }
        _sourceDisclosure!.Header = $"来源链接（{_sourceRows.Children.Count}）";
        _sourceDisclosure.Visibility = _sourceRows.Children.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private Task CopyResult()
    {
        if (string.IsNullOrWhiteSpace(_resultText?.Text))
        {
            ShowNotice("没有可复制的结果", "先运行一个工具并等待结果。", InfoBarSeverity.Informational);
            return Task.CompletedTask;
        }
        CopyText(_resultText.Text);
        ShowNotice("已复制", "已复制当前可读结果。", InfoBarSeverity.Success);
        return Task.CompletedTask;
    }

    private async Task ExportResultAsync()
    {
        if (string.IsNullOrWhiteSpace(_lastResultExport))
        {
            ShowNotice("没有可导出的结果", "先运行一个工具并等待结果。", InfoBarSeverity.Informational);
            return;
        }
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, _windowHandle);
        picker.FileTypeChoices.Add("文本文件", [".txt"]);
        picker.SuggestedFileName = "smart-search-result";
        var file = await picker.PickSaveFileAsync();
        if (file is not null)
        {
            await FileIO.WriteTextAsync(file, _lastResultExport);
            ShowNotice("已导出", "结果已写入你选择的文件。", InfoBarSeverity.Success);
        }
    }

    private async Task<StorageFolder?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, _windowHandle);
        return await picker.PickSingleFolderAsync();
    }

    private async Task NavigateToAsync(string tag)
    {
        var target = RootNavigation.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(item => item.Tag as string == tag);
        if (target is not null)
            RootNavigation.SelectedItem = target;
        await Task.CompletedTask;
    }

    private void RegisterOwnedRun(string runId, string kind, string operationKey)
    {
        if (string.IsNullOrWhiteSpace(runId)) return;
        _ownedRuns.Add(runId);
        _ownedRunStatus[runId] = "running";
        _ownedRunKinds[runId] = kind;
        _operations.TrackRun(operationKey, runId);
        RefreshActionButtons();
        _ = RecoverRunAsync(runId);
    }

    private async Task RecoverRunAsync(string runId)
    {
        // A very fast worker can finish before the start response reaches the UI.
        var response = await RequestAsync("run.result", new { run_id = runId }, "无法读取任务状态。");
        if (response is { } data) await FinishOwnedRunAsync(data);
    }

    private bool IsSearchRun(string runId) => _ownedRunKinds.TryGetValue(runId, out var kind) && kind is not "provider.test" and not "skills.install";

    private bool HasActiveOwnedRuns => _ownedRunStatus.Values.Any(status => !IsTerminal(status));

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose)
            return;
        args.Cancel = true;
        await RequestCloseAsync();
    }

    private async Task RequestCloseAsync()
    {
        if (_shuttingDown)
            return;
        if (_operations.IsBusy("updates-cli") || Text(Property(_updates, "cli_update"), "status") == "running")
        {
            ShowNotice("CLI 正在更新", "请等待原管理器完成，避免中断安装。可以最小化 App。", InfoBarSeverity.Warning);
            return;
        }
        if (HasActiveOwnedRuns)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = DialogRoot,
                RequestedTheme = ((FrameworkElement)Content).ActualTheme,
                Title = "仍有 Smart Search 任务在运行",
                Content = "继续后台运行可从通知区域恢复；取消任务并退出只会取消本 App 发起的任务，不会终止外部 CLI。",
                PrimaryButtonText = "继续在后台",
                SecondaryButtonText = "取消任务并退出",
                CloseButtonText = "返回"
            };
            switch (await dialog.ShowAsync())
            {
                case ContentDialogResult.Primary:
                    HideToTray();
                    return;
                case ContentDialogResult.Secondary:
                    foreach (var runId in _ownedRunStatus.Where(entry => !IsTerminal(entry.Value)).Select(entry => entry.Key).ToArray())
                        await CancelOwnedRunAsync(runId);
                    await ShutdownAndCloseAsync();
                    return;
                default:
                    return;
            }
        }
        await ShutdownAndCloseAsync();
    }

    private void HideToTray()
    {
        ShowWindow(_windowHandle, SwHide);
        _tray.Show();
    }

    private void ShowMainWindow()
    {
        ShowWindow(_windowHandle, SwRestore);
        Activate();
        _tray.Hide();
    }

    internal void ActivateFromRedirect() => ShowMainWindow();

    private async void ExitFromTray()
    {
        ShowMainWindow();
        await RequestCloseAsync();
    }

    private async Task ShutdownAndCloseAsync()
    {
        if (_shuttingDown)
            return;
        _shuttingDown = true;
        _activityTimer.Stop();
        _tray.Hide();
        await _backend.DisposeAsync();
        _tray.Dispose();
        _allowClose = true;
        Close();
    }

    private async Task<bool> ConfirmAsync(string title, string content, string confirm)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = DialogRoot,
            RequestedTheme = ((FrameworkElement)Content).ActualTheme,
            Title = title,
            Content = content,
            PrimaryButtonText = confirm,
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private XamlRoot DialogRoot => ((FrameworkElement)Content).XamlRoot;

    private static Style UiStyle(string key) => (Style)Application.Current.Resources[key];

    private static StackPanel PagePanel() => new() { Spacing = 20, MaxWidth = 1120, HorizontalAlignment = HorizontalAlignment.Left };

    private ScrollViewer Scroll(UIElement content)
    {
        var host = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        host.Children.Add(content);
        host.SizeChanged += (_, args) =>
        {
            if (content is FrameworkElement element && args.NewSize.Width > 0)
                element.Width = Math.Min(1120, args.NewSize.Width);
        };
        var scroll = new ScrollViewer
        {
            Content = host, Padding = new Thickness(24), HorizontalContentAlignment = HorizontalAlignment.Stretch,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var page = _currentPage;
        scroll.Loaded += (_, _) => scroll.ChangeView(null, _pageOffsets.GetValueOrDefault(page), null, true);
        _pageScroll = scroll;
        return scroll;
    }

    private static TextBlock PageTitle(string text) => new() { Text = text, Style = UiStyle("PageHeadingStyle") };
    private static TextBlock SectionHeading(string text) => new() { Text = text, Style = UiStyle("SectionHeadingStyle") };
    private static TextBlock Body(string text) => new() { Text = text, Style = UiStyle("BodyCopyStyle") };
    private static TextBlock Secondary(string text) => new() { Text = text, Style = UiStyle("SecondaryCopyStyle") };
    private static TextBlock DataText(string text) => new() { Text = text, Style = UiStyle("DataCopyStyle") };
    private static ContentControl Card(UIElement content) => new() { Content = content, Style = UiStyle("SurfaceCardStyle") };
    private static ContentControl Badge(string text, string tone) => new() { Content = text, Style = UiStyle("Status" + tone + "Style") };

    private static UIElement HeadingWithStatus(string heading, string status, string tone)
    {
        var row = new Grid { ColumnSpacing = 16 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(SectionHeading(heading));
        var badge = Badge(status, tone);
        Grid.SetColumn(badge, 1);
        row.Children.Add(badge);
        return row;
    }

    private static UIElement KeyValue(string label, string value) => new StackPanel
    {
        Spacing = 6, Children = { Secondary(label), DataText(value) }
    };

    private static UIElement Section(string heading, IEnumerable<UIElement> children)
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(SectionHeading(heading));
        foreach (var child in children) panel.Children.Add(child);
        return panel;
    }

    private Expander Disclosure(string key, string heading, UIElement content, bool expanded = false)
    {
        var expander = new Expander
        {
            Header = heading, Content = content, IsExpanded = _disclosures.GetValueOrDefault(key, expanded),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        expander.Expanding += (_, _) => _disclosures[key] = true;
        expander.Collapsed += (_, _) => _disclosures[key] = false;
        return expander;
    }

    private static UIElement FieldRow(UIElement label, UIElement input)
    {
        var row = new Grid { ColumnSpacing = 20, RowSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        row.Children.Add(label);
        row.Children.Add(input);
        row.SizeChanged += (_, args) =>
        {
            var wide = args.NewSize.Width >= 640;
            row.ColumnDefinitions[0].Width = wide ? new GridLength(220) : new GridLength(1, GridUnitType.Star);
            row.ColumnDefinitions[1].Width = wide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            Grid.SetColumn((FrameworkElement)input, wide ? 1 : 0);
            Grid.SetRow((FrameworkElement)input, wide ? 0 : 1);
        };
        return row;
    }

    private static StackPanel ActionRow(params UIElement[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var child in children) row.Children.Add(child);
        row.SizeChanged += (_, args) => row.Orientation = args.NewSize.Width < 540 ? Orientation.Vertical : Orientation.Horizontal;
        return row;
    }

    private static IEnumerable<UIElement> CapabilityChains(JsonElement state)
    {
        var chains = Property(state, "capability_chains");
        if (chains.ValueKind != JsonValueKind.Object) yield break;
        foreach (var chain in chains.EnumerateObject())
            yield return KeyValue(CapabilityLabel(chain.Name) + "回退顺序", string.Join(" → ", Items(chain.Value).Select(value => ProviderLabel(value.GetString() ?? ""))));
    }

    private static TextBlock OfflineHint() => Secondary("本地引擎未连接。请先重新连接，再读取配置和运行任务。");

    private Button ActionButton(string text, Func<Task> action, bool primary = false, string? operationKey = null,
        string busyText = "处理中…", Func<string>? label = null, Func<string>? dynamicKey = null)
    {
        var button = new Button { MinHeight = 36, HorizontalAlignment = HorizontalAlignment.Left };
        if (primary) button.Style = UiStyle("AccentButtonStyle");
        var binding = new ActionBinding(dynamicKey ?? (() => operationKey ?? text), label ?? (() => text), busyText);
        _actionButtons[button] = binding;
        UpdateActionButton(button, binding);
        button.Click += async (_, _) => await RunOperationAsync(binding.Key(), action);
        return button;
    }

    private async Task RunOperationAsync(string key, Func<Task> action)
    {
        if (new[] { "state", "config-save", "config-preview", "profile" }.Contains(key) && ConfigOperationBusy) return;
        if (!_operations.Begin(key)) return;
        RefreshActionButtons();
        try { await action(); }
        catch (Exception error) { ShowNotice("操作未完成", SafeMessage(error), InfoBarSeverity.Error); }
        finally { _operations.EndRequest(key); RefreshActionButtons(); }
    }

    private bool ConfigOperationBusy => new[] { "state", "config-save", "config-preview", "profile" }.Any(_operations.IsBusy);

    private void UpdateActionButton(Button button, ActionBinding binding)
    {
        var key = binding.Key();
        var downloadStatus = Text(Property(_updates, "download"), "status");
        var downloading = downloadStatus is "downloading" or "cancelling";
        var updatingCli = Text(Property(_updates, "cli_update"), "status") == "running";
        var busy = _operations.IsBusy(key) || key == "updates-check" && Bool(_updates, "checking") ||
            key == "updates-download" && downloading || key == "updates-cancel" && downloadStatus == "cancelling" || key == "updates-cli" && updatingCli;
        var allowed = key switch
        {
            "updates-download" => Bool(Property(_updates, "app"), "available") && Text(Property(_updates, "app"), "error").Length == 0,
            "updates-cancel" => downloadStatus == "downloading",
            "updates-install" or "updates-folder" => Text(Property(_updates, "download"), "status") == "ready",
            "updates-cli" => Bool(Property(_state, "cli"), "can_update") && Bool(Property(_updates, "cli"), "available") && !Bool(_updates, "checking") && Text(Property(_updates, "cli"), "error").Length == 0,
            "updates-copy" => Text(Property(_updates, "cli"), "command").Length > 0,
            _ => true
        };
        button.IsEnabled = allowed && !busy && !(new[] { "state", "config-save", "config-preview", "profile" }.Contains(key) && ConfigOperationBusy);
        var label = busy ? binding.BusyText : binding.Label();
        if (busy)
            button.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 8,
                Children = { new ProgressRing { IsActive = true, Width = 16, Height = 16 }, new TextBlock { Text = label } }
            };
        else button.Content = label;
        AutomationProperties.SetName(button, label);
    }

    private void RefreshActionButtons()
    {
        foreach (var (button, binding) in _actionButtons.ToArray()) UpdateActionButton(button, binding);
        if (_currentPage == "providers")
        {
            var changes = CollectDraft();
            var count = changes.Set.Count + changes.Unset.Count;
            if (_saveSummary is not null)
                _saveSummary.Text = count > 0 ? $"有 {count} 项未保存修改 · 密钥留空会保留原值" : "没有未保存的修改 · 测试不会自动保存配置";
            foreach (var editor in _fieldEditors.Values)
            {
                editor.Input.IsEnabled = !_operations.IsBusy("config-save");
                if (editor.Clear is not null) editor.Clear.IsEnabled = !_operations.IsBusy("config-save");
            }
            if (_state is { } state)
                foreach (var (provider, host) in _providerStatusPanels)
                {
                    var updated = BuildProviderStatus(state, provider);
                    var children = updated.Children.ToArray();
                    updated.Children.Clear();
                    host.Children.Clear();
                    foreach (var child in children) host.Children.Add(child);
                }
        }
        if (_activityEnabledSwitch is not null) _activityEnabledSwitch.IsEnabled = !_operations.IsBusy("activity-setting");
    }

    private string ProviderTestLabel(string provider)
    {
        var draft = CollectDraft(provider);
        if (Text(Property(_state, "probe_kinds"), provider) == "presence")
            return draft.Set.Count + draft.Unset.Count > 0 ? "检查未保存的配置" : "检查配置";
        return draft.Set.Count + draft.Unset.Count > 0 ? "用未保存的修改测试" : "测试";
    }

    private Control CreateFieldInput(JsonElement field, bool secret, string value, bool isLocked)
    {
        if (isLocked)
            return new TextBox { Text = value, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas") };
        if (secret)
            return new PasswordBox { PlaceholderText = "留空将保留当前密钥", IsEnabled = true };
        if (Text(field, "kind").Equals("bool", StringComparison.OrdinalIgnoreCase))
            return new ToggleSwitch { IsOn = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("是", StringComparison.OrdinalIgnoreCase) };
        var choices = Items(field, "choices").Where(choice => choice.ValueKind == JsonValueKind.String).Select(choice => choice.GetString()!).ToList();
        if (choices.Count > 0)
        {
            var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var choice in choices)
                combo.Items.Add(choice);
            combo.SelectedItem = choices.FirstOrDefault(choice => choice.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? choices.FirstOrDefault();
            return combo;
        }
        return new TextBox { Text = value, PlaceholderText = Text(field, "placeholder", Text(field, "default")), TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas") };
    }

    private Control CreateCommandInput(JsonElement field)
    {
        var kind = Text(field, "kind");
        if (kind.Equals("bool", StringComparison.OrdinalIgnoreCase))
            return new ToggleSwitch();
        var choices = Items(field, "choices").Where(choice => choice.ValueKind == JsonValueKind.String).Select(choice => choice.GetString()!).ToList();
        if (kind.Equals("choice", StringComparison.OrdinalIgnoreCase) && choices.Count > 0)
        {
            var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var choice in choices)
                combo.Items.Add(choice);
            if (!string.IsNullOrWhiteSpace(Text(field, "default")))
                combo.SelectedItem = Text(field, "default");
            return combo;
        }
        return new TextBox
        {
            Text = Text(field, "default"),
            PlaceholderText = Bool(field, "multiple") ? "每行一个值" : string.Empty,
            AcceptsReturn = Bool(field, "multiple"),
            TextWrapping = TextWrapping.Wrap
        };
    }

    private DraftChange CollectDraft(string? provider = null)
    {
        var set = new Dictionary<string, object?>();
        var unset = new List<string>();
        foreach (var (key, editor) in _fieldEditors)
        {
            if (editor.Locked || (provider is not null && !Text(editor.Field, "provider").Equals(provider, StringComparison.OrdinalIgnoreCase)))
                continue;
            var value = ReadEditorValue(editor);
            if (!HasDraftChange(editor, value))
                continue;
            if (editor.Clear?.IsChecked == true)
            {
                unset.Add(key);
                continue;
            }
            set[key] = ConvertValue(editor, value);
        }
        return new DraftChange(set, unset);
    }

    private Dictionary<string, FieldDraft> CaptureDraft()
    {
        var captured = new Dictionary<string, FieldDraft>(StringComparer.Ordinal);
        foreach (var (key, editor) in _fieldEditors)
        {
            var value = ReadEditorValue(editor);
            if (!HasDraftChange(editor, value))
                continue;
            captured[key] = new FieldDraft(value.Text, value.IsChecked, editor.Clear?.IsChecked == true);
        }
        return captured;
    }

    private static bool HasDraftChange(FieldEditor editor, CommandValue value) =>
        !editor.Locked && (editor.Clear?.IsChecked == true ||
                           (editor.Secret ? !string.IsNullOrWhiteSpace(value.Text) : !ControlValueComparer.Equal(value, editor.Initial)));

    private static void RestoreDraft(FieldEditor editor, FieldDraft draft)
    {
        RestoreControl(editor.Input, new CommandValue(draft.Text, draft.IsChecked));
        if (editor.Clear is not null) editor.Clear.IsChecked = draft.Clear;
    }

    private void CaptureCommandInputs()
    {
        if (_renderedCommandId is not null && _commandControls.Count > 0)
            _commandDrafts[_renderedCommandId] = _commandControls.ToDictionary(item => item.Key, item => ReadControl(item.Value));
    }

    private static void RestoreControl(Control input, CommandValue value)
    {
        switch (input)
        {
            case TextBox textBox: textBox.Text = value.Text ?? string.Empty; break;
            case PasswordBox passwordBox: passwordBox.Password = value.Text ?? string.Empty; break;
            case ComboBox comboBox: comboBox.SelectedItem = value.Text; break;
            case ToggleSwitch toggle: toggle.IsOn = value.IsChecked; break;
        }
    }

    private static CommandValue ReadEditorValue(FieldEditor editor) => ReadControl(editor.Input);

    private CommandValue ReadControl(string name) => _commandControls.TryGetValue(name, out var control) ? ReadControl(control) : new CommandValue(null);

    private static CommandValue ReadControl(Control control) => control switch
    {
        TextBox textBox => new CommandValue(textBox.Text),
        PasswordBox passwordBox => new CommandValue(passwordBox.Password),
        ComboBox comboBox => new CommandValue(comboBox.SelectedItem?.ToString()),
        ToggleSwitch toggle => new CommandValue(null, toggle.IsOn),
        _ => new CommandValue(null)
    };

    private static object? ConvertValue(FieldEditor editor, CommandValue value)
    {
        var kind = Text(editor.Field, "kind").ToLowerInvariant();
        return kind switch
        {
            "bool" => value.IsChecked,
            "int" when int.TryParse(value.Text, out var integer) => integer,
            "float" when double.TryParse(value.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) => number,
            _ => value.Text
        };
    }

    private static bool IsAdvanced(JsonElement field)
    {
        var tier = Text(field, "tier");
        var section = Text(field, "section");
        return tier.Equals("advanced", StringComparison.OrdinalIgnoreCase) || tier.Equals("expert", StringComparison.OrdinalIgnoreCase) ||
               section.Equals("routing", StringComparison.OrdinalIgnoreCase) || section.Equals("reliability", StringComparison.OrdinalIgnoreCase) || section.Equals("diagnostics", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCommandAdvanced(JsonElement field)
    {
        if (field.ValueKind == JsonValueKind.Object && field.TryGetProperty("advanced", out var advanced) && advanced.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return advanced.GetBoolean();
        return Items(field, "flags").Any();
    }

    private static bool IsSecret(JsonElement field)
    {
        var kind = Text(field, "kind");
        var key = Text(field, "key");
        return kind.Equals("secret", StringComparison.OrdinalIgnoreCase) || kind.Equals("password", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("KEY", StringComparison.OrdinalIgnoreCase) || key.Contains("TOKEN", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("SECRET", StringComparison.OrdinalIgnoreCase) || key.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase);
    }

    private static string Label(JsonElement field) => Text(field, "label_zh", Text(field, "label_en", Text(field, "key")));

    private static string SourceLabel(string source) => source.ToLowerInvariant() switch
    {
        "environment" => "环境变量",
        "config_file" => "配置文件",
        "default" => "默认值",
        _ => string.IsNullOrWhiteSpace(source) ? "未知" : source
    };

    private static string MissingText(JsonElement value)
    {
        var missing = Items(value, "missing").Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()).Where(item => !string.IsNullOrWhiteSpace(item));
        return string.Join("、", missing.Select(value => CapabilityLabel(value!))) is { Length: > 0 } text ? text : "未说明";
    }

    private static string CapabilityLabel(string capability) => capability switch
    {
        "main_search" => "主搜索",
        "web_search" => "网页搜索",
        "docs_search" => "文档检索",
        "web_fetch" => "网页读取",
        "vertical_search" => "垂直搜索",
        _ => capability
    };

    private string ProviderCheckLabel(string status)
    {
        var label = Text(Property(Property(Property(_state, "metadata"), "status_labels"), status.ToLowerInvariant()), "zh");
        if (!string.IsNullOrWhiteSpace(label)) return label;
        return status.ToLowerInvariant() switch
        {
            "ok" or "passed" or "success" => "通过",
            "failed" or "error" => "未通过",
            "cancelled" => "已取消",
            "timeout" => "超时",
            "not_configured" => "未配置",
            "config_error" or "parameter_error" => "配置需调整",
            "auth_error" => "凭据不可用",
            "rate_limit" or "rate_limited" => "请求受限",
            "network_error" => "网络异常",
            "warning" => "需要确认",
            "runtime_error" or "interrupted" => "已中断",
            "running" => "测试中",
            "closed" => "已结束（不推断成功）",
            _ => "状态未知"
        };
    }

    private static string StatusTone(string status) => status.ToLowerInvariant() switch
    {
        "ok" or "passed" or "success" or "finished" or "up_to_date" => "Success",
        "running" or "cancelling" => "Active",
        "timeout" or "warning" or "stale" or "rate_limit" or "rate_limited" or "configured" or "not_configured" => "Warning",
        "error" or "failed" or "interrupted" => "Error",
        _ when status.EndsWith("_error", StringComparison.OrdinalIgnoreCase) => "Error",
        _ => "Neutral"
    };

    private static string ProviderLabel(string provider) => provider switch
    {
        "openai-compatible" => "OpenAI 兼容接口", "xai-responses" => "xAI", "exa" => "Exa",
        "context7" => "Context7", "tavily" => "Tavily", "jina" => "Jina", "firecrawl" => "Firecrawl",
        "zhipu" => "智谱", "zhipu-mcp" => "智谱 Coding Plan", "zhipu-mcp-reader" => "智谱网页阅读",
        "anysearch" => "AnySearch · 实验性", "sciverse" => "SciVerse · 实验性", _ => provider
    };

    private string CommandLabel(string command) => command switch
    {
        "provider.test" => "服务商测试", "skills.install" => "安装 / 更新 Skills", "version" => "版本查询",
        "config" => "配置管理", "skills" => "Skills 管理", "providers" => "服务商检查", "model" => "模型设置",
        _ => Items(Property(_state, "commands")).Where(item => Text(item, "id") == command).Select(item => Text(item, "label")).FirstOrDefault() ?? "其他任务"
    };

    private static string PhaseLabel(string phase) => phase switch
    {
        "started" or "starting" => "已启动", "running" => "运行中", "completed" or "finished" => "已完成",
        "failed" => "失败", "cancelled" => "已取消", "cancelling" => "取消中",
        "provider.test" => "测试连接", "search" or "main_search" => "搜索", "fetch" or "web_fetch" => "读取网页",
        "routing" or "route" => "选择检索方式", "planning" or "deep" => "制定计划", "research" => "研究",
        _ => "处理请求"
    };

    private static string CooldownText(JsonElement health)
    {
        var seconds = Number(health, "cooldown_remaining_seconds");
        return seconds > 0 ? TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss") : "未返回";
    }

    private static string StatusLabel(string status) => status.ToLowerInvariant() switch
    {
        "running" => "运行中",
        "finished" => "已完成",
        "failed" => "失败",
        "cancelled" => "已取消",
        "cancelling" => "正在取消",
        "stale" => "状态未更新",
        "interrupted" => "已中断",
        _ => "未知"
    };

    private static string SkillStatusLabel(string status) => status.ToLowerInvariant() switch
    {
        "missing" => "未安装",
        "stale" => "可更新",
        "up_to_date" => "已是当前版本",
        _ => "状态未知"
    };

    private static bool IsTerminal(string status) => status.Equals("finished", StringComparison.OrdinalIgnoreCase) ||
                                                    status.Equals("failed", StringComparison.OrdinalIgnoreCase) ||
                                                    status.Equals("cancelled", StringComparison.OrdinalIgnoreCase) ||
                                                    status.Equals("stale", StringComparison.OrdinalIgnoreCase) ||
                                                    status.Equals("interrupted", StringComparison.OrdinalIgnoreCase);

    private static string ReadableResult(JsonElement result)
    {
        foreach (var name in new[] { "display_text", "answer", "content", "text", "summary", "message", "error" })
        {
            var candidate = Text(result, name);
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;
        }
        return "任务已返回结构化结果。展开“高级 JSON”可以查看完整字段；来源会列在本结果下方。";
    }

    private static string Elapsed(JsonElement run)
    {
        var milliseconds = Number(run, "elapsed_ms");
        return milliseconds <= 0 ? "未返回" : TimeSpan.FromMilliseconds(milliseconds).ToString(@"mm\:ss");
    }

    private static string Timestamp(JsonElement value, string property)
    {
        var seconds = Number(value, property);
        return seconds <= 0 ? "未返回" : DateTimeOffset.FromUnixTimeSeconds((long)seconds).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    }

    private static string TimestampOrText(JsonElement value, string property)
    {
        var seconds = Number(value, property);
        return seconds > 0 ? DateTimeOffset.FromUnixTimeSeconds((long)seconds).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : Text(value, property, "未返回");
    }

    private static JsonElement Property(JsonElement? value, string property) => value is { } element ? Property(element, property) : default;

    private static JsonElement Property(JsonElement value, string property) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var item) ? item : default;

    private static IEnumerable<JsonElement> Items(JsonElement value, string property) => Items(Property(value, property));

    private static IEnumerable<JsonElement> Items(JsonElement value) => value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(item => item.Clone()) : Enumerable.Empty<JsonElement>();

    private static string Text(JsonElement? value, string property, string fallback = "") => value is { } element ? Text(element, property, fallback) : fallback;

    private static string Text(JsonElement value, string property, string fallback = "") => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var item) ? Display(item, fallback) : fallback;

    private static string DisplayValue(JsonElement values, string key) => values.ValueKind == JsonValueKind.Object && values.TryGetProperty(key, out var value) ? Display(value) : string.Empty;

    private static string Display(JsonElement value, string fallback = "") => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? fallback,
        JsonValueKind.Number => value.ToString(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null or JsonValueKind.Undefined => fallback,
        _ => fallback
    };

    private static bool Bool(JsonElement? value, string property, bool fallback = false) => value is { } element && Bool(element, property, fallback);

    private static bool Bool(JsonElement value, string property, bool fallback = false) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var item)
            ? item.ValueKind == JsonValueKind.True || (item.ValueKind == JsonValueKind.String && bool.TryParse(item.GetString(), out var parsed) && parsed)
            : fallback;

    private static double Number(JsonElement value, string property) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var item) && item.TryGetDouble(out var result) ? result : 0;

    private IReadOnlyList<string> ActivityDirectories()
    {
        var directories = _extraActivityDirectories.ToList();
        var current = Text(_state, "config_dir", Text(_state, "config_path"));
        if (!string.IsNullOrWhiteSpace(current) && !directories.Contains(current, StringComparer.OrdinalIgnoreCase))
            directories.Insert(0, current);
        return directories;
    }

    private void RenderExtraDirectories(StackPanel rows)
    {
        if (_extraActivityDirectories.Count == 0)
        {
            rows.Children.Add(Body("没有额外目录。"));
            return;
        }
        foreach (var directory in _extraActivityDirectories.ToArray())
        {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            item.Children.Add(new TextBlock { Text = directory, TextWrapping = TextWrapping.Wrap, MaxWidth = 620, VerticalAlignment = VerticalAlignment.Center });
            item.Children.Add(ActionButton("移除", () =>
            {
                _extraActivityDirectories.Remove(directory);
                SaveExtraDirectories();
                RenderCurrentPage();
                return Task.CompletedTask;
            }));
            rows.Children.Add(item);
        }
    }

    private void LoadLocalPreferences()
    {
        try
        {
            if (File.Exists(PreferencesPath))
            {
                foreach (var (key, value) in JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(PreferencesPath)) ?? [])
                    _preferences[key] = value;
            }
            var raw = ReadSetting("activityDirectories");
            if (!string.IsNullOrWhiteSpace(raw))
                _extraActivityDirectories.AddRange(JsonSerializer.Deserialize<List<string>>(raw) ?? []);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            // An unreadable local preference is ignored instead of scanning unexpected paths.
        }
    }

    private void SaveExtraDirectories() => SaveSetting("activityDirectories", JsonSerializer.Serialize(_extraActivityDirectories));

    private string? ReadSetting(string key) => _preferences.GetValueOrDefault(key);

    private void SaveSetting(string key, string value)
    {
        _preferences[key] = value;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PreferencesPath)!);
            var temporary = PreferencesPath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(_preferences));
            File.Move(temporary, PreferencesPath, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The setting remains effective for this session even if its local persistence is unavailable.
        }
    }

    private static string PreferencesPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Smart Search", "desktop-preferences.json");

    private void ApplyTheme(string theme)
    {
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = theme switch
            {
                "light" => ElementTheme.Light,
                "dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }

    private static BackendLaunch ReadBackendLaunch()
    {
        var arguments = Environment.GetCommandLineArgs();
        string? path = null;
        var backendArguments = new List<string>();
        for (var index = 0; index < arguments.Length - 1; index++)
        {
            if (arguments[index].Equals("--backend", StringComparison.OrdinalIgnoreCase))
                path = arguments[index + 1];
            if (arguments[index].Equals("--backend-arg", StringComparison.OrdinalIgnoreCase))
                backendArguments.Add(arguments[index + 1]);
        }
        return new BackendLaunch(path, backendArguments);
    }

    private static string SafeMessage(Exception error) => error switch
    {
        BackendRpcException rpc => rpc.Message,
        BackendDisconnectedException disconnected => disconnected.Message,
        OperationCanceledException => "本地后端在 30 秒内没有响应。请检查后端后手动重试。",
        _ => "操作未完成。请检查连接或文件权限后重试。"
    };

    private void ShowNotice(string title, string message, InfoBarSeverity severity)
    {
        NoticeBar.Title = title;
        NoticeBar.Message = message;
        NoticeBar.Severity = severity;
        NoticeBar.IsOpen = true;
    }

    private static void CopyText(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    private sealed record CommandOption(string Id, string Label, string Description, bool Experimental, JsonElement Definition)
    {
        public override string ToString() => Experimental ? $"{Label}（实验性）" : Label;
    }

    private sealed record FieldDraft(string? Text, bool IsChecked, bool Clear);
    private sealed record ActionBinding(Func<string> Key, Func<string> Label, string BusyText);
    private sealed record ActivityRowView(Expander Row, Action<JsonElement> Update);

    private sealed record DraftChange(Dictionary<string, object?> Set, List<string> Unset);

    private sealed record FieldEditor(JsonElement Field, Control Input, CheckBox? Clear, CommandValue Initial, bool Secret, bool Locked);

    private sealed record BackendLaunch(string? Path, IReadOnlyList<string> Arguments);
}
