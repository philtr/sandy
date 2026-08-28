@preconcurrency import HotwireNative
import SwiftUI

final class ServerConfigurationViewController: UIHostingController<ServerConfigurationView>, @preconcurrency PathConfigurationIdentifiable {
    static let pathConfigurationIdentifier = "server_configuration"

    @MainActor
    init(
        initialAddress: String,
        allowsCancellation: Bool,
        healthChecker: any ServerHealthChecking,
        onConnect: @escaping @MainActor (ServerOrigin) -> Void,
        onCancel: (() -> Void)?
    ) {
        let model = ServerConfigurationViewModel(
            serverAddress: initialAddress,
            healthChecker: healthChecker,
            onConnect: onConnect
        )
        super.init(rootView: ServerConfigurationView(
            model: model,
            allowsCancellation: allowsCancellation,
            onCancel: onCancel
        ))
        view.backgroundColor = SandyTheme.page
    }

    @MainActor @preconcurrency required dynamic init?(coder aDecoder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }
}
