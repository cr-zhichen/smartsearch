import AppKit
import SwiftUI

// THESIS: Show the next useful task first; move configuration and diagnostics to their own destinations.
// OWN-WORLD: Native macOS chrome, white semantic content surfaces, system type and restrained separators.
// STORY: Configure a provider, run a tool, read its result, and inspect real activity without losing context.
// FIRST VIEWPORT: Sidebar navigation plus a focused detail area with one primary action and concise status.
// FORM: User-pinned Apple desktop conventions and Codex Tweaks reference; no random concept selection.
// FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, and DESIGN.md
struct ContentView: View {
    @ObservedObject var model: AppModel
    @State private var configurationSelection: ConfigurationRoute?

    var body: some View {
        NavigationSplitView {
            List(selection: Binding<Destination?>(
                get: { model.selectedDestination },
                set: { if let destination = $0 { model.selectedDestination = destination } }
            )) {
                ForEach(Destination.allCases) { destination in
                    Label(destination.title, systemImage: destination.symbol)
                        .tag(destination)
                }
            }
            .listStyle(.sidebar)
            .id(model.languagePreference)
            .navigationTitle("Smart Search")
            .navigationSplitViewColumnWidth(220)
            .safeAreaInset(edge: .bottom, spacing: 0) {
                HStack {
                    ConnectionIndicator(state: model.connection)
                        .font(.caption)
                    Spacer()
                    if model.connection == .failed || model.connection == .disconnected {
                        Button {
                            Task { await model.reconnect() }
                        } label: {
                            Label(L("重新连接"), systemImage: "arrow.clockwise")
                                .labelStyle(.iconOnly)
                        }
                        .buttonStyle(.borderless)
                        .help(L("重新连接"))
                    }
                }
                .padding(16)
            }
        } detail: {
            VStack(spacing: 0) {
                if let error = model.errorMessage {
                    MessageBanner(message: error, symbol: "exclamationmark.triangle.fill", tint: .red) {
                        model.errorMessage = nil
                    }
                }
                if let notice = model.noticeMessage {
                    MessageBanner(message: notice, symbol: "checkmark.circle.fill", tint: .green) {
                        model.noticeMessage = nil
                    }
                }
                destinationView
                    // Refresh translated controls without replacing the native navigation container.
                    .id(model.languagePreference)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .background(DesktopAppearance.contentBackground)
            .navigationTitle(model.selectedDestination.title)
            .toolbar {
                ToolbarItem(placement: .primaryAction) {
                    Button {
                        Task { await model.refreshState() }
                    } label: {
                        if model.isBusy.contains("state") {
                            ProgressView().controlSize(.small)
                        } else {
                            Label(L("刷新状态"), systemImage: "arrow.clockwise")
                        }
                    }
                    .id(model.languagePreference)
                    .help(L("刷新状态"))
                    .disabled(model.connection != .ready || model.configOperationBusy)
                }
            }
        }
        .navigationSplitViewStyle(.balanced)
        .groupBoxStyle(DesktopGroupBoxStyle())
        .toggleStyle(.switch)
        .disclosureGroupStyle(WholeRowDisclosureStyle())
        .environment(\.locale, model.interfaceLocale)
        .onChange(of: model.selectedDestination) { destination in
            Task { await model.enter(destination) }
        }
        .task {
            await model.enter(model.selectedDestination)
        }
    }

    @ViewBuilder
    private var destinationView: some View {
        switch model.selectedDestination {
        case .overview: OverviewView(model: model)
        case .providers: ProvidersView(model: model, selection: $configurationSelection)
        case .search: SearchResearchView(model: model)
        case .activity: ActivityView(model: model)
        case .integration: IntegrationView(model: model)
        case .settings: SettingsAboutView(model: model)
        }
    }
}

private struct MessageBanner: View {
    let message: String
    let symbol: String
    let tint: Color
    let dismiss: () -> Void

    var body: some View {
        HStack(spacing: 8) {
            Image(systemName: symbol).foregroundStyle(tint)
            Text(message).fixedSize(horizontal: false, vertical: true)
            Spacer(minLength: 8)
            Button(action: dismiss) { Image(systemName: "xmark") }
                .buttonStyle(.borderless)
                .accessibilityLabel(L("关闭提示"))
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 10)
        .background(tint.opacity(0.10))
    }
}

private struct ConnectionIndicator: View {
    let state: AppModel.ConnectionState

    var body: some View {
        Label {
            Text(state.title).foregroundStyle(.secondary)
        } icon: {
            Image(systemName: state.symbol).foregroundStyle(tint)
        }
            .accessibilityLabel(L("后端状态：{0}", "\(state.title)"))
    }

    private var tint: Color {
        switch state {
        case .ready: return .green
        case .connecting: return .orange
        case .failed: return .red
        case .disconnected: return .secondary
        }
    }
}

private struct BackendUnavailableView: View {
    @ObservedObject var model: AppModel

    var body: some View {
        VStack(spacing: 16) {
            if model.connection == .connecting {
                ProgressView().controlSize(.large)
            } else {
                Image(systemName: "bolt.horizontal.circle")
                    .font(.system(size: 42))
                    .foregroundStyle(.secondary)
            }
            Text(model.connection == .failed ? L("后端目前不可用") : L("正在连接本机后端"))
                .font(.title2.weight(.semibold))
            Text(L("连接后会读取当前配置、工具目录和活动记录。"))
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
            if model.connection != .connecting {
                Button(L("重新连接")) { Task { await model.reconnect() } }
                    .buttonStyle(.borderedProminent)
                    .disabled(model.isBusy.contains("connect"))
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(40)
    }
}

private struct OverviewView: View {
    @ObservedObject var model: AppModel
    @State private var showingDetails = false

    var body: some View {
        if let state = model.state {
            DesktopPage(L("概览"), subtitle: L("配置、运行与结果，都在这台电脑上管理。")) {
                DesktopPanel {
                    HStack(spacing: 12) {
                        Image(systemName: state.minimumProfileOK == true ? "checkmark.circle" : "slider.horizontal.3")
                            .font(.system(size: 23, weight: .semibold))
                            .foregroundStyle(state.minimumProfileOK == true ? Color.green : Color.accentColor)
                            .frame(width: 52, height: 52)
                            .background((state.minimumProfileOK == true ? Color.green : Color.accentColor).opacity(0.14), in: Circle())
                            .accessibilityHidden(true)
                        VStack(alignment: .leading, spacing: 4) {
                            Text(readinessTitle(state)).font(.title3.weight(.semibold))
                            Text(readinessDetail(state)).foregroundStyle(.secondary)
                                .fixedSize(horizontal: false, vertical: true)
                        }
                        Spacer(minLength: 16)
                        Button(state.minimumProfileOK == true ? L("开始搜索") : L("去配置")) {
                            model.selectedDestination = state.minimumProfileOK == true ? .search : .providers
                        }
                        .buttonStyle(.borderedProminent)
                        .fixedSize()
                    }
                    Text(L("配置齐全表示可以发起请求；连接是否正常，以你主动测试的结果为准。"))
                        .font(.caption).foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }

                if let details = state.capabilityStatus?.objectValue, !details.isEmpty {
                    let primary = ["main_search", "docs_search", "web_fetch"].filter { details[$0] != nil }
                    let additional = details.keys.filter { !primary.contains($0) }.sorted()
                    GroupBox(L("当前能力")) {
                        ForEach(primary, id: \.self) { key in
                            CapabilityStatusRow(capability: key, status: details[key] ?? .object([:]))
                            if key != primary.last { Divider() }
                        }
                        if !additional.isEmpty {
                            DisclosureGroup(L("更多能力")) {
                                VStack(alignment: .leading, spacing: 12) {
                                    ForEach(additional, id: \.self) { key in
                                        CapabilityStatusRow(capability: key, status: details[key] ?? .object([:]))
                                    }
                                }
                                .padding(.top, 12)
                            }
                        }
                    }
                }

                HStack {
                    Text(L("管理连接信息和检索服务。"))
                        .foregroundStyle(.secondary)
                    Spacer()
                    Button(L("配置服务商")) { model.selectedDestination = .providers }
                    Button(L("配置详情…")) { showingDetails = true }
                }
            }
            .sheet(isPresented: $showingDetails) {
                DetailSheet(L("配置与路由详情")) {
                    KeyValueLine(label: L("配置文件"), value: state.configPath ?? L("后端未提供"))
                    KeyValueLine(label: L("配置目录"), value: state.configDirectory ?? L("后端未提供"))
                    KeyValueLine(label: L("内置引擎"), value: state.version ?? L("后端未提供"))
                    KeyValueLine(label: L("协议 generation"), value: state.generation ?? L("后端未提供"))
                    KeyValueLine(label: L("最后读取"), value: model.lastStateRefresh?.formatted(date: .abbreviated, time: .standard) ?? L("尚未读取"))
                    ForEach(state.capabilityChains.keys.sorted(), id: \.self) { key in
                        KeyValueLine(label: capabilityName(key), value: state.capabilityChains[key, default: []].joined(separator: " → "))
                    }
                    Button(L("选择配置目录")) {
                        showingDetails = false
                        model.selectedDestination = .settings
                    }
                }
            }
        } else {
            BackendUnavailableView(model: model)
        }
    }

    private func readinessDetail(_ state: DesktopState) -> String {
        if state.minimumProfileOK == true { return L("主搜索、文档检索和网页抓取已满足使用条件。") }
        if state.minimumProfileOK == nil { return L("后端尚未报告基础配置状态。") }
        if state.minimumMissing.isEmpty { return L("后端未列出缺失能力。请在服务商页查看当前字段。") }
        return L("还需要配置：{0}。每类任选一个服务商即可。", state.minimumMissing.map(capabilityName).joined(separator: "、"))
    }

    private func readinessTitle(_ state: DesktopState) -> String {
        switch state.minimumProfileOK {
        case true: return L("可以开始搜索了")
        case false: return L("先完成基础配置")
        case nil: return L("查看配置状态")
        }
    }
}

private enum ConfigurationRoute: Hashable {
    case provider(String)
    case section(String)
    case researchSources
    case routing
}

private struct ProvidersView: View {
    @ObservedObject var model: AppModel
    @Binding var selection: ConfigurationRoute?
    @State private var filter = ""

    var body: some View {
        if let state = model.state {
            let providers = groups(state)
            VStack(spacing: 0) {
                DesktopSplitView("providers") {
                    VStack(spacing: 0) {
                        TextField(L("查找服务商"), text: $filter)
                            .textFieldStyle(.roundedBorder).padding(12)
                        List(selection: $selection) {
                            if state.fields.contains(where: { $0.key == "SMART_SEARCH_INTENT_ROUTER" }) {
                                Text(L("意图路由"))
                                    .fontWeight(.medium)
                                    .padding(.vertical, 5)
                                    .tag(ConfigurationRoute.section("routing"))
                                    .listRowSeparator(.hidden)
                            }
                            ForEach(providerCategories(providers), id: \.self) { capability in
                                Section(capabilityName(capability)) {
                                    ForEach(providers.filter { ($0.primaryCapability ?? "other") == capability }) { group in
                                        providerRow(group, state: state)
                                            .tag(ConfigurationRoute.provider(group.id))
                                            .listRowSeparator(.hidden)
                                    }
                                }
                            }
                            Section(L("高级配置")) {
                                if !researchSourceFields(state).isEmpty && matches(L("研究数据源")) {
                                    Text(L("研究数据源")).padding(.vertical, 5)
                                        .tag(ConfigurationRoute.researchSources)
                                }
                                ForEach(advancedSectionIDs(state), id: \.self) { id in
                                    Text(state.sections.first { $0.id == id }?.label ?? id)
                                        .padding(.vertical, 5)
                                        .tag(ConfigurationRoute.section(id))
                                }
                                if matches(L("冷却与路由详情")) {
                                    Text(L("冷却与路由详情")).padding(.vertical, 5)
                                        .tag(ConfigurationRoute.routing)
                                }
                            }
                        }
                        .listStyle(.inset)
                        .scrollContentBackground(.hidden)
                        if providers.isEmpty && !filter.isEmpty {
                            Text(L("没有匹配的服务商")).font(.caption)
                                .foregroundStyle(.secondary).padding(12)
                        }
                    }
                } detail: {
                    configurationDetail(state)
                }
                ConfigActions(model: model)
            }
            .onAppear { reconcileSelection(state) }
            .onChange(of: availableRoutes(state)) { _ in reconcileSelection(state) }
        } else {
            BackendUnavailableView(model: model)
        }
    }

    private func providerRow(_ group: ProviderFieldGroup, state: DesktopState) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack(spacing: 6) {
                Text(group.id).fontWeight(.medium)
                if group.fields.contains(where: { model.configDraft[$0.key] != nil || model.clearSecretKeys.contains($0.key) }) {
                    Image(systemName: "pencil.circle")
                        .foregroundStyle(.orange).help(L("有未保存修改"))
                        .accessibilityLabel(L("有未保存修改"))
                }
            }
            Text(providerIsConfigured(group, state: state) ? L("已配置") : L("未配置"))
                .font(.caption).foregroundStyle(.secondary)
        }
        .padding(.vertical, 5)
    }

