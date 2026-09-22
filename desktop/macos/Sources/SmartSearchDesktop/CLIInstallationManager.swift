import Combine
import Foundation

struct CLIInstallation: Identifiable, Codable, Equatable, Sendable {
    var id: String
    var executable: String
    var arguments: [String] = []
    var version: String = ""
    var source: String
    var compatible = false
    var manager: [String] = []
    var prefix: String = ""
    var environment: [String: String] = [:]
    var canManage: Bool { source == "npm" && !manager.isEmpty }
    var title: String { "npm · \(version.isEmpty ? L("版本未知") : version) · \(id)" }
}

struct NpmEnvironment: Equatable, Sendable {
    var npmPath: String
    var nodePath: String
    var version: String
    var prefix: String
    var modules: String
    var command: [String]
    var environment: [String: String]
    var identity: String { nodePath + "|" + command.joined(separator: "|") + "|" + prefix }
}

private struct CLIUpdateRecord: Codable {
    var identity = ""
    var latestVersion = ""
    var supportsBinary = false
    var checkedAt: Date?
    var attemptedAt: Date?
}

enum CLIManagementError: LocalizedError {
    case failed(String)
    var errorDescription: String? { if case let .failed(message) = self { return message }; return nil }
}

@MainActor
final class CLIInstallationManager: ObservableObject {
    @Published private(set) var installations: [CLIInstallation] = []
    @Published private(set) var npm: NpmEnvironment?
    @Published private(set) var busy = false
    @Published private(set) var message = ""
    @Published private(set) var checking = false
    @Published private(set) var checkError = ""
    @Published private var update = CLIUpdateRecord()
    @Published var automaticallyChecks: Bool {
        didSet { preferences.set(automaticallyChecks, forKey: "SmartSearchDesktop.cliAutoCheck") }
    }
    @Published private(set) var manualNpmPath: String
    var selected: CLIInstallation? { installations.first }
    var latestVersion: String { update.latestVersion }
    var latestSupportsBinary: Bool { update.supportsBinary }
    var checkedAt: Date? { update.checkedAt }
    var updateAvailable: Bool {
        guard let selected, Self.validVersion(selected.version), Self.validVersion(latestVersion) else { return false }
        return selected.version.compare(latestVersion, options: .numeric) == .orderedAscending
    }
    private static let package = "@konbakuyomu/smart-search"
    private let preferences: UserDefaults
    private let initialSearchPath: String?
    private let versionLoader: (() async throws -> String)?
    private let clock: () -> Date
    private var checkedAtLaunch = false
    private var automaticTask: Task<Void, Never>?

    init(searchPath: String? = nil, preferences: UserDefaults = .standard,
         versionLoader: (() async throws -> String)? = nil, clock: @escaping () -> Date = Date.init) {
        self.preferences = preferences
        self.initialSearchPath = searchPath
        self.manualNpmPath = preferences.string(forKey: "SmartSearchDesktop.npmPath") ?? ""
        self.automaticallyChecks = preferences.object(forKey: "SmartSearchDesktop.cliAutoCheck") as? Bool ?? true
        self.versionLoader = versionLoader
        self.clock = clock
        if let data = preferences.data(forKey: "SmartSearchDesktop.cliUpdateState"),
           let record = try? JSONDecoder().decode(CLIUpdateRecord.self, from: data) { update = record }
    }

    deinit { automaticTask?.cancel() }

    func setNpmPath(_ path: String) {
        manualNpmPath = (path as NSString).expandingTildeInPath.trimmingCharacters(in: .whitespacesAndNewlines)
        preferences.set(manualNpmPath, forKey: "SmartSearchDesktop.npmPath")
    }

    func startAutomaticChecks() {
        guard automaticTask == nil else { return }
        automaticTask = Task { [weak self] in
            while !Task.isCancelled {
                do { try await Task.sleep(nanoseconds: 60_000_000_000) } catch { return }
                await self?.checkAutomatically()
            }
        }
    }

