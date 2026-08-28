import Foundation
import Testing
@testable import Sandy

struct ServerHealthCheckerTests {
    @Test func acceptsSuccessfulSameOriginHealthResponse() async throws {
        let origin = try ServerOrigin("https://sandy.example.com")
        let checker = ServerHealthChecker(transport: StubHealthTransport(
            result: .success(.init(statusCode: 200, finalURL: origin.appending(path: "up")))
        ))

        try await checker.check(origin)
    }

    @Test func rejectsNonSuccessResponses() async throws {
        let origin = try ServerOrigin("https://sandy.example.com")
        let checker = ServerHealthChecker(transport: StubHealthTransport(
            result: .success(.init(statusCode: 503, finalURL: origin.appending(path: "up")))
        ))

        await #expect(throws: ServerHealthChecker.CheckError.unhealthyStatus(503)) {
            try await checker.check(origin)
        }
    }

    @Test func rejectsCrossOriginRedirects() async throws {
        let origin = try ServerOrigin("https://sandy.example.com")
        let checker = ServerHealthChecker(transport: StubHealthTransport(
            result: .success(.init(statusCode: 200, finalURL: URL(string: "https://login.example.net/up")!))
        ))

        await #expect(throws: ServerHealthChecker.CheckError.crossOriginRedirect) {
            try await checker.check(origin)
        }
    }

    @Test func reportsTimeouts() async throws {
        let origin = try ServerOrigin("https://sandy.example.com")
        let checker = ServerHealthChecker(transport: StubHealthTransport(
            result: .failure(URLError(.timedOut))
        ))

        await #expect(throws: ServerHealthChecker.CheckError.timedOut) {
            try await checker.check(origin)
        }
    }
}

private struct StubHealthTransport: HealthTransporting {
    let result: Result<HealthResponse, Error>

    func fetch(_ url: URL, timeout: TimeInterval) async throws -> HealthResponse {
        try result.get()
    }
}
