import AppKit
import SwiftUI

// Codex Tweaks: MainWindowView, UpdateView and BackendPresentationTokens.
// Keep the reference layout values local; no dependency on its backend contract.
enum DesktopMetrics {
    static let contentWidth: CGFloat = 1120
    static let pagePadding: CGFloat = 32
    static let sectionSpacing: CGFloat = 28
    static let cardPadding: CGFloat = 20
    static let cardRadius: CGFloat = 14
}

enum DesktopAppearance {
    // White in light appearance; use the matching native surface in dark appearance.
    static let contentBackground = Color(nsColor: .textBackgroundColor)
}

struct DesktopPage<Content: View>: View {
    let title: String
    let subtitle: String
    let content: Content

    init(_ title: String, subtitle: String, @ViewBuilder content: () -> Content) {
        self.title = title
        self.subtitle = subtitle
        self.content = content()
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: DesktopMetrics.sectionSpacing) {
                VStack(alignment: .leading, spacing: 7) {
                    Text(title).font(.largeTitle.weight(.semibold))
                    Text(subtitle)
                        .font(.body)
                        .foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                content
            }
            .frame(maxWidth: DesktopMetrics.contentWidth, alignment: .leading)
            .padding(DesktopMetrics.pagePadding)
            .frame(maxWidth: .infinity, alignment: .topLeading)
        }
        .background(DesktopAppearance.contentBackground)
        .scrollIndicators(.visible)
        .textFieldStyle(.roundedBorder)
    }
}

struct DesktopPanel<Content: View>: View {
    let title: String?
    let content: Content

    init(_ title: String? = nil, @ViewBuilder content: () -> Content) {
        self.title = title
        self.content = content()
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            if let title { Text(title).font(.title2.weight(.semibold)) }
            content
        }
        .desktopPanel()
    }
}

struct DesktopGroupBoxStyle: GroupBoxStyle {
    func makeBody(configuration: Configuration) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            configuration.label.font(.title2.weight(.semibold))
            configuration.content
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

struct DesktopDisclosure<Content: View>: View {
    let title: String
    let content: Content

    init(_ title: String, @ViewBuilder content: () -> Content) {
        self.title = title
        self.content = content()
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Divider()
            DisclosureGroup {
                VStack(alignment: .leading, spacing: 12) { content }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(.top, 12)
            } label: {
                Text(title).font(.title2.weight(.semibold))
            }
        }
    }
}

extension View {
    func desktopPanel() -> some View {
        padding(DesktopMetrics.cardPadding)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(DesktopAppearance.contentBackground)
            .clipShape(RoundedRectangle(cornerRadius: DesktopMetrics.cardRadius, style: .continuous))
            .overlay {
                RoundedRectangle(cornerRadius: DesktopMetrics.cardRadius, style: .continuous)
                    .stroke(Color(nsColor: .separatorColor).opacity(0.7), lineWidth: 1)
            }
    }
}
