@preconcurrency import HotwireNative
import SwiftUI

@MainActor
enum SandyActions {
    static var changeServer: () -> Void = {}
}

struct SandyHotwireErrorView: @preconcurrency ErrorPresentableView {
    let error: HotwireNativeError
    let handler: ErrorPresenter.Handler?

    var body: some View {
        SandyErrorScreen(
            message: error.localizedDescription,
            retry: handler,
            changeServer: SandyActions.changeServer
        )
    }
}

struct SandyErrorScreen: View {
    let message: String
    let retry: (() -> Void)?
    let changeServer: () -> Void
    @State private var retryRequested = false

    var body: some View {
        ZStack {
            SandyTheme.backgroundGradient.ignoresSafeArea()
            VStack(spacing: 18) {
                Image(systemName: "wifi.exclamationmark")
                    .font(.system(size: 46, weight: .semibold))
                    .foregroundStyle(SandyTheme.attention)
                Text("Sandy is unavailable")
                    .font(.largeTitle.bold())
                    .foregroundStyle(.white)
                Text(message)
                    .multilineTextAlignment(.center)
                    .foregroundStyle(SandyTheme.muted)

                if let retry {
                    Button("Retry") {
                        retryRequested = true
                        retry()
                    }
                    .buttonStyle(.borderedProminent)
                    .tint(SandyTheme.primary)
                    .accessibilityIdentifier("retry-request")
                }

                Button("Change Server", action: changeServer)
                    .buttonStyle(.bordered)
                    .tint(SandyTheme.primary)
                    .accessibilityIdentifier("change-server")

                if retryRequested {
                    Text("Retry requested")
                        .font(.caption)
                        .foregroundStyle(SandyTheme.muted)
                        .accessibilityIdentifier("retry-requested")
                }
            }
            .frame(maxWidth: 520)
            .padding(32)
        }
    }
}
