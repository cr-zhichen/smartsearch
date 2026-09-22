import AppKit
import SwiftUI

struct CLIManagementView: View {
    @ObservedObject var model: AppModel
    @ObservedObject private var manager: CLIInstallationManager
    var firstStep: Bool
    @State private var npmPath = ""
    @State private var showingSettings = false
    @State private var pendingAction: SettingsAction?

    private enum SettingsAction {
        case install, uninstall, reconnect
        case npmPath(String)
    }

    init(model: AppModel, firstStep: Bool = false) {
        self.model = model
        self.firstStep = firstStep
        manager = model.cliManager
    }

    var body: some View {
        DesktopPanel {
            DesktopStepHeader(title: firstStep ? L("1. 安装 CLI") : L("独立 CLI"), subtitle: status) {
                if manager.busy || manager.checking { ProgressView().controlSize(.small) }
                if manager.npm == nil {
                    Link(L("安装 Node.js"), destination: URL(string: "https://nodejs.org/en/download")!)
                        .buttonStyle(.borderedProminent)
                } else if manager.selected?.compatible != true || manager.updateAvailable {
                    Button(installTitle) { Task { await model.manageCLI() } }
                        .buttonStyle(.borderedProminent).disabled(blocked || releaseUnavailable)
                } else if model.connection != .ready {
                    Button(L("重新检测")) { Task { await model.reconnect() } }.disabled(blocked)
                }
                Button(L("CLI 设置…")) {
                    npmPath = manager.manualNpmPath
                    showingSettings = true
                }
            }
            if !manager.message.isEmpty {
                Text(manager.message).font(.callout).foregroundStyle(.secondary).textSelection(.enabled)
            }
            if !manager.checkError.isEmpty {
                Text(L("检查更新失败，请稍后重试。")).font(.callout).foregroundStyle(.orange)
            }
        }
        .sheet(isPresented: $showingSettings, onDismiss: performPendingAction) {
            DetailSheet(L("CLI 设置")) {
                DesktopStepHeader(title: L("独立 CLI"), subtitle: status) {
                    Button(L("重新检测")) { deferAction(.reconnect) }.disabled(blocked)
                    Button(L("检查更新")) { Task { await manager.checkVersion() } }
                        .disabled(manager.npm == nil || blocked)
                }
                Divider()
                VStack(alignment: .leading, spacing: 8) {
                    Text(L("npm 环境")).font(.headline)
                    if let npm = manager.npm {
                        Text(L("npm {0} · {1}", npm.version, npm.npmPath))
                            .font(.callout).textSelection(.enabled)
                        Text(L("安装目录：{0}", npm.prefix))
                            .font(.caption).foregroundStyle(.secondary).textSelection(.enabled)
                    } else {
                        Text(L("未找到 npm")).foregroundStyle(.secondary)
                    }
                    TextField(L("npm 路径（留空自动查找）"), text: $npmPath)
                        .textFieldStyle(.roundedBorder).disabled(blocked)
                    HStack(spacing: 8) {
                        Button(L("选择文件…")) { chooseNpm() }.disabled(blocked)
                        Button(L("应用路径")) { deferAction(.npmPath(npmPath)) }.disabled(blocked)
                        Button(L("自动查找")) { deferAction(.npmPath("")) }.disabled(blocked)
                    }
                }
                Divider()
                VStack(alignment: .leading, spacing: 8) {
                    Toggle(L("自动检查 CLI 更新"), isOn: $manager.automaticallyChecks)
                    Text(L("启动时及每 24 小时检查。")).font(.caption).foregroundStyle(.secondary)
                    if let date = manager.checkedAt {
                        Text(L("上次检查：{0}", date.formatted(date: .abbreviated, time: .shortened)))
                            .font(.caption).foregroundStyle(.secondary)
                    }
                }
                Divider()
                HStack(spacing: 8) {
                    Button(installTitle) { deferAction(.install) }
                        .disabled(manager.npm == nil || blocked || releaseUnavailable)
                    if manager.selected != nil {
                        Spacer()
                        Button(L("卸载 CLI"), role: .destructive) { deferAction(.uninstall) }.disabled(blocked)
                    }
                }
                if !manager.checkError.isEmpty {
                    DisclosureGroup(L("错误详情")) {
                        Text(manager.checkError).font(.caption).textSelection(.enabled)
                    }
                }
            }
        }
    }

    private var blocked: Bool { manager.busy || manager.checking || !model.canInstallAppUpdate }
    private var releaseUnavailable: Bool { !manager.latestVersion.isEmpty && !manager.latestSupportsBinary }
    private var installTitle: String {
        if manager.selected == nil { return L("安装 CLI") }
        if manager.updateAvailable { return L("更新 CLI") }
        return L("修复 CLI")
    }
    private var status: String {
        if manager.busy { return L("处理中…") }
        guard manager.npm != nil else { return L("未找到 npm") }
        guard let installation = manager.selected else {
            return releaseUnavailable ? L("暂时无法安装，请稍后重试。") : L("尚未安装")
        }
        if !installation.compatible { return L("{0} · 需要修复", installation.version) }
        if manager.updateAvailable { return L("{0} · 可更新至 {1}", installation.version, manager.latestVersion) }
        if model.connection == .ready { return L("{0} · 已就绪", installation.version) }
        return L("{0} · 尚未连接", installation.version)
    }

    private func deferAction(_ action: SettingsAction) {
        pendingAction = action
        showingSettings = false
    }

    private func performPendingAction() {
        guard let action = pendingAction else { return }
        pendingAction = nil
        Task {
            switch action {
            case .install: await model.manageCLI()
            case .uninstall: await model.manageCLI(remove: true)
            case .reconnect: await model.reconnect()
            case .npmPath(let path): await model.setNpmPath(path)
            }
        }
    }

    private func chooseNpm() {
        let panel = NSOpenPanel()
        panel.canChooseDirectories = false
        panel.allowsMultipleSelection = false
        panel.message = L("选择与你的 Node.js 配套的 npm 文件。")
        if panel.runModal() == .OK, let path = panel.url?.path { npmPath = path }
    }
}
