import Foundation

@MainActor
final class ServerConnectionController {
    private let store: ServerOriginStore
    private let onConnect: @MainActor (ServerOrigin) -> Void

    init(
        store: ServerOriginStore,
        onConnect: @escaping @MainActor (ServerOrigin) -> Void
    ) {
        self.store = store
        self.onConnect = onConnect
    }

    func connect(to origin: ServerOrigin) {
        store.save(origin)
        onConnect(origin)
    }
}