    func checkAutomatically(onLaunch: Bool = false) async {
        guard automaticallyChecks, npm != nil, !busy, !checking else { return }
        if !(onLaunch && !checkedAtLaunch) {
            if let date = update.checkedAt, clock().timeIntervalSince(date) < 86400 { return }
            if let date = update.attemptedAt, clock().timeIntervalSince(date) < 3600 { return }
        }
        checkedAtLaunch = true
        await checkVersion()
    }

    func discover() async {
        guard !busy, !checking else { return }
        busy = true
        defer { busy = false }
        installations = []
        npm = nil
        message = ""
        var searchPath = initialSearchPath ?? ProcessInfo.processInfo.environment["PATH"] ?? "/usr/bin:/bin"
        if initialSearchPath == nil, let loginPath = try? await Self.command(["/bin/zsh", "-lc", "print -r -- $PATH"]) {
            searchPath = loginPath.trimmingCharacters(in: .whitespacesAndNewlines) + ":" + searchPath
        }
        let paths = manualNpmPath.isEmpty ? npmCandidates(searchPath: searchPath) : [manualNpmPath]
        for path in paths {
            do { npm = try await resolveNpm(path, searchPath: searchPath); break }
            catch { if !manualNpmPath.isEmpty { message = error.localizedDescription } }
        }
        guard let npm else {
            if message.isEmpty { message = L("未找到可用的 npm。请先安装 Node.js，或手动指定 npm 路径。") }
            return
        }
        if update.identity != npm.identity {
            update = CLIUpdateRecord(identity: npm.identity)
            checkedAtLaunch = false
            checkError = ""
            saveUpdateState()
        }
        await refreshInstallation(npm)
    }

    private func npmCandidates(searchPath: String) -> [String] {
        var directories = searchPath.split(separator: ":").map(String.init)
        if initialSearchPath == nil {
            let home = FileManager.default.homeDirectoryForCurrentUser.path
            let mise = ProcessInfo.processInfo.environment["MISE_DATA_DIR"] ?? home + "/.local/share/mise"
            directories += ["/opt/homebrew/bin", "/usr/local/bin", home + "/.local/bin", home + "/.volta/bin", mise + "/shims"]
            for root in [mise + "/installs/node", home + "/.nvm/versions/node"] {
                let versions = (try? FileManager.default.contentsOfDirectory(atPath: root)) ?? []
                directories += versions.sorted { $0.compare($1, options: .numeric) == .orderedDescending }.map { root + "/" + $0 + "/bin" }
            }
        }
        var seen = Set<String>()
        return directories.filter { $0.hasPrefix("/") }.map { $0 + "/npm" }
            .filter { seen.insert($0).inserted && FileManager.default.isExecutableFile(atPath: $0) }
    }