    @ViewBuilder
    private func configurationDetail(_ state: DesktopState) -> some View {
        switch selection {
        case .provider(let id):
            if let group = state.providerGroups.first(where: { $0.id == id }) {
                ConfigurationEditor(model: model, title: id, subtitle: providerPurpose(group),
                                    fields: group.fields, section: id)
                    .id(ConfigurationRoute.provider(id))
            }
        case .section(let id):
            if id == "routing" {
                IntentRoutingEditor(model: model, state: state)
            } else {
                ConfigurationEditor(model: model, title: state.sections.first { $0.id == id }?.label ?? id,
                                    fields: state.fields.filter { $0.provider?.isEmpty != false && $0.section == id }, section: id)
                    .id(ConfigurationRoute.section(id))
            }
        case .researchSources:
            ConfigurationEditor(model: model, title: L("研究数据源"),
                                fields: researchSourceFields(state), section: "routing")
                .id(ConfigurationRoute.researchSources)
        case .routing:
            DesktopPage(L("冷却与路由详情"), subtitle: L("查看路由顺序和最近请求状态。"), padding: 16) {
                ProviderHealthView(health: state.providerHealth)
                ForEach(state.capabilityChains.keys.sorted(), id: \.self) { key in
                    KeyValueLine(label: capabilityName(key), value: state.capabilityChains[key, default: []].joined(separator: " → "))
                }
            }
        case nil:
            Text(L("从左侧选择服务商或配置项目。"))
                .foregroundStyle(.secondary).padding(24)
        }
    }

    private func availableRoutes(_ state: DesktopState) -> [ConfigurationRoute] {
        state.providerGroups.map { .provider($0.id) } + sectionIDs(state).map { .section($0) }
            + (researchSourceFields(state).isEmpty ? [] : [.researchSources]) + [.routing]
    }

    private func reconcileSelection(_ state: DesktopState) {
        if let selection, availableRoutes(state).contains(selection) { return }
        // Filtering does not switch away from the field currently being edited.
        if state.fields.contains(where: { $0.key == "SMART_SEARCH_INTENT_ROUTER" }) {
            selection = .section("routing")
        } else {
            selection = groups(state).first.map { .provider($0.id) }
        }
    }

    private func sectionIDs(_ state: DesktopState) -> [String] {
        let sections = Set(state.fields.filter { $0.provider?.isEmpty != false }.map(\.section))
        return orderedSectionIDs(state: state, present: sections)
    }

    private func advancedSectionIDs(_ state: DesktopState) -> [String] {
        sectionIDs(state).filter { id in
            id != "routing" && (matches(id) || matches(state.sections.first { $0.id == id }?.label ?? id))
        }
    }

    private func researchSourceFields(_ state: DesktopState) -> [ConfigField] {
        state.fields.filter { $0.section == "routing" && $0.key.hasPrefix("SMART_SEARCH_RESEARCH_") }
    }

    private func groups(_ state: DesktopState) -> [ProviderFieldGroup] {
        state.providerGroups
            .filter { group in
                matches(group.id) || matches(providerPurpose(group)) || group.capabilities.contains { matches($0) }
            }
            .sorted {
                let left = providerIsConfigured($0, state: state)
                let right = providerIsConfigured($1, state: state)
                return left == right ? $0.id < $1.id : left
            }
    }

    private func providerCategories(_ groups: [ProviderFieldGroup]) -> [String] {
        let order = ["main_search", "docs_search", "web_search", "web_fetch", "vertical_search", "site_map", "synthesis"]
        let present = Set(groups.map { $0.primaryCapability ?? "other" })
        return order.filter(present.contains) + present.subtracting(order).sorted()
    }

    private func matches(_ text: String) -> Bool {
        filter.isEmpty || text.localizedCaseInsensitiveContains(filter)
    }
}

private struct IntentRoutingEditor: View {
    @ObservedObject var model: AppModel
    let state: DesktopState

    private var fields: [ConfigField] { state.fields.filter { $0.section == "routing" } }
    private var modeField: ConfigField? { fields.first { $0.key == "SMART_SEARCH_INTENT_ROUTER" } }
    private var mode: String { modeField.map(selectedValue) ?? "" }
    private let resultProcessingKeys: Set<String> = [
        "SMART_SEARCH_JEV_FILTER_RESULTS", "SMART_SEARCH_JEV_FILTER_THRESHOLD", "SMART_SEARCH_JEV_SYNTHESIZE",
    ]
    private var filteringEnabled: Bool {
        guard let field = fields.first(where: { $0.key == "SMART_SEARCH_JEV_FILTER_RESULTS" }) else { return false }
        return configurationBooleanValue(selectedValue(field))
    }

    var body: some View {
        DesktopPage(L("意图路由"), subtitle: L("先选择路由方式，再填写该模式使用的参数。"), padding: 20) {
            if let modeField {
                DesktopPanel {
                    ConfigFieldEditor(model: model, state: state, field: modeField)
                    Text(modeDescription).font(.callout).foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }
            }
            switch mode {
            case "hybrid":
                fieldPanel(L("向量模型"), fields: fields.filter { $0.key.hasPrefix("INTENT_EMBEDDING_") })
                fieldPanel(L("分类模型"), fields: fields.filter { $0.key.hasPrefix("INTENT_CLASSIFIER_") })
                fieldPanel(L("请求设置"), fields: fields.filter { $0.key == "INTENT_ROUTER_TIMEOUT_SECONDS" })
            case "jev":
                fieldPanel(L("JEV 连接"), fields: fields.filter { $0.key.hasPrefix("TYPESAFE_") })
                fieldPanel(L("检索与判断"), fields: fields.filter {
                    $0.key.hasPrefix("SMART_SEARCH_JEV_") && !resultProcessingKeys.contains($0.key)
                })
                fieldPanel(L("结果处理"), fields: fields.filter {
                    resultProcessingKeys.contains($0.key)
                        && ($0.key != "SMART_SEARCH_JEV_FILTER_THRESHOLD" || filteringEnabled)
                })
            default: EmptyView()
            }
        }
    }

    private var modeDescription: String {
        switch mode {
        case "hybrid": return L("以规则为基础，可按需配置向量模型和分类模型增强判断。未配置的模型不会被调用。")
        case "jev": return L("使用 JEV 选择检索渠道并判断证据是否充分，需要单独配置 TypeSafe 凭据。")
        case "rules": return L("仅使用本地规则判断意图，无需填写模型接口或密钥。")
        case "off": return L("关闭自动意图路由，无需填写路由参数。")
        default: return L("请选择一种路由模式。")
        }
    }

    private func selectedValue(_ field: ConfigField) -> String {
        (model.configDraft[field.key] ?? state.effectiveValue(for: field))
            .trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
    }

