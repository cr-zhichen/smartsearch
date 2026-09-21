using static SmartSearch.Desktop.Localization;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SmartSearch.Desktop;

public sealed partial class MainWindow
{
    private readonly Dictionary<string, SkillTargetRow> _skillTargetRows = [];
    private TextBlock? _skillSelectionSummary;
    private bool _updatingSkillRows;

    private UIElement BuildAiPage()
    {
        var panel = PagePanel();
        panel.Children.Add(PageTitle(L("更新 Skills")));
        panel.Children.Add(Secondary(L("为编程 Agent 安装或更新 Smart Search Skill。软件更新不会自动同步这些文件。")));
        _skillSummary = Body(string.Empty);
        _skillResult = Body(string.Empty);
        _skillRows = new StackPanel { Spacing = 8 };
        _skillTargetRows.Clear();
        var source = new StackPanel { Spacing = 12 };
        source.Children.Add(SectionHeading(L("最新正式版 Skills")));
        source.Children.Add(_skillSummary);
        source.Children.Add(ActionRow(
            ActionButton(L("检查最新 Skills"), () => SkillsRequestAsync("skills.check"), operationKey: "skills-check", busyText: L("检查中…")),
            DetailsButton(L("检查详情与偏好"), ShowSkillPreferencesAsync)));
        panel.Children.Add(Card(source));
        panel.Children.Add(Card(SettingRow(L("运行环境"), L("所有 Agent 共用独立 CLI。准备完成后，选择需要更新 Skills 的 Agent。"),
            ActionButton(L("管理运行环境…"), OpenRuntimeSettingsAsync))));
        panel.Children.Add(SettingsSection(L("选择 Agent"), L("状态只表示 Smart Search Skill 内容。Codex 使用的 .agents/skills 也可能被其他兼容 Agent 读取。"), Card(_skillRows)));
        panel.Children.Add(_skillResult);
        _skillSelectionSummary = Secondary(string.Empty);
        var footer = WorkspaceFooter(_skillSelectionSummary,
            ActionRow(ActionButton(L("刷新本机状态"), LoadSkillsAsync, operationKey: "skills-status", busyText: L("刷新中…")),
                ActionButton(L("更新所选 Skills"), InstallSelectedSkillsAsync, primary: true, operationKey: "skills-install", busyText: L("更新中…"))));
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.Children.Add(Scroll(panel));
        Grid.SetRow(footer, 1);
        layout.Children.Add(footer);
        RenderSkillState();
        _ = RunOperationAsync("skills-status", LoadSkillsAsync);
        return layout;
    }

    private Task ShowSkillPreferencesAsync()
    {
        var source = Property(_skills, "source");
        _autoSkillsSwitch = CompactSwitch(L("每天自动检查 Skills，只提示，不写入"), Bool(_skills, "auto_check", true));
        _autoSkillsSwitch.Toggled += async (_, _) =>
        {
            if (!_settingAutoSkills) await SkillsRequestAsync("skills.auto", new { enabled = _autoSkillsSwitch.IsOn });
        };
        return ShowDetailsAsync(L("检查详情与偏好"), new StackPanel { Spacing = 16, Children =
        {
            SettingRow(L("每天自动检查 Skills，只提示，不写入"), string.Empty, _autoSkillsSwitch),
            Divider(), KeyValue(L("最近成功检查"), Timestamp(source, "checked_at")),
            KeyValue(L("独立 CLI"), Text(_skills, "cli_version", L("未发现"))),
            Secondary(L("不同内容会先备份；额外文件与未选目标保持原样。更新后重新打开 Agent 会话；Gemini 可运行 /skills reload。实际调用仍需在 Agent 中验证。"))
        } });
    }

