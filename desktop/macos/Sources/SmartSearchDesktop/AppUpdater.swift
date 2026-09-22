import AppKit
import Combine
import Foundation
import Sparkle

@MainActor
final class AppUpdater: NSObject, ObservableObject, SPUUpdaterDelegate, SPUStandardUserDriverDelegate {
    enum Phase { case idle, checking, available, upToDate, skipped, downloading, ready, failed }

    @Published private(set) var started = false
    @Published private(set) var automaticallyChecks = UserDefaults.standard.object(forKey: "SUEnableAutomaticChecks") as? Bool ?? true
    @Published private(set) var phase: Phase = .idle
    @Published private(set) var latestVersion = ""
    @Published private(set) var waitingToRestart = false
    @Published private(set) var checkedAt: Date?
    @Published private(set) var sessionInProgress = false
    @Published private var canShowUpdate = false
    @Published private var unavailableReason = ""
    private var observations: [NSKeyValueObservation] = []
    var canInstall: () -> Bool = { false }
    var prepareInstall: () async -> Bool = { false }
    var recoverInstall: () async -> Void = {}
    private var resumeInstallation: (() -> Void)?
    private var preparing = false
    private var prepared = false
    private var deferredPrompt = false
    private var shownVersions: Set<String> = []
    private lazy var driver = SPUStandardUserDriver(hostBundle: .main, delegate: self)
    private lazy var updater = SPUUpdater(hostBundle: .main, applicationBundle: .main, userDriver: driver, delegate: self)

    var currentVersion: String { Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "" }
    var checking: Bool { phase == .checking }
    var available: Bool { !latestVersion.isEmpty && phase != .failed && phase != .skipped }
    var canCheck: Bool { started && !sessionInProgress && !checking && !waitingToRestart }
    var canInstallUpdate: Bool { started && !checking && (waitingToRestart || canShowUpdate) }
    var statusMessage: String {
        if !unavailableReason.isEmpty { return L(unavailableReason) }
        switch phase {
        case .idle: return L("尚未检查")
        case .checking: return L("正在检查 App 更新…")
        case .available: return L("发现新版本 {0}", latestVersion)
        case .upToDate: return L("暂无可安装的更新")
        case .skipped: return L("已跳过版本 {0}", latestVersion)
        case .downloading: return L("正在下载并准备更新…")
        case .ready: return L("更新已就绪")
        case .failed: return L("App 更新未完成，请重试。")
        }
    }

    func start() {
        guard !started else { return }
        unavailableReason = ""
        guard let key = Bundle.main.object(forInfoDictionaryKey: "SUPublicEDKey") as? String,
              Data(base64Encoded: key)?.count == 32 else {
            unavailableReason = "此副本尚未配置自动更新，请安装正式版。"
            return
        }
        updater.automaticallyChecksForUpdates = automaticallyChecks
        updater.automaticallyDownloadsUpdates = false
        updater.updateCheckInterval = 86400
        do { try updater.start(); started = true }
        catch { unavailableReason = "App 更新配置无效，请安装正式版后重试。"; return }
        observations = [
            updater.observe(\.sessionInProgress, options: [.initial, .new]) { [weak self] _, _ in
                Task { @MainActor in self?.refreshAvailability() }
            },
            updater.observe(\.canCheckForUpdates, options: [.initial, .new]) { [weak self] _, _ in
                Task { @MainActor in self?.refreshAvailability() }
            },
            updater.observe(\.automaticallyChecksForUpdates, options: [.new]) { [weak self] _, _ in
                Task { @MainActor in self?.refreshAvailability() }
            },
        ]
        // Check on every launch; Sparkle owns subsequent scheduling and skipped versions.
        if automaticallyChecks { updater.checkForUpdatesInBackground() }
        refreshAvailability()
    }

    func setAutomaticallyChecks(_ value: Bool) {
        guard started else { return }
        updater.automaticallyChecksForUpdates = value
        automaticallyChecks = value
    }

    private func refreshAvailability() {
        sessionInProgress = updater.sessionInProgress
        canShowUpdate = updater.canCheckForUpdates
        automaticallyChecks = updater.automaticallyChecksForUpdates
    }