    @ViewBuilder
    private func fieldPanel(_ title: String, fields: [ConfigField]) -> some View {
        if !fields.isEmpty {
            DesktopPanel(title) {
                ForEach(fields) { field in
                    ConfigFieldEditor(model: model, state: state, field: field)
                    if field.id != fields.last?.id { Divider() }
                }
            }
        }
    }
}

private func configurationBooleanValue(_ value: String) -> Bool {
    ["true", "1", "yes", "on"].contains(value.lowercased())
}

private func configurationChoiceLabel(_ choice: String, for field: ConfigField) -> String {
    if field.key == "SMART_SEARCH_INTENT_ROUTER" {
        switch choice {
        case "hybrid": return L("混合路由")
        case "jev": return L("JEV 语义路由")
        case "rules": return L("规则路由")
        case "off": return L("关闭路由")
        default: return choice
        }
    }
    if field.key == "SMART_SEARCH_JEV_SYNTHESIZE" {
        switch choice {
        case "false": return L("直接返回证据")
        case "auto": return L("按需汇总")
        case "true": return L("始终汇总")
        default: return choice
        }
    }
    return choice
}

private struct ConfigActions: View {
    @ObservedObject var model: AppModel
    @State private var showingPreview = false
    private var count: Int { model.configDraft.count + model.clearSecretKeys.count }
    var body: some View {
        VStack(spacing: 0) {
            Divider()
            HStack(spacing: 12) {
                Text(count == 0 ? L("所有修改已保存") : L("未保存修改：{0} 项", "\(count)"))
                    .font(.caption).foregroundStyle(.secondary)
                Spacer()
                Button(L("放弃修改")) { model.resetConfigDraft() }.disabled(count == 0 || model.configOperationBusy)
                Button {
                    Task {
                        await model.previewConfig()
                        showingPreview = model.configPreview != nil
                    }
                } label: {
                    BusyLabel(text: L("检查配置"), busyText: L("检查中…"), busy: model.isBusy.contains("preview"))
                }
                .disabled(model.connection != .ready || model.configOperationBusy)
                Button { Task { await model.saveConfig() } } label: {
                    BusyLabel(text: L("保存更改"), busyText: L("保存中…"), busy: model.isBusy.contains("save"))
                }
                .buttonStyle(.borderedProminent)
                .disabled(count == 0 || model.connection != .ready || model.configOperationBusy)
            }.padding(16)
        }.background(DesktopAppearance.contentBackground)
        .sheet(isPresented: $showingPreview) {
            DetailSheet(L("配置检查")) {
                if let preview = model.configPreview { ConfigPreviewView(preview: preview) }
                Text(L("检查使用当前草稿，不会保存或发起服务商请求。"))
                    .foregroundStyle(.secondary)
            }
        }
    }
}

private struct ConfigurationEditor: View {
    @ObservedObject var model: AppModel
    let title: String
    var subtitle: String = L("修改先保留为草稿；测试使用当前填写的值。")
    let fields: [ConfigField]
    let section: String

    var body: some View {
        if let state = model.state {
            DesktopPage(title, subtitle: subtitle, padding: 16) {
                ProviderSection(model: model, state: state, section: section, fields: fields)
            }
        }
    }
}

private func capabilityName(_ value: String) -> String {
    ["main_search": L("主搜索"), "docs_search": L("文档检索"), "web_fetch": L("网页抓取"),
     "web_search": L("网页搜索"), "vertical_search": L("垂直检索"), "site_map": L("站点地图"),
     "synthesis": L("结果汇总"), "other": L("其他能力")][value] ?? value
}

private func providerPurpose(_ group: ProviderFieldGroup) -> String {
    let capabilities = group.capabilities.map(capabilityName).joined(separator: L("、"))
    let strengths = group.strengths.map { L($0) }.joined(separator: L("、"))
    var sentences: [String] = []
    if !capabilities.isEmpty {
        let purpose = strengths.isEmpty
            ? L("用于{0}。", capabilities)
            : L("用于{0}，侧重{1}。", capabilities, strengths)
        sentences.append(purpose)
    } else if let help = group.fields.first(where: { !$0.help.isEmpty })?.help {
        sentences.append(help)
    }
    if group.isExperimental { sentences.append(L("实验性能力。")) }
    if group.isExplicitOnly {
        sentences.append(L("仅在明确指定时调用。"))
    } else if group.isRoutingDisabled {
        sentences.append(L("不参与自动路由。"))
    }
    return sentences.joined(separator: " ")
}

/// Section ids in backend order, with anything the backend did not describe
/// appended alphabetically so a new section never vanishes from the page.
private func orderedSectionIDs(state: DesktopState, present: Set<String>) -> [String] {
    var ordered = state.sections.map(\.id).filter(present.contains)
    let described = Set(ordered)
    ordered.append(contentsOf: present.subtracting(described).sorted())
    return ordered
}

/// A button label that turns into a spinner while its operation runs. Without it
/// a 20-second probe looks identical to a click that did nothing.
private struct BusyLabel: View {
    let text: String
    let busyText: String
    let busy: Bool

    var body: some View {
        if busy {
            HStack(spacing: 6) {
                ProgressView().controlSize(.small)
                Text(busyText)
            }
        } else {
            Text(text)
        }
    }
}

private struct ConfigPreviewView: View {
    let preview: JSONValue

    var body: some View {
        GroupBox(L("配置检查")) {
            VStack(alignment: .leading, spacing: 6) {
                if preview.boolValue == false || preview["ok"]?.boolValue == false {
                    Label(L("这样还不够用，尚未保存。"), systemImage: "xmark.circle.fill")
                        .foregroundStyle(.red)
                } else if preview["minimum_profile_ok"]?.boolValue == true {
                    Label(L("这样配就够用了。"), systemImage: "checkmark.circle.fill")
                        .foregroundStyle(.green)
                } else {
                    Text(L("检查已完成；请根据还缺的能力决定是否保存。"))
                        .foregroundStyle(.secondary)
                }
                ForEach(preview["missing"]?.arrayValue?.map(\.displayString) ?? [], id: \.self) { item in
                    Text(item).foregroundStyle(.secondary)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
        }
    }
}

private struct CapabilityStatusRow: View {
    let capability: String
    let status: JSONValue

    private var configuredProviders: [String] {
        status["configured"]?.arrayValue?.map(\.displayString).filter { !$0.isEmpty } ?? []
    }

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: 12) {
            VStack(alignment: .leading, spacing: 3) {
                Text(capabilityTitle).fontWeight(.medium)
                Text(configuredProviders.isEmpty ? L("没有已配置的服务商") : L("已配置：{0}", "\(configuredProviders.joined(separator: "、"))"))
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            Spacer()
            Label {
                Text(configurationLabel).font(.callout)
            } icon: {
                Image(systemName: configurationSymbol).foregroundStyle(configurationColor)
            }
            if status["experimental"]?.boolValue == true {
                Text(L("实验性")).font(.caption).foregroundStyle(.orange)
            }
        }
        .padding(.vertical, 3)
    }

    private var capabilityTitle: String {
        switch capability {
        case "main_search": return L("主搜索")
        case "web_search": return L("网页搜索")
        case "docs_search": return L("文档检索")
        case "web_fetch": return L("网页抓取")
        case "vertical_search": return L("垂直检索")
        default: return capability
        }
    }

    private var configurationLabel: String {
        switch status["ok"]?.boolValue {
        case .some(true): return L("配置条件已满足")
        case .some(false): return L("缺少配置")
        case nil: return L("状态未报告")
        }
    }

    private var configurationSymbol: String {
        switch status["ok"]?.boolValue {
        case .some(true): return "checkmark.circle"
        case .some(false): return "exclamationmark.circle"
        case nil: return "questionmark.circle"
        }
    }

    private var configurationColor: Color {
        switch status["ok"]?.boolValue {
        case .some(true): return .green
        case .some(false): return .orange
        case nil: return .secondary
        }
    }
}

private struct ProviderHealthView: View {
    let health: JSONValue?

    var body: some View {
        GroupBox(L("服务商冷却状态")) {
            VStack(alignment: .leading, spacing: 9) {
                Text(L("冷却仅影响本机是否暂时跳过重试。无冷却不等于服务商刚刚联网成功。"))
                    .font(.caption)
                    .foregroundStyle(.secondary)
                if let providers = health?["providers"]?.arrayValue {
                    if providers.isEmpty {
                        Text(L("后端没有需要显示的冷却记录。"))
                            .foregroundStyle(.secondary)
                    } else {
                        ForEach(providers.indices, id: \.self) { index in
                            ProviderHealthRow(health: providers[index])
                        }
                    }
                } else {
                    Text(L("后端未报告冷却状态。"))
                        .foregroundStyle(.secondary)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
        }
    }
}

private struct ProviderHealthRow: View {
    let health: JSONValue

    private var state: String { health["state"]?.stringValue ?? "unknown" }
    private var remainingSeconds: Double { health["cooldown_remaining_seconds"]?.numberValue ?? 0 }

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack(alignment: .firstTextBaseline) {
                Text(health["provider"]?.displayString ?? L("未知服务商")).fontWeight(.medium)
                Spacer()
                if state == "cooldown" {
                    Label(L("冷却中（剩余 {0}）", "\(cooldownText)"), systemImage: "pause.circle")
                        .foregroundStyle(.orange)
                } else {
                    Label(L("无冷却"), systemImage: "minus.circle")
                        .foregroundStyle(.secondary)
                }
            }
            if health["configured"]?.boolValue == false {
                Text(L("当前配置未包含此服务商。"))
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            if state == "closed" {
                Text(L("无冷却只表示当前不会因本机冷却被跳过，不代表联网验证成功。"))
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            if let errorType = health["error_type"]?.stringValue, !errorType.isEmpty {
                Text(L("最近一次请求异常，可主动测试确认。"))
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            if let message = health["error"]?.stringValue, !message.isEmpty {
                Text(message).font(.caption).foregroundStyle(.secondary)
            }
        }
        .padding(.vertical, 3)
    }

    private var cooldownText: String {
        if remainingSeconds >= 60 { return L("{0} 分钟", "\(Int((remainingSeconds / 60).rounded(.up)))") }
        return L("{0} 秒", "\(Int(remainingSeconds.rounded(.up)))")
    }
}

private struct ProviderDraftCheckRow: View {
    let provider: String
    let check: JSONValue

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack(alignment: .firstTextBaseline) {
                Text(provider).fontWeight(.medium)
                Spacer()
                Text(statusLabel).foregroundStyle(statusColor)
            }
            Text(L("时间：{0} · 来源：{1} · 范围：{2}", "\(checkedAtText)", "\(source)", "\(scope)"))
                .font(.caption)
                .foregroundStyle(.secondary)
            if let probe = check["probe"]?.stringValue, !probe.isEmpty {
                Text(L("方式：{0}", "\(["live": L("真实请求"), "main": L("主搜索请求"), "presence": L("仅检查已填写"), "shared": L("共用凭据")][probe] ?? L("本机检查"))")).font(.caption).foregroundStyle(.secondary)
            }
            if let message = check["message"]?.stringValue, !message.isEmpty {
                DisclosureGroup(L("技术详情")) { Text(message).font(.system(.caption, design: .monospaced)).textSelection(.enabled) }
            }
        }
        .padding(.vertical, 3)
    }

    private var status: String { check["status"]?.stringValue ?? "unknown" }
    private var source: String { check["source"]?.stringValue == "app" ? L("本次 App 会话") : L("本机测试") }
    private var scope: String { check["scope"]?.stringValue == "draft" ? L("未保存的修改") : L("当前有效配置") }

    private var statusLabel: String {
        switch status {
        case "ok": return L("测试通过")
        case "cancelled": return L("测试已取消")
        case "not_configured": return L("未配置")
        case "timeout": return L("测试超时")
        case "warning": return L("需要确认")
        case "configured": return L("已填写，未验证")
        default: return L("测试未通过")
        }
    }

    private var statusColor: Color {
        switch status {
        case "ok": return .green
        case "cancelled": return .secondary
        case "not_configured", "timeout", "warning", "configured": return .orange
        default: return .red
        }
    }

    private var checkedAtText: String {
        guard let seconds = check["checked_at"]?.numberValue else { return L("后端未提供") }
        return Date(timeIntervalSince1970: seconds).formatted(date: .abbreviated, time: .standard)
    }
}

private func providerIsConfigured(_ group: ProviderFieldGroup, state: DesktopState) -> Bool {
    group.fields.contains { $0.isSecret && state.hasSecretValue(for: $0) }
}

private struct ProviderSection: View {
    @ObservedObject var model: AppModel
    let state: DesktopState
    let section: String
    let fields: [ConfigField]

