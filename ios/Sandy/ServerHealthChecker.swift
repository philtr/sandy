import Foundation

struct HealthResponse: Sendable {
    let statusCode: Int
    let finalURL: URL
}

protocol HealthTransporting: Sendable {
    func fetch(_ url: URL, timeout: TimeInterval) async throws -> HealthResponse
}

struct URLSessionHealthTransport: HealthTransporting {
    func fetch(_ url: URL, timeout: TimeInterval) async throws -> HealthResponse {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = timeout
        configuration.timeoutIntervalForResource = timeout
        let session = URLSession(configuration: configuration)
        defer { session.invalidateAndCancel() }

        var request = URLRequest(url: url)
        request.httpMethod = "GET"
        request.cachePolicy = .reloadIgnoringLocalAndRemoteCacheData
        request.timeoutInterval = timeout
        let (_, response) = try await session.data(for: request)
        guard let response = response as? HTTPURLResponse, let finalURL = response.url else {
            throw ServerHealthChecker.CheckError.invalidResponse
        }
        return HealthResponse(statusCode: response.statusCode, finalURL: finalURL)
    }
}

protocol ServerHealthChecking: Sendable {
    func check(_ origin: ServerOrigin) async throws
}

struct ServerHealthChecker: ServerHealthChecking {
    enum CheckError: LocalizedError, Equatable {
        case invalidResponse
        case unhealthyStatus(Int)
        case crossOriginRedirect
        case timedOut
        case transport(String)

        var errorDescription: String? {
            switch self {
            case .invalidResponse:
                "The server returned an invalid health response."
            case .unhealthyStatus(let status):
                "The server health check returned HTTP \(status)."
            case .crossOriginRedirect:
                "The server health check redirected to a different origin."
            case .timedOut:
                "The server did not respond within ten seconds."
            case .transport(let message):
                "Could not connect to the Sandy server: \(message)"
            }
        }
    }

    private let transport: any HealthTransporting
    private let timeout: TimeInterval

    init(
        transport: any HealthTransporting = URLSessionHealthTransport(),
        timeout: TimeInterval = 10
    ) {
        self.transport = transport
        self.timeout = timeout
    }

    func check(_ origin: ServerOrigin) async throws {
        let response: HealthResponse
        do {
            response = try await transport.fetch(origin.appending(path: "up"), timeout: timeout)
        } catch let error as URLError where error.code == .timedOut {
            throw CheckError.timedOut
        } catch let error as CheckError {
            throw error
        } catch {
            throw CheckError.transport(error.localizedDescription)
        }

        guard origin.hasSameOrigin(as: response.finalURL) else {
            throw CheckError.crossOriginRedirect
        }
        guard (200..<300).contains(response.statusCode) else {
            throw CheckError.unhealthyStatus(response.statusCode)
        }
    }
}