    private func resolveNpm(_ path: String, searchPath: String) async throws -> NpmEnvironment {
        guard path.hasPrefix("/"), FileManager.default.isExecutableFile(atPath: path) else {
            throw CLIManagementError.failed(L("npm 路径无效，请选择可执行的 npm 文件。"))
        }
        let entry = URL(fileURLWithPath: path)
        let resolved = entry.resolvingSymlinksInPath()
        let node = entry.deletingLastPathComponent().appendingPathComponent("node").path
        guard FileManager.default.isExecutableFile(atPath: node) else {
            throw CLIManagementError.failed(L("npm 同目录下未找到 Node.js，请选择完整的 Node.js 安装。"))
        }
        var environment = ["PATH": entry.deletingLastPathComponent().path + ":" + searchPath]
        // Resolve version-manager shims once, then bind every operation to this
        // Node installation, independent of the App working directory.
        let actualNode = try await Self.command([node, "-p", "process.execPath"], environment: environment).trimmingCharacters(in: .whitespacesAndNewlines)
        guard actualNode.hasPrefix("/"), FileManager.default.isExecutableFile(atPath: actualNode) else {
            throw CLIManagementError.failed(L("无法确认 Node.js 的实际路径。"))
        }
        let nodeDirectory = URL(fileURLWithPath: actualNode).deletingLastPathComponent()
        environment["PATH"] = nodeDirectory.path + ":" + searchPath
        let nodeVersion = try await Self.command([actualNode, "-p", "process.versions.node"], environment: environment).trimmingCharacters(in: .whitespacesAndNewlines)
        guard (Int(nodeVersion.split(separator: ".").first ?? "") ?? 0) >= 18 else {
            throw CLIManagementError.failed(L("请使用 Node.js 18 或更新版本。"))
        }
        let bundledScript = nodeDirectory.deletingLastPathComponent().appendingPathComponent("lib/node_modules/npm/bin/npm-cli.js")
        let script = resolved.lastPathComponent == "npm-cli.js" ? resolved : bundledScript
        guard FileManager.default.fileExists(atPath: script.path) else {
            throw CLIManagementError.failed(L("无法找到与 Node.js 配套的 npm，请重新选择 npm 路径。"))
        }
        let command = [actualNode, script.path]
        let version = try await Self.command(command + ["--version"], environment: environment).trimmingCharacters(in: .whitespacesAndNewlines)
        let prefix = try await Self.command(command + ["prefix", "--global"], environment: environment).trimmingCharacters(in: .whitespacesAndNewlines)
        let modules = try await Self.command(command + ["root", "--global", "--prefix", prefix], environment: environment).trimmingCharacters(in: .whitespacesAndNewlines)
        guard Self.validVersion(version), prefix.hasPrefix("/"), modules.hasPrefix("/"), !prefix.contains("\n"), !modules.contains("\n") else {
            throw CLIManagementError.failed(L("npm 返回了无效的版本或安装目录。"))
        }
        return NpmEnvironment(npmPath: path, nodePath: actualNode, version: version, prefix: prefix, modules: modules, command: command, environment: environment)
    }

    private func refreshInstallation(_ npm: NpmEnvironment) async {
        installations = []
        let root = URL(fileURLWithPath: npm.modules).appendingPathComponent(Self.package)
        guard let data = try? Data(contentsOf: root.appendingPathComponent("package.json")),
              let package = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              package["name"] as? String == Self.package else { return }
        var environment = npm.environment
        environment["SMART_SEARCH_PACKAGE_ROOT"] = root.path
        environment["SMART_SEARCH_NODE_PATH"] = npm.nodePath
        var installed = CLIInstallation(id: root.path, executable: npm.nodePath, arguments: [root.appendingPathComponent("npm/bin/smart-search.js").path],
            version: package["version"] as? String ?? "", source: "npm", manager: npm.command, prefix: npm.prefix, environment: environment)
        // Never execute a legacy wrapper during detection: it may install Python.
        if package["smartSearchBinary"] as? Bool == true,
           let output = try? await Self.command([installed.executable] + installed.arguments + ["--desktop-capabilities"], environment: environment, timeout: 45),
           let info = try? JSONSerialization.jsonObject(with: Data(output.utf8)) as? [String: Any] {
            installed.compatible = info["product"] as? String == "smart-search" && info["desktop_protocol_version"] as? Int == 1 && info["version"] as? String == installed.version
        }
        installations = [installed]
    }

