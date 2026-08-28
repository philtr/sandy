import Foundation
import Testing
@testable import Sandy

struct ServerOriginStoreTests {
    @Test func savesLoadsAndClearsTheOrigin() throws {
        let suiteName = "ServerOriginStoreTests.\(UUID().uuidString)"
        let defaults = try #require(UserDefaults(suiteName: suiteName))
        defer { defaults.removePersistentDomain(forName: suiteName) }
        let store = ServerOriginStore(defaults: defaults)
        let origin = try ServerOrigin("https://sandy.example.com")

        #expect(store.load() == nil)
        store.save(origin)
        #expect(store.load() == origin)
        store.clear()
        #expect(store.load() == nil)
    }

    @Test func ignoresInvalidPersistedValues() throws {
        let suiteName = "ServerOriginStoreTests.\(UUID().uuidString)"
        let defaults = try #require(UserDefaults(suiteName: suiteName))
        defer { defaults.removePersistentDomain(forName: suiteName) }
        defaults.set("http://public.example.com", forKey: ServerOriginStore.storageKey)

        #expect(ServerOriginStore(defaults: defaults).load() == nil)
    }
}
