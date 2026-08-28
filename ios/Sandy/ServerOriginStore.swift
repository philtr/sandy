import Foundation

final class ServerOriginStore: @unchecked Sendable {
    static let storageKey = "serverOrigin"

    private let defaults: UserDefaults

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
    }

    func load() -> ServerOrigin? {
        guard let value = defaults.string(forKey: Self.storageKey) else { return nil }
        return try? ServerOrigin(value, allowsInsecureLocalhost: Self.allowsInsecureLocalhost)
    }

    func save(_ origin: ServerOrigin) {
        defaults.set(origin.url.absoluteString, forKey: Self.storageKey)
    }

    func clear() {
        defaults.removeObject(forKey: Self.storageKey)
    }

    private static var allowsInsecureLocalhost: Bool {
#if DEBUG
        true
#else
        false
#endif
    }
}
