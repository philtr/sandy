import Foundation
import Observation

@MainActor
@Observable
final class ServerConfigurationViewModel {
    enum Phase: Equatable {
        case idle
        case checking
        case failed(String)
    }

    var serverAddress: String
    private(set) var phase: Phase = .idle

    private let healthChecker: any ServerHealthChecking
    private let onConnect: @MainActor (ServerOrigin) -> Void
    private var connectionTask: Task<Void, Never>?

    init(
        serverAddress: String,
        healthChecker: any ServerHealthChecking,
        onConnect: @escaping @MainActor (ServerOrigin) -> Void
    ) {
        self.serverAddress = serverAddress
        self.healthChecker = healthChecker
        self.onConnect = onConnect
    }

    var isChecking: Bool { phase == .checking }

    var errorMessage: String? {
        guard case .failed(let message) = phase else { return nil }
        return message
    }

    func connect() {
        guard !isChecking else { return }
        let origin: ServerOrigin
        do {
            origin = try ServerOrigin(
                serverAddress,
                allowsInsecureLocalhost: Self.allowsInsecureLocalhost
            )
        } catch {
            phase = .failed(error.localizedDescription)
            return
        }

        phase = .checking
        connectionTask?.cancel()
        connectionTask = Task { [healthChecker, onConnect] in
            do {
                try await healthChecker.check(origin)
                guard !Task.isCancelled else { return }
                onConnect(origin)
            } catch is CancellationError {
                phase = .idle
            } catch {
                phase = .failed(error.localizedDescription)
            }
        }
    }

    private static var allowsInsecureLocalhost: Bool {
#if DEBUG
        true
#else
        false
#endif
    }
}
