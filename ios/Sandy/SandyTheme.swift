import SwiftUI
import UIKit

enum SandyTheme {
    static let page = UIColor(red: 9 / 255, green: 14 / 255, blue: 22 / 255, alpha: 1)
    static let primaryUIColor = UIColor(red: 103 / 255, green: 168 / 255, blue: 1, alpha: 1)

    static let primary = Color(red: 103 / 255, green: 168 / 255, blue: 1)
    static let onPrimary = Color(red: 7 / 255, green: 16 / 255, blue: 29 / 255)
    static let muted = Color(red: 184 / 255, green: 191 / 255, blue: 204 / 255)
    static let attention = Color(red: 244 / 255, green: 185 / 255, blue: 66 / 255)
    static let danger = Color(red: 1, green: 132 / 255, blue: 153 / 255)
    static let surface = Color(red: 22 / 255, green: 29 / 255, blue: 39 / 255).opacity(0.94)
    static let border = Color(red: 113 / 255, green: 128 / 255, blue: 143 / 255).opacity(0.72)
    static let backgroundGradient = RadialGradient(
        colors: [Color(red: 23 / 255, green: 58 / 255, blue: 106 / 255), Color(uiColor: page)],
        center: .top,
        startRadius: 20,
        endRadius: 720
    )

    @MainActor
    static func configureAppearance() {
        let appearance = UINavigationBarAppearance()
        appearance.configureWithOpaqueBackground()
        appearance.backgroundColor = page
        appearance.titleTextAttributes = [.foregroundColor: UIColor.white]
        appearance.largeTitleTextAttributes = [.foregroundColor: UIColor.white]
        appearance.shadowColor = primaryUIColor.withAlphaComponent(0.16)

        let navigationBar = UINavigationBar.appearance()
        navigationBar.standardAppearance = appearance
        navigationBar.scrollEdgeAppearance = appearance
        navigationBar.compactAppearance = appearance
        navigationBar.tintColor = primaryUIColor
    }
}
