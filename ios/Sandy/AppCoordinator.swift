@preconcurrency import HotwireNative
import SwiftUI
import UIKit

@MainActor
final class AppCoordinator: NSObject {
    private let window: UIWindow
    private let store: ServerOriginStore
    private let healthChecker: any ServerHealthChecking
    private var navigator: Navigator?

    private lazy var connectionController = ServerConnectionController(store: store) { [weak self] origin in
        self?.startNavigator(at: origin)
    }

    init(
        window: UIWindow,
        store: ServerOriginStore = ServerOriginStore(),
        healthChecker: any ServerHealthChecking = ServerHealthChecker()
    ) {
        self.window = window
        self.store = store
        self.healthChecker = healthChecker
        super.init()
        SandyActions.changeServer = { [weak self] in
            self?.routeToServerConfiguration()
        }
    }

    func start() {
#if DEBUG
        let arguments = ProcessInfo.processInfo.arguments
        if arguments.contains("-reset-server-origin") {
            store.clear()
        }
        if arguments.contains("-ui-test-error-screen") {
            showErrorPreview()
            return
        }
#endif

        if let origin = store.load() {
            startNavigator(at: origin)
        } else {
            showServerConfiguration(allowsCancellation: false)
        }
    }

    private func startNavigator(at origin: ServerOrigin) {
        let localConfiguration = Bundle.main.url(forResource: "ios_v1", withExtension: "json")!
        Hotwire.loadPathConfiguration(from: [
            .file(localConfiguration),
            .server(origin.appending(path: "configurations/ios_v1.json"))
        ])

        let navigator = Navigator(
            configuration: .init(name: "main", startLocation: origin.url),
            delegate: self
        )
        self.navigator = navigator
        window.rootViewController = navigator.rootViewController
        navigator.start()
    }

    private func makeServerConfigurationController(
        allowsCancellation: Bool,
        onCancel: (() -> Void)? = nil
    ) -> ServerConfigurationViewController {
        ServerConfigurationViewController(
            initialAddress: store.load()?.url.absoluteString ?? "",
            allowsCancellation: allowsCancellation,
            healthChecker: healthChecker,
            onConnect: { [weak self] origin in
                self?.connectionController.connect(to: origin)
            },
            onCancel: onCancel
        )
    }

    private func showServerConfiguration(allowsCancellation: Bool) {
        navigator = nil
        let controller = makeServerConfigurationController(allowsCancellation: allowsCancellation)
        let navigationController = UINavigationController(rootViewController: controller)
        window.rootViewController = navigationController
    }

    private func routeToServerConfiguration() {
        guard let origin = store.load(), let navigator else {
            showServerConfiguration(allowsCancellation: false)
            return
        }

        var components = URLComponents(url: origin.appending(path: "settings"), resolvingAgainstBaseURL: false)!
        components.queryItems = [URLQueryItem(name: "native_screen", value: "server")]
        navigator.route(components.url!)
    }

#if DEBUG
    private func showErrorPreview() {
        store.clear()
        let screen = SandyErrorScreen(
            message: "Sandy could not reach the configured server.",
            retry: {},
            changeServer: { [weak self] in
                self?.showServerConfiguration(allowsCancellation: false)
            }
        )
        window.rootViewController = UIHostingController(rootView: screen)
    }
#endif
}

extension AppCoordinator: @preconcurrency NavigatorDelegate {
    func handle(proposal: VisitProposal, from navigator: Navigator) -> ProposalResult {
        guard proposal.viewController == ServerConfigurationViewController.pathConfigurationIdentifier else {
            return .accept
        }

        return .acceptCustom(makeServerConfigurationController(
            allowsCancellation: true,
            onCancel: { navigator.pop() }
        ))
    }
}