    private var provider: String? {
        let providers = Set(fields.compactMap(\.provider).filter { !$0.isEmpty })
        return providers.count == 1 ? providers.first : nil
    }

    private var testKey: String { "test:" + (provider ?? section) }
    private var connectionFields: [ConfigField] { fields.filter { !$0.isAdvanced } }
    private var advancedFields: [ConfigField] { fields.filter(\.isAdvanced) }

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            if !connectionFields.isEmpty && !advancedFields.isEmpty {
                Text(L("连接设置")).font(.headline)
                fieldEditors(connectionFields)
                Text(L("高级参数")).font(.headline).padding(.top, 10)
                fieldEditors(advancedFields)
            } else {
                fieldEditors(fields)
            }
            if let provider, let check = state.providerChecks?[provider] {
                ProviderDraftCheckRow(provider: provider, check: check)
            }
            HStack {
                if let provider {
                    Button {
                        Task { await model.testProvider(provider) }
                    } label: {
                        if model.isBusy.contains(testKey) {
                            HStack(spacing: 6) {
                                ProgressView().controlSize(.small)
                                Text(model.state?.raw["probe_kinds"]?[provider]?.stringValue == "presence" ? L("检查中…") : L("测试中…"))
                            }
                        } else {
                            Text(model.providerTestLabel(provider))
                        }
                    }
                    .disabled(model.connection != .ready || model.isBusy.contains(testKey))
                }
                Spacer()
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private func fieldEditors(_ fields: [ConfigField]) -> some View {
        ForEach(fields) { field in
            ConfigFieldEditor(model: model, state: state, field: field)
            if field.id != fields.last?.id { Divider() }
        }
    }
}

private struct ConfigFieldEditor: View {
    @ObservedObject var model: AppModel
    let state: DesktopState
    let field: ConfigField
    @State private var showingInfo = false

    private var secretPrompt: String {
        state.hasSecretValue(for: field) && !model.clearSecretKeys.contains(field.key)
            ? "••••••••" : L("输入 API Key")
    }

    private var readOnlyValue: String {
        let value = state.effectiveValue(for: field)
        if field.isSecret { return state.hasSecretValue(for: field) ? "••••••••" : L("未配置") }
        if value.isEmpty { return L("后端未提供有效值") }
        return configurationChoiceLabel(value, for: field)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 7) {
            HStack(alignment: .firstTextBaseline) {
                Text(field.label).fontWeight(.medium)
                Spacer()
                Button { showingInfo = true } label: { Image(systemName: "info.circle") }
                    .buttonStyle(.borderless).help(L("字段说明"))
                    .popover(isPresented: $showingInfo) { fieldDetails.padding(20).frame(width: 360) }
            }
            Text(model.draftStatus(for: field)).font(.caption).foregroundStyle(.secondary)

            if model.isEnvironmentReadOnly(field) {
                Text(readOnlyValue)
                    .textSelection(.enabled)
            } else if field.isSecret {
                HStack {
                    // The mask is a prompt, never a draft value that could overwrite the key.
                    SecureField(field.label, text: model.draftBinding(for: field), prompt: Text(secretPrompt))
                        .accessibilityLabel(field.label)
                        .help(L("输入新值以替换；留空表示保持"))
                    if model.clearSecretKeys.contains(field.key) {
                        Button(L("保留")) { model.keepSecret(field) }
                    } else {
                        Button(L("清除 Key"), role: .destructive) { model.clearSecret(field) }
                    }
                }
            } else if !field.choices.isEmpty {
                Picker(field.label, selection: Binding(
                    get: { model.configDraft[field.key] ?? state.effectiveValue(for: field) },
                    set: { model.setDraft($0, for: field) })) {
                    if !field.choices.contains(state.effectiveValue(for: field)) {
                        Text(L("当前：{0}", state.effectiveValue(for: field).isEmpty ? L("未设置") : configurationChoiceLabel(state.effectiveValue(for: field), for: field)))
                            .tag(state.effectiveValue(for: field))
                    }
                    ForEach(field.choices, id: \.self) { choice in
                        Text(configurationChoiceLabel(choice, for: field)).tag(choice)
                    }
                }
                .labelsHidden()
            } else if field.kind == "bool" {
                Toggle(field.label, isOn: Binding(
                    get: { configurationBooleanValue(model.configDraft[field.key] ?? state.effectiveValue(for: field)) },
                    set: { model.setDraft($0 ? "true" : "false", for: field) }))
                    .labelsHidden()
            } else {
                TextField(state.effectiveValue(for: field).isEmpty ? field.placeholder : state.effectiveValue(for: field), text: model.draftBinding(for: field))
                    .accessibilityLabel(field.label)
            }

            if let keyURL = field.keyURL, let url = URL(string: keyURL) {
                Link(L("申请 Key"), destination: url).font(.caption)
            }
        }
        .disabled(model.isBusy.contains("save"))
        .accessibilityElement(children: .contain)
    }
    private var fieldDetails: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text(field.label).font(.headline)
            if !field.help.isEmpty { Text(field.help).font(.callout) }
            Text(field.key).font(.system(.caption, design: .monospaced)).textSelection(.enabled)
            KeyValueLine(label: L("来源"), value: state.statusLabels[state.source(for: field)] ?? L("未知来源"))
            Text(L("有效值：{0}", state.effectiveValue(for: field).isEmpty ? L("未设置") : state.effectiveValue(for: field)))
                .font(.caption).textSelection(.enabled)
            if state.savedValue(for: field) != state.effectiveValue(for: field) {
                Text(L("配置文件：{0}", state.savedValue(for: field).isEmpty ? L("未设置") : state.savedValue(for: field)))
                    .font(.caption).textSelection(.enabled)
            }
            if let docs = field.docsURL, let url = URL(string: docs) { Link(L("文档"), destination: url) }
        }
    }

}

private struct SearchResearchView: View {
    @ObservedObject var model: AppModel
    @State private var showOptions = false

    var body: some View {
        if let state = model.state {
            DesktopSplitView("search", leadingWidths: 280...360, initialLeadingWidth: 320, detailMinimumWidth: 360) {
                ScrollView {
                    VStack(alignment: .leading, spacing: 24) {
                        Text(L("输入")).font(.title2.weight(.semibold))
                        if state.commands.isEmpty {
                            Text(L("后端尚未提供可运行的工具目录。"))
                        } else {
                            VStack(alignment: .leading, spacing: 8) {
                                Picker(L("工具"), selection: Binding(get: { model.selectedCommandID ?? "" }, set: { model.selectCommand($0) })) {
                                    ForEach(state.commands) { command in
                                        Text(command.experimental ? L("{0}（实验性）", command.label) : command.label).tag(command.id)
                                    }
                                }
                                .labelsHidden().frame(maxWidth: .infinity, alignment: .leading)
                                if let command = model.selectedCommand {
                                    Text(command.description).font(.callout).foregroundStyle(.secondary)
                                        .fixedSize(horizontal: false, vertical: true)
                                }
                            }
                            if let command = model.selectedCommand {
                                if command.experimental {
                                    Label(L("实验性工具：只在明确选择后调用。"), systemImage: "flask")
                                        .font(.callout).foregroundStyle(.orange)
                                }
                                VStack(alignment: .leading, spacing: 16) {
                                    ForEach(command.fields.filter { !$0.isAdvanced }) { field in
                                        CommandFieldEditor(model: model, field: field)
                                    }
                                }
                                ViewThatFits(in: .horizontal) {
                                    HStack(spacing: 12) { requestActions(command) }
                                    VStack(alignment: .leading, spacing: 12) { requestActions(command) }
                                }
                            }
                        }
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(20)
                }
            } detail: {
                if let result = model.currentResult {
                    ScrollView {
                        ReadableResultView(result: result, command: model.currentResultCommand,
                                           copy: model.copyCurrentResult, export: model.exportCurrentResult)
                            .padding(24)
                    }
                } else {
                    VStack(spacing: 12) {
                        if model.isSearchRunning {
                            ProgressView().controlSize(.large)
                            Text(model.currentResultCommand ?? L("任务正在运行")).font(.headline)
                            Text(L("任务正在运行")).foregroundStyle(.secondary)
                            Button(L("查看活动")) { model.selectedDestination = .activity }
                        } else {
                            Image(systemName: "doc.text.magnifyingglass").font(.system(size: 36)).foregroundStyle(.secondary)
                            Text(L("结果会显示在这里")).font(.headline)
                            Text(L("先选择工具并运行一次请求。")).foregroundStyle(.secondary)
                        }
                    }
                    .multilineTextAlignment(.center).padding(24)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                }
            }
            .sheet(isPresented: $showOptions) {
                DetailSheet(L("搜索选项")) {
                    if let command = model.selectedCommand {
                        ForEach(command.fields.filter(\.isAdvanced)) { field in
                            CommandFieldEditor(model: model, field: field)
                        }
                    }
                }
            }
        } else { BackendUnavailableView(model: model) }
    }

    @ViewBuilder
    private func requestActions(_ command: CommandCatalogEntry) -> some View {
        Button { Task { await model.startSelectedCommand() } } label: {
            BusyLabel(text: L("开始 {0}", command.label), busyText: L("运行中…"), busy: model.isBusy.contains("run:\(command.id)"))
        }
        .buttonStyle(.borderedProminent)
        .keyboardShortcut(.return, modifiers: .command)
        .disabled(model.connection != .ready || model.isBusy.contains("run:\(command.id)"))
        if command.fields.contains(where: \.isAdvanced) {
            Button(L("搜索选项…")) { showOptions = true }
        }
    }
}

private struct CommandFieldEditor: View {
    @ObservedObject var model: AppModel
    let field: CommandField

