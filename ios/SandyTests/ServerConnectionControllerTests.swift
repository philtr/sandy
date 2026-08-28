import Foundation
import Testing
@testable import Sandy

@MainActor
struct ServerConnectionControllerTests {
    @Test func savesAndRestartsForInitialAndReplacementOrigins() throws {
        let suiteName = "ServerConnectionControllerTests.\(UUID().uuidString)"
        let defaults = try #require(UserDefaults(suiteName: suiteName))
        defer { defaults.removePersistentDomain(forName: suiteName) }
        let store = ServerOriginStore(defaults: defaults)
        var connected: [ServerOrigin] = []
        let controller = ServerConnectionController(store: store) { connected.append($0) }
        let first = try ServerOrigin("https://one.example.com")
        let second = try ServerOrigin("https://two.example.com")

        controller.connect(to: first)
        controller.connect(to: second)

        #expect(store.load() == second)
        #expect(connected == [first, second])
    }
}