    private void RenderSkillState()
    {
        if (_currentPage != "ai" || _skillRows is null || _skillSummary is null) { RefreshActionButtons(); return; }
        var source = Property(_skills, "source");
        var lines = new List<string>();
        lines.Add(Bool(_skills, "checking") ? L("正在检查官方稳定版 Skills…") : Text(source, "version").Length > 0
            ? L("npm {0}", Text(source, "version")) : L("尚未获取正式版 Skills；当前文件仅与 App 内置副本比较。"));
        if (Bool(_skills, "cached") && Text(source, "version").Length > 0) lines.Add(L("显示上次缓存；请检查最新 Skills 后再更新。"));
        if (Text(_skills, "error").Length > 0) lines.Add(Text(_skills, "error"));
        if (Text(_skills, "compatibility").Length > 0) lines.Add(Text(_skills, "compatibility"));
        _skillSummary.Text = string.Join("\n", lines);
        _settingAutoSkills = true;
        if (_autoSkillsSwitch is not null) _autoSkillsSwitch.IsOn = Bool(_skills, "auto_check", true);
        _settingAutoSkills = false;
        var targets = Items(Property(_skills, "targets")).ToList();
        if (!_skillSelectionInitialized && targets.Count > 0)
        {
            foreach (var definition in Items(Property(_state, "skill_targets")))
                if (Bool(definition, "default")) _selectedSkillTargets.Add(Text(definition, "id"));
            _skillSelectionInitialized = true;
        }
        var ids = targets.Select(target => Text(target, "target", Text(target, "id"))).Where(id => id.Length > 0).ToHashSet();
        foreach (var id in _skillTargetRows.Keys.Where(id => !ids.Contains(id)).ToArray())
        {
            _skillRows.Children.Remove(_skillTargetRows[id].Container);
            _skillTargetRows.Remove(id);
        }
        _updatingSkillRows = true;
        foreach (var target in targets)
        {
            var id = Text(target, "target", Text(target, "id"));
            if (id.Length == 0) continue;
            if (!_skillTargetRows.TryGetValue(id, out var row))
            {
                row = CreateSkillTargetRow(id);
                _skillTargetRows[id] = row;
                _skillRows.Children.Add(row.Container);
            }
            row.Separator.Visibility = _skillRows.Children.IndexOf(row.Container) == 0 ? Visibility.Collapsed : Visibility.Visible;
            row.Update(target);
        }
        _updatingSkillRows = false;
        var result = Property(_skills, "result");
        var installed = Items(result, "installed").ToList();
        var failed = Items(result, "failed").ToList();
        _skillResult!.Text = Bool(_skills, "busy") ? L("正在更新所选 Skills…") : installed.Count + failed.Count > 0
            ? L("已同步 {0} 个，失败 {1} 个。", installed.Count, failed.Count) : string.Empty;
        if (targets.Count == 0) _skillResult.Text = L("后端没有返回可管理的 Skill 目标。");
        _skillResult.Visibility = _skillResult.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        RefreshSkillSelection();
        RefreshActionButtons();
    }

    private SkillTargetRow CreateSkillTargetRow(string id)
    {
        var current = default(JsonElement);
        var name = Body(string.Empty);
        name.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        var status = Secondary(string.Empty);
        var toggle = CompactSwitch(id, _selectedSkillTargets.Contains(id));
        toggle.Toggled += (_, _) =>
        {
            if (_updatingSkillRows) return;
            if (toggle.IsOn) _selectedSkillTargets.Add(id); else _selectedSkillTargets.Remove(id);
            RefreshSkillSelection();
            RefreshActionButtons();
        };
        var details = DetailsButton(L("文件详情"), () =>
        {
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(KeyValue(L("状态"), SkillStatusLabel(Text(current, "status"))));
            panel.Children.Add(KeyValue(L("路径"), Text(current, "path")));
            foreach (var (key, heading) in new[] { ("missing_files", L("待安装的文件")), ("content_stale_files", L("内容不同的文件")) })
            {
                var files = Items(current, key).Select(item => item.GetString()).Where(file => !string.IsNullOrWhiteSpace(file)).ToList();
                if (files.Count > 0) panel.Children.Add(SettingsSection(heading, string.Empty, DataText(string.Join("\n", files))));
            }
            if (Bool(current, "invocation_changed")) panel.Children.Add(Secondary(L("本机 CLI 调用信息需要刷新。")));
            panel.Children.Add(Secondary(L("这里只查看文件状态。点击“更新所选 Skills”后才会安装或更新文件。")));
            foreach (var legacy in Items(current, "legacy_locations")) panel.Children.Add(DataText(L("历史副本，保留：{0}", Text(legacy, "path"))));
            if (Text(current, "error").Length > 0) panel.Children.Add(Body(Text(current, "error")));
            var result = Property(_skills, "result");
            foreach (var receipt in Items(result, "installed").Where(item => Text(item, "target") == id))
                if (Text(receipt, "backup").Length > 0) panel.Children.Add(DataText(L("备份：{0}", Text(receipt, "backup"))));
            foreach (var failure in Items(result, "failed").Where(item => Text(item, "target") == id)) panel.Children.Add(Body(Text(failure, "error")));
            return ShowDetailsAsync(Text(current, "label", id), panel);
        });
        var line = new Grid { ColumnSpacing = 16 };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = new StackPanel { Spacing = 4, Children = { name, status } };
        line.Children.Add(label);
        details.MinHeight = 28;
        details.Padding = new Thickness(8, 4, 8, 4);
        details.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(details, 1);
        line.Children.Add(details);
        Grid.SetColumn(toggle, 2);
        line.Children.Add(toggle);
        var separator = Divider();
        var container = new StackPanel { Spacing = 8, Children = { separator, line } };
        return new SkillTargetRow(container, separator, target =>
        {
            current = target.Clone();
            name.Text = Text(target, "label", id);
            status.Text = Bool(target, "invocation_changed") && !Items(target, "content_stale_files").Any() && !Items(target, "missing_files").Any()
                ? L("调用信息需刷新") : SkillStatusLabel(Text(target, "status"));
            toggle.IsOn = _selectedSkillTargets.Contains(id);
            toggle.IsEnabled = !Bool(_skills, "busy");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle, name.Text);
        });
    }

    private void RefreshSkillSelection()
    {
        if (_skillSelectionSummary is not null) _skillSelectionSummary.Text = L("已选择 {0} 个 Agent", _selectedSkillTargets.Count);
    }

    private sealed record SkillTargetRow(StackPanel Container, Border Separator, Action<JsonElement> Update);
}
