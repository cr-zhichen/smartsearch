import SwiftUI

struct AppUpdatesView: View {
    @ObservedObject var model: AppModel
    @ObservedObject private var updater: AppUpdater

    init(model: AppModel) {
        self.model = model
        updater = model.appUpdater
    }

    private var updateInProgress: Bool { updater.sessionInProgress && !updater.checking }
    private var offersInstallation: Bool { updater.available || updater.waitingToRestart || updateInProgress }
    private var actionEnabled: Bool {
        if offersInstallation {
            return updater.canInstallUpdate && (updateInProgress && !updater.waitingToRestart || model.canInstallAppUpdate)
        }
        return updater.canCheck
    }
    private var actionTitle: String {
        if updater.waitingToRestart { return L("重启并完成更新") }
        if updateInProgress { return L("查看更新进度") }
        if updater.available { return L("更新到 {0}", updater.latestVersion) }
        if updater.phase == .failed { return L("重新检查") }
        return L("检查更新")
    }
    private var statusIcon: String {
        if !updater.started || updater.phase == .failed { return "exclamationmark.circle" }
        switch updater.phase {
        case .upToDate: return "checkmark.circle"
        case .available: return "arrow.up.circle"
        case .ready: return "arrow.clockwise.circle"
        case .skipped: return "bell.slash"
        default: return "arrow.triangle.2.circlepath"
        }
    }

    var body: some View {
        DesktopPanel {
            HStack(spacing: 8) {
                if updater.checking || updater.phase == .downloading {
                    ProgressView().controlSize(.small)
                }
                Label(updater.statusMessage, systemImage: statusIcon)
                    .font(.headline)
                    .foregroundStyle(updater.phase == .failed ? Color.red : Color.primary)
                    .fixedSize(horizontal: false, vertical: true)
            }

            Grid(alignment: .leading, horizontalSpacing: 30, verticalSpacing: 10) {
                versionRow(L("当前版本"), value: updater.currentVersion.isEmpty ? L("未知") : updater.currentVersion)
                versionRow(L("可安装版本"), value: availableVersion)
                versionRow(L("上次检查"), value: lastCheckText)
            }
            .frame(maxWidth: .infinity, alignment: .leading)

            Divider()

            HStack(spacing: 16) {
                VStack(alignment: .leading, spacing: 4) {
                    Text(L("自动检查更新"))
                    Text(L("启动时检查新版本，之后每 24 小时检查。确认后才下载。"))
                        .font(.callout).foregroundStyle(.secondary)
                }
                Spacer(minLength: 0)
                Toggle(L("自动检查更新"), isOn: Binding(
                    get: { updater.automaticallyChecks },
                    set: { updater.setAutomaticallyChecks($0) }))
                    .labelsHidden()
                    .toggleStyle(.switch)
                    .disabled(!updater.started)
            }

            HStack(spacing: 10) {
                Button {
                    if offersInstallation { updater.install() } else { updater.check() }
                } label: {
                    BusyLabel(text: actionTitle, busyText: L("检查中…"), busy: updater.checking)
                }
                .buttonStyle(.borderedProminent)
                .disabled(!actionEnabled)
                if updater.available && updater.canCheck {
                    Button(L("重新检查")) { updater.check() }
                }
                Link(L("查看版本说明"), destination: URL(string: "https://github.com/konbakuyomu/smartsearch/releases")!)
                if !updater.started && model.backendPathOverride.isEmpty {
                    Link(L("下载正式版"), destination: URL(string: "https://github.com/konbakuyomu/smartsearch/releases/latest")!)
                }
            }

            if offersInstallation && !model.canInstallAppUpdate {
                Text(L("请先处理正在进行的任务或未保存修改，再点击更新重启。"))
                    .font(.callout).foregroundStyle(.secondary)
            }
            if updater.phase == .skipped {
                Text(L("此版本不再自动提醒。手动检查仍可查看并安装。"))
                    .font(.callout).foregroundStyle(.secondary)
            }
            Text(L("在 App 内完成下载和安装，重启后生效。配置、独立 CLI 和 Skills 保留。"))
                .font(.caption).foregroundStyle(.secondary)
        }
    }

    private var availableVersion: String {
        if !updater.latestVersion.isEmpty { return updater.latestVersion }
        if updater.checkedAt != nil { return updater.currentVersion }
        return L("尚未检查")
    }

    private var lastCheckText: String {
        guard let date = updater.checkedAt else { return L("尚未检查") }
        return date.formatted(.dateTime.year().month(.abbreviated).day().hour().minute().locale(model.interfaceLocale))
    }

    private func versionRow(_ label: String, value: String) -> some View {
        GridRow {
            Text(label).foregroundStyle(.secondary)
            Text(value).textSelection(.enabled)
        }
    }
}
