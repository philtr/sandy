import Foundation
import Testing
@testable import Sandy

struct ServerOriginTests {
    @Test func normalizesHTTPSOrigins() throws {
        #expect(try ServerOrigin("sandy.example.com").url.absoluteString == "https://sandy.example.com")
        #expect(try ServerOrigin(" HTTPS://SANDY.EXAMPLE.COM:8443/ ").url.absoluteString == "https://sandy.example.com:8443")
    }

    @Test(arguments: [
        "ftp://sandy.example.com",
        "https://user:password@sandy.example.com",
        "https://sandy.example.com/family",
        "https://sandy.example.com?family=one",
        "https://sandy.example.com#settings",
        "not a host"
    ])
    func rejectsNonOriginInputs(_ input: String) {
        #expect(throws: ServerOrigin.ValidationError.self) {
            try ServerOrigin(input)
        }
    }

    @Test func onlyAllowsInsecureLocalDevelopmentWhenExplicitlyEnabled() throws {
        #expect(throws: ServerOrigin.ValidationError.self) {
            try ServerOrigin("http://localhost:3000")
        }
        #expect(try ServerOrigin("http://localhost:3000", allowsInsecureLocalhost: true).url.absoluteString == "http://localhost:3000")
        #expect(try ServerOrigin("http://127.0.0.1:3000", allowsInsecureLocalhost: true).url.absoluteString == "http://127.0.0.1:3000")
        #expect(throws: ServerOrigin.ValidationError.self) {
            try ServerOrigin("http://sandy.example.com", allowsInsecureLocalhost: true)
        }
    }

    @Test func appendsPathsWithoutLosingTheOrigin() throws {
        let origin = try ServerOrigin("https://sandy.example.com:8443")
        #expect(origin.appending(path: "up").absoluteString == "https://sandy.example.com:8443/up")
    }
}