    var body: some View {
        VStack(alignment: .leading, spacing: 5) {
            if field.isBoolean {
                Toggle(field.label, isOn: model.booleanBinding(for: field))
            } else if !field.choices.isEmpty {
                Picker(field.label, selection: model.commandBinding(for: field)) {
                    if !field.required { Text(L("未指定")).tag("") }
                    ForEach(field.choices, id: \.self) { choice in Text(choice).tag(choice) }
                }
            } else if field.acceptsMultipleValues {
                Text(field.label + (field.required ? L("（必填）") : ""))
                TextEditor(text: model.commandBinding(for: field))
                    .font(.body)
                    .frame(minHeight: 58)
                    .overlay(RoundedRectangle(cornerRadius: 5).stroke(.quaternary))
                Text(L("每行一个值。"))
                    .font(.caption)
                    .foregroundStyle(.secondary)
            } else {
                Text(field.label + (field.required ? L("（必填）") : ""))
                TextField(field.label, text: model.commandBinding(for: field), axis: .vertical)
                    .labelsHidden()
                    .lineLimit(field.name == "query" ? 3...8 : 1...6)
                    .textFieldStyle(.roundedBorder)
            }
            if !field.help.isEmpty { Text(field.help).font(.caption).foregroundStyle(.secondary) }
        }
    }
}

private struct ReadableResultView: View {
    let result: JSONValue
    let command: String?
    let copy: () -> Void
    let export: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text(command.map { L("结果：{0}", $0) } ?? L("结果")).font(.title2.weight(.semibold))
            ViewThatFits(in: .horizontal) {
                HStack { resultActions }.fixedSize(horizontal: true, vertical: false)
                VStack(alignment: .leading, spacing: 8) { resultActions }
            }
            Divider()
            if let text = result.readableText, !text.isEmpty {
                Text(text).textSelection(.enabled)
                    .lineSpacing(4)
                    .frame(maxWidth: .infinity, alignment: .leading)
            } else {
                Text(L("后端返回了结构化结果，但没有可直接阅读的文本字段。可在高级详情查看脱敏结构。"))
                    .foregroundStyle(.secondary)
            }
            let sources = result.sourceLinks
            if !sources.isEmpty {
                Divider()
                Text(L("来源")).font(.headline)
                ForEach(sources, id: \.absoluteString) { url in
                    Link(destination: url) {
                        VStack(alignment: .leading, spacing: 3) {
                            Text(url.host ?? url.absoluteString).font(.callout.weight(.medium))
                            Text(url.absoluteString).font(.caption).foregroundStyle(.secondary)
                                .fixedSize(horizontal: false, vertical: true)
                        }
                    }
                }
            }
            DisclosureGroup(L("高级 JSON")) {
                Text(result.redacted().prettyPrinted())
                    .font(.system(.body, design: .monospaced))
                    .textSelection(.enabled)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    @ViewBuilder
    private var resultActions: some View {
        Button(L("复制脱敏 JSON"), action: copy)
        Button(L("导出脱敏结果"), action: export)
    }
}

private struct ActivityView: View {
    @ObservedObject var model: AppModel
    @State private var confirmClear = false
    @State private var showPreferences = false
    @State private var filter = "all"

    private var runs: [ActivityRun] {
        model.activityRuns.filter { run in
            switch filter {
            case "running": return run.isActive
            case "failed": return run.status == "failed" || run.status == "interrupted"
            default: return true
            }
        }
    }

    var body: some View {
        VStack(spacing: 0) {
            HStack(spacing: 12) {
                Picker(L("活动筛选"), selection: $filter) {
                    Text(L("全部")).tag("all")
                    Text(L("运行中")).tag("running")
                    Text(L("失败")).tag("failed")
                }.pickerStyle(.segmented).frame(maxWidth: 320)
                Spacer()
                Button(L("刷新")) { Task { await model.refreshActivity() } }.disabled(model.isBusy.contains("activity"))
                Menu {
                    Button(L("观察设置…")) { showPreferences = true }
                    Button(L("清除已结束历史"), role: .destructive) { confirmClear = true }
                        .disabled(model.isBusy.contains("clear-activity"))
                } label: { Image(systemName: "ellipsis.circle") }
                .menuStyle(.borderlessButton).fixedSize().help(L("活动选项"))
            }.padding(20)
            if !model.activityEnabled || !model.activityErrors.isEmpty {
                HStack {
                    Label(model.activityErrors.isEmpty ? L("活动记录已暂停") : L("部分活动目录无法读取"), systemImage: "exclamationmark.circle")
                        .foregroundStyle(.secondary)
                    Spacer()
                    Button(L("查看设置")) { showPreferences = true }
                }.font(.callout).padding(.horizontal, 20).padding(.bottom, 12)
            }
            Divider()
            DesktopSplitView("activity", leadingWidths: 224...320, initialLeadingWidth: 260, detailMinimumWidth: 360) {
                List(selection: Binding<String?>(
                    get: { model.selectedActivity?.runID },
                    set: { id in
                        if let run = runs.first(where: { $0.runID == id }) {
                            Task { await model.showActivityDetails(run) }
                        }
                    })) {
                    ForEach(runs) { run in
                        VStack(alignment: .leading, spacing: 6) {
                            Text(model.displayLabel(for: run)).fontWeight(.medium).lineLimit(2)
                            Text([run.origin, run.phase.map { model.state?.phaseLabel($0) ?? $0 }]
                                .compactMap { $0 }.filter { !$0.isEmpty }.joined(separator: " · "))
                                .font(.caption).foregroundStyle(.secondary).lineLimit(1)
                            HStack {
                                StatusTag(status: run.status, label: model.state?.statusLabels[run.status])
                                Spacer()
                                Text(run.elapsedText).font(.caption.monospacedDigit()).foregroundStyle(.secondary)
                            }
                        }.padding(.vertical, 6).tag(run.runID)
                    }
                }.listStyle(.inset).scrollContentBackground(.hidden)
            } detail: {
                if let run = model.selectedActivity {
                    VStack(alignment: .leading, spacing: 0) {
                        if model.canCancel(run) {
                            HStack {
                                Spacer()
                                Button(L("取消任务"), role: .destructive) { Task { await model.cancel(run) } }
                                    .disabled(model.isBusy.contains("cancel:\(run.runID)"))
                            }.padding([.horizontal, .top], 20)
                        }
                        ActivityDetailView(model: model, run: run, embedded: true)
                    }.frame(maxWidth: .infinity, maxHeight: .infinity)
                } else {
                    VStack(spacing: 10) {
                        Image(systemName: "clock").font(.system(size: 34)).foregroundStyle(.secondary)
                        Text(runs.isEmpty ? L("没有可显示的活动记录") : L("选择一条活动查看详情")).font(.headline)
                        Text(L("这里显示当前观察范围内的任务。")).foregroundStyle(.secondary)
                    }.frame(maxWidth: .infinity, maxHeight: .infinity)
                }
            }
        }
        .onChange(of: runs) { visibleRuns in
            if let selected = model.selectedActivity, !visibleRuns.contains(where: { $0.runID == selected.runID }) {
                model.selectedActivity = nil
            }
        }
        .task {
            while !Task.isCancelled {
                await model.refreshActivity()
                try? await Task.sleep(nanoseconds: 2_000_000_000)
            }
        }
        .alert(L("清除活动历史？"), isPresented: $confirmClear) {
            Button(L("取消"), role: .cancel) {}
            Button(L("清除已结束记录"), role: .destructive) { Task { await model.clearActivityHistory() } }
        } message: { Text(L("仅清除已结束任务的活动元数据，不会删除配置、研究证据或你导出的文件。")) }
        .sheet(isPresented: $showPreferences) {
            DetailSheet(L("观察设置")) {
                Toggle(L("记录活动"), isOn: Binding(get: { model.activityEnabled }, set: { value in Task { await model.setActivityEnabled(value) } }))
                    .disabled(model.isBusy.contains("activity-setting"))
                Text(L("没有记录不代表外部 CLI 一定空闲。")).font(.caption).foregroundStyle(.secondary)
                Button(L("添加配置目录"), action: model.addObservedDirectory)
                ForEach(model.observedDirectories, id: \.self) { directory in
                    HStack {
                        Text(directory).font(.caption).textSelection(.enabled)
                        Spacer()
                        Button(L("移除")) { model.removeObservedDirectory(directory) }
                    }
                }
                ForEach(model.activityErrors, id: \.self) { Text($0).foregroundStyle(.orange) }
            }
        }
    }
}

private struct ActivityDetailView: View {
    @ObservedObject var model: AppModel
    let run: ActivityRun
    var embedded = false
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack {
                Text(L("活动详情")).font(.title2.weight(.semibold))
                Spacer()
                if !embedded { Button(L("完成")) { dismiss() } }
            }
            ScrollView {
                VStack(alignment: .leading, spacing: 14) {
                    Text(model.displayLabel(for: run)).font(.headline)
                    HStack {
                        StatusTag(status: run.status, label: model.state?.statusLabels[run.status])
                        Text(run.elapsedText).monospacedDigit().foregroundStyle(.secondary)
                        Spacer()
                        Text(run.origin).foregroundStyle(.secondary)
                    }
                    if let phase = run.phase {
                        KeyValueLine(label: L("当前阶段"), value: model.state?.phaseLabel(phase) ?? phase)
                    }
                    let provider = [run.provider, run.model].compactMap { $0 }.filter { !$0.isEmpty }.joined(separator: " · ")
                    if !provider.isEmpty { KeyValueLine(label: L("服务商与模型"), value: provider) }
                    if let error = run.errorType, !error.isEmpty {
                        Label(error, systemImage: "exclamationmark.circle").foregroundStyle(.red)
                    }
                    Text(L("配置版本：{0}", run.configRevision ?? L("活动记录未提供")))
                        .font(.caption).foregroundStyle(.secondary).textSelection(.enabled)
                    Divider()
                    if model.hasOwnedResult(for: run) {
                        Button(L("查看结果")) { model.showOwnedResult(run) }
                    }
                    if let result = model.activityResult(for: run) {
                        ActivityResultView(result: result)
                    }
                    if let details = model.activityDetails {
                        let events = details["events"]?.arrayValue ?? []
                        if details["ok"]?.boolValue == false {
                            Text(details["error"]?.stringValue ?? L("活动详情当前不可读取。"))
                                .foregroundStyle(.red)
                        } else if !events.isEmpty {
                              VStack(alignment: .leading, spacing: 12) {
                                ForEach(events.indices, id: \.self) { index in
                                    ActivityEventRow(event: events[index], phaseLabel: model.state?.phaseLabel(events[index]["phase"]?.displayString ?? "") ?? L("未知阶段"))
                                    if index != events.indices.last { Divider() }
                                }
                            }
                        } else {
                            Text(L("后端没有返回额外的脱敏阶段元数据。"))
                                .foregroundStyle(.secondary)
                        }
                        DisclosureGroup(L("高级详情")) {
                            Text(details.redacted().prettyPrinted())
                                .font(.system(.body, design: .monospaced))
                                .textSelection(.enabled)
                        }
                    } else {
                        ProgressView(L("正在读取脱敏活动详情…"))
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
            }
        }
        .padding(20)
        .frame(minWidth: embedded ? 300 : 560, maxWidth: .infinity, minHeight: 360, maxHeight: .infinity)
    }
}

private struct ActivityResultView: View {
    let result: JSONValue

