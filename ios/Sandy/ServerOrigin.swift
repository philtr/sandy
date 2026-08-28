import Foundation

struct ServerOrigin: Equatable, Sendable {
    enum ValidationError: LocalizedError, Equatable {
        case empty
        case invalid
        case missingHost
        case unsupportedScheme
        case insecureConnection
        case containsCredentials
        case containsPath
        case containsQuery
        case containsFragment

        var errorDescription: String? {
            switch self {
            case .empty:
                "Enter your Sandy server address."
            case .invalid, .missingHost:
                "Enter a valid server address, such as https://sandy.example.com."
            case .unsupportedScheme:
                "The server address must use HTTPS."
            case .insecureConnection:
                "Sandy requires HTTPS outside local development."
            case .containsCredentials:
                "Remove the username and password from the server address."
            case .containsPath:
                "Enter only the server origin, without a path."
            case .containsQuery:
                "Enter only the server origin, without query parameters."
            case .containsFragment:
                "Enter only the server origin, without a fragment."
            }
        }
    }

    let url: URL

    init(_ input: String, allowsInsecureLocalhost: Bool = false) throws {
        let trimmed = input.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { throw ValidationError.empty }
        let candidate = trimmed.contains("://") ? trimmed : "https://\(trimmed)"
        guard var components = URLComponents(string: candidate) else {
            throw ValidationError.invalid
        }

        guard let rawScheme = components.scheme else { throw ValidationError.unsupportedScheme }
        let scheme = rawScheme.lowercased()
        guard let rawHost = components.host, !rawHost.isEmpty else { throw ValidationError.missingHost }
        let host = rawHost.lowercased()
        guard components.user == nil, components.password == nil else {
            throw ValidationError.containsCredentials
        }
        guard components.path.isEmpty || components.path == "/" else {
            throw ValidationError.containsPath
        }
        guard components.query == nil else { throw ValidationError.containsQuery }
        guard components.fragment == nil else { throw ValidationError.containsFragment }

        let isLocalhost = host == "localhost" || host == "127.0.0.1"
        if scheme != "https" {
            guard scheme == "http" else { throw ValidationError.unsupportedScheme }
            guard allowsInsecureLocalhost && isLocalhost else {
                throw ValidationError.insecureConnection
            }
        }

        components.scheme = scheme
        components.host = host
        components.path = ""
        guard let normalizedURL = components.url else { throw ValidationError.invalid }
        url = normalizedURL
    }

    func appending(path: String) -> URL {
        url.appending(path: path.trimmingCharacters(in: CharacterSet(charactersIn: "/")))
    }

    func hasSameOrigin(as other: URL) -> Bool {
        guard let otherComponents = URLComponents(url: other, resolvingAgainstBaseURL: false),
              let ownComponents = URLComponents(url: url, resolvingAgainstBaseURL: false) else {
            return false
        }
        return ownComponents.scheme?.lowercased() == otherComponents.scheme?.lowercased()
            && ownComponents.host?.lowercased() == otherComponents.host?.lowercased()
            && effectivePort(ownComponents) == effectivePort(otherComponents)
    }

    private func effectivePort(_ components: URLComponents) -> Int? {
        if let port = components.port { return port }
        return components.scheme?.lowercased() == "https" ? 443 : 80
    }
}
