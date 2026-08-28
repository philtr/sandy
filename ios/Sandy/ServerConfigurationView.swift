import SwiftUI

struct ServerConfigurationView: View {
    @State private var model: ServerConfigurationViewModel
    let allowsCancellation: Bool
    let onCancel: (() -> Void)?

    init(
        model: ServerConfigurationViewModel,
        allowsCancellation: Bool,
        onCancel: (() -> Void)?
    ) {
        _model = State(initialValue: model)
        self.allowsCancellation = allowsCancellation
        self.onCancel = onCancel
    }

    var body: some View {
        ZStack {
            SandyTheme.backgroundGradient.ignoresSafeArea()
            ScrollView {
                VStack(spacing: 24) {
                    Image("LaunchLogo")
                        .resizable()
                        .scaledToFit()
                        .frame(width: 112, height: 112)
                        .clipShape(.rect(cornerRadius: 24))
                        .shadow(color: SandyTheme.primary.opacity(0.35), radius: 24)

                    VStack(spacing: 8) {
                        Text("Connect to Sandy")
                            .font(.largeTitle.bold())
                            .foregroundStyle(.white)
                        Text("Enter the HTTPS address for your household’s Sandy server.")
                            .multilineTextAlignment(.center)
                            .foregroundStyle(SandyTheme.muted)
                    }

                    VStack(alignment: .leading, spacing: 8) {
                        Text("Server address")
                            .font(.headline)
                            .foregroundStyle(.white)
                        TextField("https://sandy.example.com", text: $model.serverAddress)
                            .textContentType(.URL)
                            .keyboardType(.URL)
                            .textInputAutocapitalization(.never)
                            .autocorrectionDisabled()
                            .submitLabel(.go)
                            .onSubmit(model.connect)
                            .padding(14)
                            .background(SandyTheme.surface)
                            .foregroundStyle(.white)
                            .clipShape(.rect(cornerRadius: 12))
                            .overlay {
                                RoundedRectangle(cornerRadius: 12)
                                    .stroke(SandyTheme.border, lineWidth: 1)
                            }
                            .accessibilityIdentifier("server-address")

                        if let errorMessage = model.errorMessage {
                            Text(errorMessage)
                                .font(.callout.weight(.semibold))
                                .foregroundStyle(SandyTheme.danger)
                                .accessibilityIdentifier("connection-error")
                        }
                    }

                    Button(action: model.connect) {
                        HStack {
                            if model.isChecking { ProgressView().tint(SandyTheme.onPrimary) }
                            Text(model.isChecking ? "Checking Server…" : "Connect")
                                .fontWeight(.bold)
                        }
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 14)
                    }
                    .buttonStyle(.plain)
                    .background(SandyTheme.primary)
                    .foregroundStyle(SandyTheme.onPrimary)
                    .clipShape(.rect(cornerRadius: 12))
                    .disabled(model.isChecking)
                    .accessibilityIdentifier("connect-server")
                }
                .frame(maxWidth: 560)
                .padding(28)
                .frame(maxWidth: .infinity)
            }
            .scrollDismissesKeyboard(.interactively)
        }
        .navigationTitle(allowsCancellation ? "Server" : "Sandy")
        .navigationBarTitleDisplayMode(.inline)
        .toolbar {
            if allowsCancellation, let onCancel {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel", action: onCancel)
                }
            }
        }
    }
}