    var body: some View {
        GroupBox(L("任务结果")) {
            VStack(alignment: .leading, spacing: 8) {
                if let text = result.readableText, !text.isEmpty {
                    Text(text).textSelection(.enabled)
                } else {
                    Text(L("后端没有提供可直接阅读的结果文本。"))
                        .foregroundStyle(.secondary)
                }
                DisclosureGroup(L("高级 JSON")) {
                    Text(result.redacted().prettyPrinted())
                        .font(.system(.body, design: .monospaced))
                        .textSelection(.enabled)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
        }
    }
}

private struct ActivityEventRow: View {
    let event: JSONValue
    let phaseLabel: String

    var body: some View {
        HStack(spacing: 10) {
            VStack(alignment: .leading, spacing: 3) {
                Text(phaseLabel)
                Text([event["provider"]?.stringValue, event["model"]?.stringValue]
                    .compactMap { $0 }
                    .filter { !$0.isEmpty }
                    .joined(separator: " · "))
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            Spacer()
            StatusTag(status: event["status"]?.displayString ?? "unknown")
        }
        .padding(.vertical, 3)
    }
}

private struct StatusTag: View {
    let status: String
    var label: String? = nil

    var body: some View {
        Text(label ?? ["running": L("运行中"), "finished": L("已完成"), "failed": L("失败"), "cancelled": L("已取消"), "cancelling": L("正在取消"), "stale": L("状态未更新"), "interrupted": L("已中断")][status] ?? L("状态未知"))
            .font(.caption.weight(.medium))
            .foregroundStyle(color)
            .padding(.horizontal, 8).padding(.vertical, 3)
            .background(color.opacity(0.12), in: Capsule())
    }

    private var color: Color {
        switch status {
        case "finished", "up_to_date": return .green
        case "failed", "interrupted": return .red
        case "running", "cancelling": return .orange
        case "cancelled", "stale": return .secondary
        default: return .secondary
        }
    }
}

private struct EnvironmentSetupView: View {
    @ObservedObject var model: AppModel
    private var environment: JSONValue { model.environmentState ?? .object([:]) }

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            GroupBox(L("准备独立 CLI")) {
                VStack(alignment: .leading, spacing: 14) {
                    Text(environment["message"]?.displayString ?? L("先检测环境，再安装缺少的组件。"))
                        .textSelection(.enabled)
                    if model.environmentBusy, let total = environment["total"]?.numberValue, total > 0 {
                        ProgressView(value: environment["received"]?.numberValue ?? 0, total: total)
                    }
                    ForEach(environment["steps"]?.arrayValue ?? [], id: \.self) { step in
                        VStack(alignment: .leading, spacing: 4) {
                            HStack {
                                Text(step["name"]?.displayString ?? "").font(.headline)
                                Spacer()
                                Text(step["status_label"]?.displayString ?? L("待处理"))
                                    .font(.caption).foregroundStyle(.secondary)
                            }
                            Text(step["message"]?.displayString ?? "").foregroundStyle(.secondary)
                        }
                    }
                    if environment["plan_id"]?.stringValue?.isEmpty == false {
                        Text(model.environmentActions.isEmpty ? L("独立 CLI 已就绪。可返回上方检查并更新 Skills。") :
                            L("本次将执行：\n") + model.environmentActions.map { "• " + $0 }.joined(separator: "\n"))
                    } else { Text(L("检测后会在这里列出将要安装或配置的内容。")).foregroundStyle(.secondary) }
                    HStack {
                        Button { Task { await model.environmentAction("environment.check") } } label: {
                            BusyLabel(text: L("检测环境"), busyText: L("检测中…"), busy: model.environmentBusy && environment["operation"]?.stringValue == "check")
                        }
                            .disabled(model.skillsBusy || model.environmentBusy || model.isUpdatingCLI)
                        Button { Task { await model.prepareEnvironment() } } label: {
                            BusyLabel(text: model.environmentActionLabel, busyText: L("准备中…"), busy: model.environmentBusy && environment["operation"]?.stringValue == "install")
                        }
                            .buttonStyle(.borderedProminent)
                            .disabled(model.skillsBusy || model.environmentBusy || model.isUpdatingCLI || model.environmentActions.isEmpty || environment["can_install"]?.boolValue != true || environment["plan_id"]?.stringValue?.isEmpty != false)
                        Button { Task { await model.environmentAction("environment.verify") } } label: {
                            BusyLabel(text: L("验证可用性"), busyText: L("验证中…"), busy: model.environmentBusy && environment["operation"]?.stringValue == "verify")
                        }
                            .disabled(model.skillsBusy || model.environmentBusy || model.isUpdatingCLI)
                        if environment["can_cancel"]?.boolValue == true {
                            Button(L("取消下载")) { Task { await model.environmentAction("environment.cancel") } }
                        }
                    }
                    HStack {
                        Button(L("去配置服务商")) { model.selectedDestination = .providers }
                        Button(L("复制 AI 测试指引"), action: model.copyEnvironmentTest)
                            .disabled(environment["invocation"]?.stringValue?.isEmpty != false)
                    }
                }.frame(maxWidth: .infinity, alignment: .leading)
            }
            DisclosureGroup(L("安装位置与检查详情")) {
                VStack(alignment: .leading, spacing: 8) {
                    KeyValueLine(label: L("独立安装目录"), value: environment["tools_dir"]?.displayString ?? L("检测后显示"))
                    if let checked = environment["checked_at"]?.numberValue, checked > 0 {
                        KeyValueLine(label: L("检查时间"), value: Date(timeIntervalSince1970: checked).formatted())
                    }
                    KeyValueLine(label: "Node", value: environment["node"]?["path"]?.displayString ?? "")
                    KeyValueLine(label: "Python", value: environment["python"]?["path"]?.displayString ?? "")
                    KeyValueLine(label: L("独立调用"), value: environment["invocation"]?.displayString ?? "")
                    KeyValueLine(label: L("配置目录"), value: environment["config_dir"]?.displayString ?? "")
                    Text(L("缺失的 Python 使用 Astral CPython；App 只负责管理，独立 CLI 不依赖 App。"))
                        .font(.caption).foregroundStyle(.secondary)
                    Text(environment["log"]?.displayString ?? "").font(.system(.caption, design: .monospaced)).textSelection(.enabled)
                    Text(environment["error"]?.displayString ?? "").foregroundStyle(.red)
                }.frame(maxWidth: .infinity, alignment: .leading).padding(.top, 8)
            }
        }.disabled(model.connection != .ready)
    }
}

private enum SkillsSheet: Identifiable {
    case preferences, environment, files(JSONValue)
    var id: String {
        switch self {
        case .preferences: return "preferences"
        case .environment: return "environment"
        case .files(let target): return target["target"]?.stringValue ?? "files"
        }
    }
}

private struct IntegrationView: View {
    @ObservedObject var model: AppModel
    @State private var sheet: SkillsSheet?
    private var skills: JSONValue { model.skillsState ?? .object([:]) }
    private var unavailable: Bool { model.connection != .ready || model.environmentBusy || model.isUpdatingCLI || model.skillsBusy || model.skillsChecking }

