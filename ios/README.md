# Sandy for iOS

Sandy's iPhone and iPad app is a Hotwire Native shell around the Rails parent
interface. It targets iOS 18 and uses the bundle identifier `net.rdln.sandy`.

## Development

Open `Sandy.xcodeproj` in Xcode 26 or newer. Hotwire Native 1.3.0 is pinned in
the project and `Package.resolved`.

On a fresh launch, enter the HTTPS origin of a running Sandy deployment. Debug
builds also accept `http://localhost:3000` and `http://127.0.0.1:3000` for a
Rails server running on the development Mac. The app checks `<origin>/up`
before saving the origin.

Run the full test suite with an available simulator name:

```sh
cd ios
xcodebuild test \
  -project Sandy.xcodeproj \
  -scheme Sandy \
  -destination 'platform=iOS Simulator,name=iPhone 17e,OS=latest' \
  CODE_SIGNING_ALLOWED=NO
```

The committed Xcode project is generated from `project.yml`. When changing the
project structure, regenerate it with XcodeGen 2.46.0 and commit both the spec
and generated project.

For a development device, select the Sandy target in Xcode, choose the local
Apple Developer team with automatic signing, and run. Do not commit a team ID,
certificate, provisioning profile, or App Store Connect credential.

## Internal TestFlight releases

Create the `net.rdln.sandy` identifier and Sandy app record in App Store
Connect, then configure a protected GitHub environment named `testflight` with:

Variables:

- `APPLE_TEAM_ID`
- `IOS_PROVISIONING_PROFILE_NAME`

Secrets:

- `APP_STORE_CONNECT_KEY_ID`
- `APP_STORE_CONNECT_ISSUER_ID`
- `APP_STORE_CONNECT_API_KEY_BASE64` — base64-encoded `.p8` private key
- `IOS_DISTRIBUTION_CERTIFICATE_BASE64` — base64-encoded distribution `.p12`
- `IOS_DISTRIBUTION_CERTIFICATE_PASSWORD`
- `IOS_PROVISIONING_PROFILE_BASE64` — base64-encoded App Store profile

The App Store Connect key needs permission to upload builds. The certificate
and provisioning profile must belong to `APPLE_TEAM_ID`, and the profile name
must exactly match `IOS_PROVISIONING_PROFILE_NAME`.

Push a stable semantic-version tag to test and upload an internal-only build:

```sh
git tag ios-v1.0.0
git push origin ios-v1.0.0
```

The tag supplies the marketing version. GitHub's workflow run number and retry
attempt supply a unique build number. External TestFlight distribution and
public App Store submission are intentionally not configured.

Before the first upload, archive locally and choose **Generate Privacy Report**
in Xcode Organizer. Confirm that it matches `PrivacyInfo.xcprivacy` and the app's
App Store Connect privacy answers. Deploy the Rails release containing
`/configurations/ios_v1.json` and the native page adaptations before inviting
testers.

## Acceptance checklist

- Configure a fresh install against the production HTTPS origin.
- Sign in, select each parent profile, terminate the app, and confirm the Rails
  session persists after relaunch.
- Grant and revoke time; unlock and lock launcher editing; generate a join code;
  update settings; archive and unenroll a test PC; and inspect history and
  diagnostics.
- Exercise Turbo confirmations, pull-to-refresh, native back gestures, external
  links, network loss and Retry, and Change Sandy Server.
- Rotate an iPhone and iPad running iOS 18 or newer and check both size classes.
- Install the tagged build from the internal TestFlight group.
