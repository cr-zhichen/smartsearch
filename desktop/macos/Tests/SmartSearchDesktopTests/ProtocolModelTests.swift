import XCTest
@testable import SmartSearchDesktop

final class ProtocolModelTests: XCTestCase {
    func testNDJSONValueAndCatalogArgumentsKeepProtocolOrder() throws {
        let payload = Data("""
        {"id":"search","label":"Search","description":"","experimental":false,"fields":[
          {"name":"query","label":"Query","help":"","flags":[],"kind":"text","choices":[],"required":true,"multiple":false,"advanced":false},
          {"name":"tags","label":"Tags","help":"","flags":["--tag"],"kind":"text","choices":[],"required":false,"multiple":true,"advanced":true},
          {"name":"stream","label":"Stream","help":"","flags":["--stream"],"kind":"bool","choices":[],"required":false,"multiple":false}
        ]}
        """.utf8)
        let value = try JSONDecoder().decode(JSONValue.self, from: payload)
        let command = try XCTUnwrap(CommandCatalogEntry(value))

        XCTAssertEqual(
            CommandArgumentBuilder.arguments(
                for: command,
                values: ["query": "native search", "tags": "swift\nmacos"],
                booleans: ["stream": true]
            ),
            ["native search", "--tag", "swift", "--tag", "macos", "--stream"]
        )
        XCTAssertTrue(CommandArgumentBuilder.missingRequired(for: command, values: [:], booleans: [:]).contains { $0.name == "query" })
        XCTAssertFalse(command.fields[0].isAdvanced)
        XCTAssertTrue(command.fields[1].isAdvanced)
    }

    func testSensitiveOutputIsRedactedBeforeCopyOrExport() {
        let value: JSONValue = .object([
            "answer": .string("safe"),
            "api_key": .string("not-for-display"),
            "nested": .object(["authorization": .string("not-for-display")]),
        ])

        let redacted = value.redacted().objectValue
        XCTAssertEqual(redacted?["api_key"]?.stringValue, "***")
        XCTAssertEqual(redacted?["nested"]?["authorization"]?.stringValue, "***")
        XCTAssertEqual(redacted?["answer"]?.stringValue, "safe")
    }

    func testOwnedResultsStayAssociatedWithTheirRunAndRespectCapacity() {
        var store = OwnedRunResultStore(capacity: 2)
        store.register(runID: "search", kind: .business, label: "搜索")
        store.register(runID: "probe", kind: .providerTest, label: "测试草稿")
        store.register(runID: "skills", kind: .skillsInstall, label: "Skills 更新")

        XCTAssertEqual(store.cache(.object(["answer": .string("answer")]), for: "search")?.kind, .business)
        XCTAssertEqual(store.cache(.object(["error": .string("failed")]), for: "probe")?.kind, .providerTest)
        XCTAssertEqual(store.result(for: "search")?["answer"]?.stringValue, "answer")
        XCTAssertEqual(store.result(for: "probe")?["error"]?.stringValue, "failed")

        store.cache(.object(["ok": .bool(true)]), for: "skills")
        XCTAssertNil(store.result(for: "search"))
        XCTAssertNil(store.descriptor(for: "search"))
        XCTAssertEqual(store.result(for: "probe")?["error"]?.stringValue, "failed")
        XCTAssertEqual(store.result(for: "skills")?["ok"]?.boolValue, true)
    }

    func testStateKeepsCooldownAndDraftChecksAsSeparateFacts() throws {
        let snapshot: JSONValue = .object([
            "provider_health": .object([
                "providers": .array([
                    .object(["provider": .string("exa"), "state": .string("closed"), "configured": .bool(true)]),
                ]),
            ]),
            "provider_checks": .object([
                "exa": .object([
                    "status": .string("ok"),
                    "checked_at": .number(1_700_000_000),
                    "source": .string("app"),
                    "scope": .string("draft"),
                ]),
            ]),
        ])
        let state = try XCTUnwrap(DesktopState(snapshot))

        XCTAssertEqual(state.providerHealth?["providers"]?.arrayValue?.first?["state"]?.stringValue, "closed")
        XCTAssertEqual(state.providerChecks?["exa"]?["scope"]?.stringValue, "draft")
        XCTAssertEqual(state.providerChecks?["exa"]?["source"]?.stringValue, "app")
    }
}