    var body: some View {
        if model.state != nil {
            DesktopPage(L("更新 Skills"), subtitle: L("选择要接入的 Agent，再更新对应的技能文件。")) {
                HStack {
                    VStack(alignment: .leading, spacing: 4) {
                        Text(L("技能来源")).font(.headline)
                        Text(skills["source"]?["version"]?.stringValue.map { "npm " + $0 } ?? L("尚未检查"))
                            .foregroundStyle(.secondary)
                    }
                    Spacer()
                    Button { Task { await model.skillsAction("skills.check") } } label: {
                        BusyLabel(text: L("检查最新 Skills"), busyText: L("检查中…"), busy: model.skillsChecking)
                    }.disabled(unavailable)
                    Button { sheet = .preferences } label: { Image(systemName: "gearshape") }
                        .help(L("检查偏好"))
                }
                if skills["cached"]?.boolValue == true {
                    Text(L("显示上次缓存；请检查最新 Skills 后再更新。")).font(.callout).foregroundStyle(.secondary)
                }
                if let error = skills["error"]?.stringValue, !error.isEmpty { Text(error).foregroundStyle(.red) }
                if let compatibility = skills["compatibility"]?.stringValue, !compatibility.isEmpty {
                    HStack {
                        Text(compatibility).font(.callout).foregroundStyle(.secondary)
                        Spacer()
                        Button(L("准备运行环境…")) { sheet = .environment }
                    }
                }
                VStack(spacing: 0) {
                    ForEach(skills["targets"]?.arrayValue ?? [], id: \.self) { target in
                        let id = target["target"]?.stringValue ?? ""
                        let name = target["label"]?.displayString ?? id
                        HStack(spacing: 12) {
                            VStack(alignment: .leading, spacing: 5) {
                                Text(name).font(.body.weight(.medium))
                                Text(skillStatus(target)).font(.caption).foregroundStyle(.secondary)
                                if let error = target["error"]?.stringValue, !error.isEmpty {
                                    Text(error).font(.caption).foregroundStyle(.red)
                                }
                            }
                            Spacer()
                            Button { sheet = .files(target) } label: { Image(systemName: "info.circle") }
                                .buttonStyle(.borderless).help(L("查看 {0} 的文件详情", name))
                            Toggle(name, isOn: Binding(
                                get: { model.selectedSkillTargets.contains(id) },
                                set: { if $0 { model.selectedSkillTargets.insert(id) } else { model.selectedSkillTargets.remove(id) } }))
                                .labelsHidden().disabled(model.skillsBusy)
                        }.padding(.vertical, 14)
                        Divider()
                    }
                }
                HStack {
                    Button(L("刷新本机状态")) { Task { await model.refreshSkillStatus() } }.disabled(unavailable)
                    Spacer()
                    Button(L("运行环境…")) { sheet = .environment }
                }
                ForEach(skills["result"]?["installed"]?.arrayValue ?? [], id: \.self) { receipt in
                    Label((receipt["target"]?.displayString ?? "") + L("：已同步"), systemImage: "checkmark.circle").foregroundStyle(.green)
                    if let backup = receipt["backup"]?.stringValue, !backup.isEmpty {
                        Text(L("备份：{0}", backup)).font(.caption).textSelection(.enabled)
                    }
                }
                ForEach(skills["result"]?["failed"]?.arrayValue ?? [], id: \.self) { failure in
                    Text((failure["target"]?.displayString ?? "") + ": " + (failure["error"]?.displayString ?? "")).foregroundStyle(.red)
                }
            }
            .safeAreaInset(edge: .bottom, spacing: 0) {
                VStack(spacing: 0) {
                    Divider()
                    HStack {
                        Text(L("已选择 {0} 个 Agent", "\(model.selectedSkillTargets.count)"))
                            .font(.callout).foregroundStyle(.secondary)
                        Spacer()
                        Button { Task { await model.installSelectedSkills() } } label: {
                            BusyLabel(text: L("更新所选 Skills"), busyText: L("更新中…"), busy: model.skillsBusy)
                        }.buttonStyle(.borderedProminent)
                            .disabled(unavailable || model.selectedSkillTargets.isEmpty || skills["can_sync"]?.boolValue != true)
                    }.padding(16)
                }.background(DesktopAppearance.contentBackground)
            }
            .sheet(item: $sheet) { selected in
                switch selected {
                case .preferences:
                    DetailSheet(L("检查偏好")) {
                        Toggle(L("每天自动检查 Skills，只提示，不写入"), isOn: Binding(
                            get: { skills["auto_check"]?.boolValue ?? true },
                            set: { enabled in Task { await model.skillsAction("skills.auto", params: .object(["enabled": .bool(enabled)])) } }))
                        if let checked = skills["source"]?["checked_at"]?.numberValue {
                            KeyValueLine(label: L("最近成功检查"), value: Date(timeIntervalSince1970: checked).formatted())
                        }
                        Text(L("不同内容会先备份；额外文件与未选目标保持原样。更新后重新打开 Agent 会话；Gemini 可运行 /skills reload。实际调用仍需在 Agent 中验证。"))
                            .font(.callout).foregroundStyle(.secondary)
                    }
                case .environment:
                    DetailSheet(L("运行环境")) { EnvironmentSetupView(model: model) }
                case .files(let target):
                    DetailSheet(target["label"]?.displayString ?? L("文件详情")) {
                        KeyValueLine(label: L("安装位置"), value: target["path"]?.displayString ?? L("未发现"))
                        Text(L("状态只表示 Smart Search Skill 内容。Codex 使用的 .agents/skills 也可能被其他兼容 Agent 读取。"))
                            .font(.callout).foregroundStyle(.secondary)
                        let changed = (target["stale_files"]?.arrayValue ?? []) + (target["missing_files"]?.arrayValue ?? [])
                        if !changed.isEmpty { Text(L("将同步：{0}", changed.map(\.displayString).joined(separator: ", "))) }
                        ForEach(target["legacy_locations"]?.arrayValue ?? [], id: \.self) { legacy in
                            Text(L("历史副本，保留：{0}", legacy["path"]?.displayString ?? "")).font(.caption)
                        }
                    }
                }
            }
        } else { BackendUnavailableView(model: model) }
    }

    private func skillStatus(_ value: JSONValue) -> String {
        switch value["status"]?.stringValue {
        case "missing": return L("未安装")
        case "stale": return L("内容不同，可同步")
        case "up_to_date", "extra_files": return L("与来源一致")
        case "error": return L("读取失败")
        default: return L("状态未知")
        }
    }
}

private struct SettingsAboutView: View {
    @ObservedObject var model: AppModel
    @State private var showEnvironment = false
    @State private var confirmEnableCLI = false

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 24) {
                applicationInfo
                settingsSection(L("通用"), subtitle: L("管理语言和配置目录。")) {
                    generalSettings
                }
                settingsSection(L("App 更新"), subtitle: L("更新 App 和内置引擎。")) {
                    UpdatesView(model: model, cliOnly: false)
                }
                settingsSection(L("独立 CLI"), subtitle: L("管理独立安装的命令行工具。")) {
                    UpdatesView(model: model, cliOnly: true)
                    DesktopPanel(L("运行环境"), compact: true) {
                        HStack(spacing: 16) {
                            Text(L("准备独立 CLI 与 AI 接入"))
                                .foregroundStyle(.secondary)
                                .fixedSize(horizontal: false, vertical: true)
                            Spacer(minLength: 0)
                            Button(L("准备运行环境…")) { showEnvironment = true }
                                .fixedSize()
                        }
                    }
                }
                settingsSection(L("高级"), subtitle: L("配置连接参数和内置 CLI。")) {
                    advancedSettings
                }
            }
            .frame(maxWidth: 800, alignment: .leading)
            .padding(24)
            .frame(maxWidth: .infinity, alignment: .topLeading)
        }
        .background(DesktopAppearance.contentBackground)
        .textFieldStyle(.roundedBorder)
        .sheet(isPresented: $showEnvironment) {
            DetailSheet(L("运行环境")) { EnvironmentSetupView(model: model) }
        }
        .alert(L("启用内置 CLI？"), isPresented: $confirmEnableCLI) {
            Button(L("取消"), role: .cancel) {}
            Button(L("确认启用")) { Task { await model.enableBundledCLI() } }
        } message: { Text(L("这会要求后端创建用户级 CLI 链接；它不会覆盖已存在的同名外部 CLI。")) }
    }

    private func settingsSection<Content: View>(_ title: String, subtitle: String,
                                                @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            VStack(alignment: .leading, spacing: 4) {
                Text(title).font(.title2.weight(.semibold))
                Text(subtitle).foregroundStyle(.secondary)
            }
            content()
        }
    }

    private var applicationInfo: some View {
        DesktopPanel(compact: true) {
            HStack(spacing: 12) {
                Image(nsImage: AppBranding.icon).resizable().frame(width: 48, height: 48)
                    .accessibilityHidden(true)
                VStack(alignment: .leading, spacing: 4) {
                    Text("Smart Search").font(.title2.weight(.semibold))
                    Text(L("版本 {0}", Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? L("尚未读取")))
                        .foregroundStyle(.secondary)
                    Text("konbakuyomu/smartsearch").font(.caption).foregroundStyle(.secondary)
                }
                Spacer(minLength: 12)
                Link(destination: URL(string: "https://github.com/konbakuyomu/smartsearch")!) {
                    Label("GitHub", systemImage: "arrow.up.right")
                }
                .buttonStyle(.bordered)
                .help(L("项目主页"))
            }
        }
    }

