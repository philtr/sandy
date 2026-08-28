import XCTest

final class SandyUITests: XCTestCase {
    override func setUpWithError() throws {
        continueAfterFailure = false
    }

    func testFreshLaunchShowsServerSetupAndInlineValidation() {
        let app = XCUIApplication()
        app.launchArguments = ["-reset-server-origin"]
        app.launch()

        XCTAssertTrue(app.staticTexts["Connect to Sandy"].waitForExistence(timeout: 3))
        XCTAssertTrue(app.textFields["server-address"].exists)
        app.textFields["server-address"].tap()
        app.textFields["server-address"].typeText("ftp://invalid.example.com")
        app.buttons["connect-server"].tap()

        XCTAssertTrue(app.staticTexts["connection-error"].waitForExistence(timeout: 2))
    }

    func testErrorScreenOffersRetryAndChangeServer() {
        let app = XCUIApplication()
        app.launchArguments = ["-ui-test-error-screen"]
        app.launch()

        XCTAssertTrue(app.buttons["retry-request"].waitForExistence(timeout: 3))
        app.buttons["retry-request"].tap()
        XCTAssertTrue(app.staticTexts["retry-requested"].waitForExistence(timeout: 2))

        app.buttons["change-server"].tap()
        XCTAssertTrue(app.staticTexts["Connect to Sandy"].waitForExistence(timeout: 2))
    }
}