    func checkVersion() async {
        guard let npm, !busy, !checking else { return }
        checking = true
        update.attemptedAt = clock()
        saveUpdateState()
        defer { checking = false }
        do {
            let version: String
            let supportsBinary: Bool
            if let versionLoader { version = try await versionLoader(); supportsBinary = true }
            else {
                let output = try await Self.command(npm.command + ["view", Self.package + "@latest", "--json"], environment: npm.environment, timeout: 60)
                guard let metadata = try JSONSerialization.jsonObject(with: Data(output.utf8)) as? [String: Any] else {
                    throw CLIManagementError.failed(L("无法读取 CLI 发行信息，请稍后重试。"))
                }
                version = metadata["version"] as? String ?? ""
                supportsBinary = metadata["smartSearchBinary"] as? Bool == true
            }
            guard Self.validVersion(version) else { throw CLIManagementError.failed(L("无法确认 CLI 的最新版本。")) }
            update.latestVersion = version
            update.supportsBinary = supportsBinary
            update.checkedAt = clock()
            checkError = ""
            saveUpdateState()
        } catch { checkError = error.localizedDescription }
    }

    private func saveUpdateState() {
        if let data = try? JSONEncoder().encode(update) { preferences.set(data, forKey: "SmartSearchDesktop.cliUpdateState") }
    }

    func installOrRepair(expectedVersion: String) async throws {
        guard let npm, !busy, !checking else { throw CLIManagementError.failed(L("请先检测或指定 npm。")) }
        guard Self.validVersion(expectedVersion), expectedVersion == latestVersion, checkError.isEmpty else {
            throw CLIManagementError.failed(L("请先成功检查 CLI 更新。"))
        }
        guard latestSupportsBinary else { throw CLIManagementError.failed(L("npm 上尚未发布自带运行时的 CLI，请等待新版发布后再安装。")) }
        busy = true
        message = L("正在通过 npm 安装 CLI…")
        defer { busy = false }
        _ = try await Self.command(npm.command + ["install", "--global", "--prefix", npm.prefix, "--include=optional", Self.package + "@" + expectedVersion], environment: npm.environment, timeout: 1800)
        await refreshInstallation(npm)
        guard selected?.version == expectedVersion, selected?.compatible == true else {
            throw CLIManagementError.failed(L("CLI 已安装，但运行验证未通过，请更新或修复。"))
        }
        message = L("CLI 安装完成，正在重新连接。")
    }

    func uninstall() async throws {
        guard let selected, selected.canManage, !busy, !checking else { return }
        busy = true
        defer { busy = false }
        _ = try await Self.command(selected.manager + ["uninstall", "--global", "--prefix", selected.prefix, Self.package], environment: selected.environment, timeout: 600)
        installations = []
        message = L("CLI 已卸载，配置和 Skills 已保留。")
    }

    private static func validVersion(_ value: String) -> Bool { value.range(of: #"^[0-9]+\.[0-9]+\.[0-9]+$"#, options: .regularExpression) != nil }

    nonisolated static func command(_ arguments: [String], environment: [String: String] = [:], timeout: TimeInterval = 15) async throws -> String {
        try await Task.detached {
            let process = Process(), output = Pipe(), diagnostics = Pipe()
            process.executableURL = URL(fileURLWithPath: arguments[0])
            process.arguments = Array(arguments.dropFirst())
            process.currentDirectoryURL = FileManager.default.homeDirectoryForCurrentUser
            process.environment = ProcessInfo.processInfo.environment.merging(environment) { _, value in value }
            process.standardInput = FileHandle.nullDevice
            process.standardOutput = output
            process.standardError = diagnostics
            try process.run()
            let errors = Task.detached { diagnostics.fileHandleForReading.readDataToEndOfFile() }
            let stop = DispatchWorkItem { if process.isRunning { process.terminate() } }
            DispatchQueue.global().asyncAfter(deadline: .now() + timeout, execute: stop)
            let data = output.fileHandleForReading.readDataToEndOfFile()
            process.waitUntilExit()
            stop.cancel()
            let detail = String(decoding: await errors.value, as: UTF8.self)
            guard process.terminationStatus == 0 else { throw CLIManagementError.failed(L("CLI 操作未完成，请检查 npm 或重试。") + "\n" + detail.suffix(2000)) }
            return String(decoding: data, as: UTF8.self)
        }.value
    }
}