    private var generalSettings: some View {
        DesktopPanel(compact: true) {
            HStack(alignment: .top, spacing: 16) {
                VStack(alignment: .leading, spacing: 4) {
                    Text(L("界面语言")).fontWeight(.medium)
                    Text(L("App 与独立 CLI 分别保存语言选择。环境写入期间请等待操作完成。"))
                        .font(.callout).foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                Picker(L("界面语言"), selection: Binding(
                    get: { model.languagePreference },
                    set: { value in Task { await model.setLanguage(value) } })) {
                        Text(L("跟随系统")).tag("auto")
                        Text(L("简体中文")).tag("zh")
                        Text("English").tag("en")
                }
                .labelsHidden().frame(width: 168, alignment: .trailing)
                .disabled(model.skillsBusy || model.environmentBusy || model.isUpdatingCLI || model.isBusy.contains("language"))
            }
            Divider()
            HStack(alignment: .top, spacing: 16) {
                VStack(alignment: .leading, spacing: 4) {
                    Text(L("当前配置目录")).fontWeight(.medium)
                    Text(model.state?.configDirectory ?? L("后端尚未提供"))
                        .font(.system(.callout, design: .monospaced))
                        .foregroundStyle(.secondary).textSelection(.enabled)
                        .fixedSize(horizontal: false, vertical: true)
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                Button(model.isBusy.contains("profile") ? L("切换中…") : L("选择配置目录…"), action: model.chooseConfigDirectory)
                    .fixedSize()
                    .frame(width: 168, alignment: .trailing)
                    .disabled(model.connection != .ready || model.configOperationBusy)
            }
        }
    }

    private var advancedSettings: some View {
        VStack(alignment: .leading, spacing: 12) {
            DesktopPanel(L("后端连接"), compact: true) {
                VStack(alignment: .leading, spacing: 8) {
                    DisclosureGroup(L("开发选项")) {
                        VStack(alignment: .leading, spacing: 8) {
                            TextField(L("开发环境后端路径（可选）"), text: $model.backendPathOverride)
                            HStack {
                                Button(L("选择…"), action: model.chooseBackendExecutable)
                                Button(L("使用内置后端")) { model.saveBackendOverride("") }
                            }
                            Text(L("发布包默认使用 Contents/Resources/backend/smart-search。仅显式选择时才会使用开发路径。"))
                                .font(.caption).foregroundStyle(.secondary)
                        }
                    }
                    HStack {
                        Stepper(L("请求超时：{0} 秒", "\(Int(model.requestTimeoutSeconds))"), value: $model.requestTimeoutSeconds, in: 5...300, step: 5)
                        Button(L("应用超时"), action: model.applyTimeout)
                    }
                    HStack {
                        ConnectionIndicator(state: model.connection)
                        Spacer()
                        Button(model.isBusy.contains("connect") ? L("连接中…") : L("重新连接")) { model.saveBackendOverride(model.backendPathOverride); Task { await model.reconnect() } }.disabled(model.isBusy.contains("connect"))
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
            }
            DesktopPanel(L("App 内置 CLI"), compact: true) {
                KeyValueLine(label: L("内置路径"), value: model.cliStatus?["bundled_path"]?.displayString ?? L("尚未读取"))
                KeyValueLine(label: L("外部路径"), value: model.cliStatus?["external_path"]?.displayString ?? L("未发现或尚未读取"))
                KeyValueLine(label: L("内置版本"), value: model.cliStatus?["version"]?.displayString ?? L("尚未读取"))
                HStack {
                    Button(L("复制内置路径"), action: model.copyBundledCLIPath)
                    Button { Task { await model.refreshCLIStatus() } } label: { BusyLabel(text: L("刷新 CLI 状态"), busyText: L("刷新中…"), busy: model.isBusy.contains("cli.status")) }.disabled(model.isBusy.contains("cli.status"))
                    Spacer()
                }
                Divider()
                Text(L("内置入口随 App 卸载失效。独立 CLI 接入不使用此入口；已有同名命令不会被覆盖。"))
                    .font(.caption).foregroundStyle(.secondary)
                Button(model.isBusy.contains("cli.enable") ? L("启用中…") : L("启用内置 CLI…")) { confirmEnableCLI = true }
                    .disabled(model.connection != .ready || model.environmentBusy || model.isBusy.contains("cli.enable"))
            }
            .frame(maxWidth: .infinity, alignment: .leading)
        }
    }
}

private struct KeyValueLine: View {
    let label: String
    let value: String

    var body: some View {
        HStack(alignment: .firstTextBaseline) {
            Text(label).foregroundStyle(.secondary).frame(width: 130, alignment: .leading)
            Text(value).textSelection(.enabled)
                .fixedSize(horizontal: false, vertical: true)
            Spacer(minLength: 0)
        }
    }
}

private struct UpdatesView: View {
    @ObservedObject var model: AppModel
    let cliOnly: Bool
    private var app: JSONValue? { model.updateResult?["app"] }
    private var cli: JSONValue? { model.updateResult?["cli"] }
    private var download: JSONValue? { model.updateResult?["download"] }
    private var checking: Bool { model.updateResult?["checking"]?.boolValue == true || model.isBusy.contains("update") }
    private var cancelling: Bool { download?["status"]?.stringValue == "cancelling" || model.isBusy.contains("updates.cancel") }
    private var downloading: Bool { download?["status"]?.stringValue == "downloading" || cancelling }
    private var ready: Bool { download?["status"]?.stringValue == "ready" }

    var body: some View {
        Group {
            if cliOnly {
                DesktopPanel(L("版本与更新"), compact: true) {
                    HStack {
                        Button { Task { await model.checkForUpdates() } } label: {
                            BusyLabel(text: L("检查更新"), busyText: L("检查中…"), busy: checking)
                        }.disabled(checking || model.connection != .ready)
                        Button(L("刷新已安装版本")) { Task { await model.refreshCLIStatus() } }
                            .disabled(model.isBusy.contains("cli.status"))
                    }
                    if let error = model.updateResult?["error"]?.stringValue, !error.isEmpty {
                        Text(error).foregroundStyle(.red)
                    }
                    Divider()
                    cliCard
                }
            } else {
                DesktopPanel(L("版本与更新"), compact: true) {
                    VStack(alignment: .leading, spacing: 8) {
                        HStack(spacing: 16) {
                            Text(L("自动检查，每 24 小时一次，点击才下载"))
                                .fixedSize(horizontal: false, vertical: true)
                                .frame(maxWidth: .infinity, alignment: .leading)
                            Toggle(L("自动检查，每 24 小时一次，点击才下载"), isOn: Binding(
                                get: { model.updateResult?["auto_check"]?.boolValue ?? true },
                                set: { value in Task { await model.updateAction("updates.auto", params: .object(["enabled": .bool(value)])) } }))
                                .labelsHidden()
                                .fixedSize()
                        }
                        HStack {
                            Button { Task { await model.checkForUpdates() } } label: {
                                BusyLabel(text: L("检查更新"), busyText: L("检查中…"), busy: checking)
                            }.disabled(checking || model.connection != .ready)
                            Button(L("刷新已安装版本")) { Task { await model.refreshState() } }.disabled(model.isBusy.contains("state"))
                        }
                        if let error = model.updateResult?["error"]?.stringValue, !error.isEmpty { Text(error).foregroundStyle(.red) }
                        if let timestamp = app?["checked_at"]?.numberValue {
                            Text(L("App 检查时间：{0}", "\(Date(timeIntervalSince1970: timestamp).formatted())")).font(.caption).foregroundStyle(.secondary)
                        }
                        Text(L("App 和内置引擎一起更新；独立 CLI 使用原管理器更新。")).font(.caption).foregroundStyle(.secondary)
                    }.frame(maxWidth: .infinity, alignment: .leading)
                    Divider()
                    appCard
                }
            }
        }
    }

    private var appCard: some View {
        VStack(alignment: .leading, spacing: 8) {
            KeyValueLine(label: "App", value: app?["current_version"]?.displayString ?? L("尚未读取"))
            KeyValueLine(label: L("内置引擎"), value: model.state?.version ?? L("尚未读取"))
            KeyValueLine(label: L("可安装稳定版"), value: app?["latest_version"]?.displayString ?? L("尚未检查"))
            if app?["package_pending"]?.boolValue == true { Text(L("较新的发行版尚未提供本平台完整安装包。")).foregroundStyle(.orange) }
            if downloading {
                let received = download?["received"]?.numberValue ?? 0
                let total = max(download?["total"]?.numberValue ?? 1, 1)
                ProgressView(value: received, total: total)
                Text(L("已下载 {0} / {1} MiB", "\(Int(received / 1048576))", "\(Int(total / 1048576))")).monospacedDigit()
            }
            if ready { Text(L("已下载并校验，尚未安装。")).foregroundStyle(.green) }
            if cancelling { Text(L("正在取消下载…")).foregroundStyle(.secondary) }
            if let error = download?["error"]?.stringValue, !error.isEmpty { Text(error).foregroundStyle(.orange) }
            HStack {
                Button(downloading ? L("下载中…") : L("下载安装包")) { Task { await model.updateAction("updates.download") } }
                    .disabled(downloading || model.isBusy.contains("updates.download") || app?["available"]?.boolValue != true || !(app?["error"]?.stringValue ?? "").isEmpty)
                if downloading { Button(cancelling ? L("正在取消…") : L("取消下载")) { Task { await model.updateAction("updates.cancel") } }.disabled(cancelling) }
                if ready { Button(L("打开安装包")) { Task { await model.openDownloadedUpdate() } }.disabled(model.environmentBusy || model.isBusy.contains("updates.installer")) }
            }
            HStack {
                if ready { Button(L("打开下载目录"), action: model.revealDownloadedUpdate) }
                Link(L("查看版本说明"), destination: URL(string: "https://github.com/konbakuyomu/smartsearch/releases")!)
            }
            Text(L("安装包校验 SHA256，尚未验证系统代码签名。打开 DMG 后先退出 App，再按正常方式安装并重新打开核对版本。"))
                .font(.caption).foregroundStyle(.secondary)
        }.frame(maxWidth: .infinity, alignment: .leading)
    }

    private var cliCard: some View {
        VStack(alignment: .leading, spacing: 8) {
            KeyValueLine(label: L("实际版本"), value: model.cliStatus?["external_version"]?.displayString ?? L("未安装或未知"))
            KeyValueLine(label: L("npm 稳定版"), value: cli?["latest_version"]?.displayString ?? L("尚未检查"))
            KeyValueLine(label: L("来源"), value: model.cliStatus?["manager_label"]?.displayString ?? L("未确认"))
            DisclosureGroup(L("安装位置")) {
                KeyValueLine(label: L("生效路径"), value: model.cliStatus?["resolved_path"]?.displayString ?? model.cliStatus?["external_path"]?.displayString ?? L("未发现"))
                Text(L("入口：") + (model.cliStatus?["external_path"]?.displayString ?? L("未发现"))).font(.system(.caption, design: .monospaced)).textSelection(.enabled)
            }
            Text(model.cliStatus?["update_note"]?.displayString ?? "").font(.caption).foregroundStyle(.secondary)
            HStack {
                Button(model.isUpdatingCLI ? L("更新中…") : L("更新 CLI")) { Task { await model.updateCLI() } }
                    .disabled(model.skillsBusy || model.environmentBusy || model.isUpdatingCLI || model.isBusy.contains("cli.update") || checking || cli?["available"]?.boolValue != true || model.cliStatus?["can_update"]?.boolValue != true || !(cli?["error"]?.stringValue ?? "").isEmpty)
                Button(L("复制更新命令"), action: model.copyCLIUpdateCommand).disabled(cli?["command"] == nil)
            }
            if model.isUpdatingCLI { Text(L("请保持 App 打开，等待原管理器完成。")).foregroundStyle(.orange) }
            if model.updateResult?["cli_update"]?["status"]?.stringValue == "finished" { Text(L("已更新并验证实际版本。")).foregroundStyle(.green) }
            if let error = model.updateResult?["cli_update"]?["error"]?.stringValue, !error.isEmpty { Text(error).foregroundStyle(.red) }
            DisclosureGroup(L("更新日志与命令")) {
                Text((cli?["command"]?.stringValue ?? "") + "\n" + (model.updateResult?["cli_update"]?["log"]?.stringValue ?? ""))
                    .font(.system(.caption, design: .monospaced)).textSelection(.enabled).frame(maxWidth: .infinity, alignment: .leading)
            }
        }.frame(maxWidth: .infinity, alignment: .leading)
    }
}

private extension JSONValue {
    var readableText: String? {
        let preferredKeys = ["display_text", "answer", "content", "text", "summary", "message", "output", "error"]
        if let object = objectValue {
            for key in preferredKeys {
                if let text = object[key]?.stringValue, !text.isEmpty { return text }
            }
            for key in ["result", "data"] {
                if let text = object[key]?.readableText, !text.isEmpty { return text }
            }
        }
        return stringValue
    }

    var sourceLinks: [URL] {
        var links: Set<URL> = []
        collectSourceLinks(into: &links)
        return links.sorted { $0.absoluteString < $1.absoluteString }
    }

    private func collectSourceLinks(into links: inout Set<URL>) {
        switch self {
        case let .object(object):
            for key in ["url", "link", "source_url", "href"] {
                if let value = object[key]?.stringValue,
                   let url = URL(string: value),
                   let scheme = url.scheme?.lowercased(),
                   ["http", "https"].contains(scheme) {
                    links.insert(url)
                }
            }
            for value in object.values { value.collectSourceLinks(into: &links) }
        case let .array(values):
            for value in values { value.collectSourceLinks(into: &links) }
        default:
            break
        }
    }
}
