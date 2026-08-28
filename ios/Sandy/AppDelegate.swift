@preconcurrency import HotwireNative
import UIKit

@main
final class AppDelegate: UIResponder, UIApplicationDelegate {
    func application(
        _ application: UIApplication,
        didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]? = nil
    ) -> Bool {
        SandyTheme.configureAppearance()
        configureHotwire()
        return true
    }

    func application(
        _ application: UIApplication,
        configurationForConnecting connectingSceneSession: UISceneSession,
        options: UIScene.ConnectionOptions
    ) -> UISceneConfiguration {
        let configuration = UISceneConfiguration(
            name: "Default Configuration",
            sessionRole: connectingSceneSession.role
        )
        configuration.delegateClass = SceneDelegate.self
        return configuration
    }

    @MainActor
    private func configureHotwire() {
        let version = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "1.0"
        let build = Bundle.main.object(forInfoDictionaryKey: "CFBundleVersion") as? String ?? "1"
        Hotwire.config.applicationUserAgentPrefix = "Sandy/\(version) (\(build));"
        Hotwire.config.backButtonDisplayMode = .minimal
        Hotwire.config.animateReplaceActions = true
        Hotwire.config.makeCustomErrorView = { error, handler in
            SandyHotwireErrorView(error: error, handler: handler)
        }
#if DEBUG
        Hotwire.config.debugLoggingEnabled = true
#endif
    }
}