    func check() {
        guard canCheck, !updater.sessionInProgress else { return }
        phase = .checking
        // Manual checks include skipped versions and expose Sparkle's Install / Later / Skip choices.
        updater.checkForUpdates()
        refreshAvailability()
    }

    func install() {
        guard canInstallUpdate else { return }
        if waitingToRestart { Task { await completePreparedUpdate() }; return }
        updater.checkForUpdates()
        refreshAvailability()
    }

    func updater(_ updater: SPUUpdater, mayPerform updateCheck: SPUUpdateCheck) throws {
        phase = .checking
    }

    func resumePromptIfPossible() {
        guard deferredPrompt, started, updater.automaticallyChecksForUpdates, canInstall() else { return }
        deferredPrompt = false
        shownVersions.insert(latestVersion)
        updater.checkForUpdates() // Brings the already-fetched scheduled update into focus.
    }

    func updater(_ updater: SPUUpdater, didFindValidUpdate item: SUAppcastItem) {
        latestVersion = item.displayVersionString
        checkedAt = Date()
        phase = .available
    }

    func updaterDidNotFindUpdate(_ updater: SPUUpdater) {
        latestVersion = ""
        checkedAt = Date()
        phase = .upToDate
    }

    func updater(_ updater: SPUUpdater, userDidMake choice: SPUUserUpdateChoice, forUpdate item: SUAppcastItem, state: SPUUserUpdateState) {
        if choice == .skip && state.stage != .installing { phase = .skipped }
    }

    func updater(_ updater: SPUUpdater, willDownloadUpdate item: SUAppcastItem, with request: NSMutableURLRequest) {
        phase = .downloading
    }

    func updater(_ updater: SPUUpdater, didExtractUpdate item: SUAppcastItem) {
        phase = .ready
    }

    func updater(_ updater: SPUUpdater, shouldProceedWithUpdate item: SUAppcastItem, updateCheck: SPUUpdateCheck) throws {
        if updateCheck == .updatesInBackground && shownVersions.contains(item.displayVersionString) {
            // SUNoUpdateError: a dismissed version is silent until a manual check or next launch.
            throw NSError(domain: SUSparkleErrorDomain, code: 1001, userInfo: [NSLocalizedDescriptionKey: L("稍后可在设置中更新。")])
        }
    }

    var supportsGentleScheduledUpdateReminders: Bool { true }

    func standardUserDriverShouldHandleShowingScheduledUpdate(_ update: SUAppcastItem, andInImmediateFocus immediateFocus: Bool) -> Bool {
        canInstall()
    }

    func standardUserDriverWillHandleShowingUpdate(_ handleShowingUpdate: Bool, forUpdate update: SUAppcastItem, state: SPUUserUpdateState) {
        deferredPrompt = !handleShowingUpdate
        if handleShowingUpdate { shownVersions.insert(update.displayVersionString) }
    }

    func updater(_ updater: SPUUpdater, shouldPostponeRelaunchForUpdate item: SUAppcastItem, untilInvokingBlock installHandler: @escaping () -> Void) -> Bool {
        resumeInstallation = installHandler
        waitingToRestart = true
        phase = .ready
        Task { await completePreparedUpdate() }
        return true
    }

    private func completePreparedUpdate() async {
        guard !preparing, let resume = resumeInstallation else { return }
        guard canInstall() else { return }
        preparing = true
        defer { preparing = false }
        guard await prepareInstall() else { return }
        prepared = true
        resumeInstallation = nil
        waitingToRestart = false
        resume()
    }

    func updater(_ updater: SPUUpdater, didFinishUpdateCycleFor updateCheck: SPUUpdateCheck, error: Error?) {
        deferredPrompt = false
        if let error = error as NSError?, !(error.domain == SUSparkleErrorDomain && error.code == 1001) {
            phase = .failed
            resumeInstallation = nil
            waitingToRestart = false
            if prepared { prepared = false; Task { await recoverInstall() } }
        } else if !waitingToRestart && phase != .upToDate && phase != .skipped {
            phase = latestVersion.isEmpty ? .idle : .available
        }
        refreshAvailability()
    }
}
