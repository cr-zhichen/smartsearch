import AppKit
import SwiftUI

// Codex Tweaks: MainWindowView, UpdateView and BackendPresentationTokens.
// Keep the reference layout values local; no dependency on its backend contract.
enum DesktopMetrics {
    static let contentWidth: CGFloat = 920
    static let pagePadding: CGFloat = 24
    static let sectionSpacing: CGFloat = 24
    static let cardPadding: CGFloat = 16
    static let cardRadius: CGFloat = 12
}

enum DesktopAppearance {
    // White in light appearance; use the matching native surface in dark appearance.
    static let contentBackground = Color(nsColor: .textBackgroundColor)
}

struct DesktopSplitView<Leading: View, Detail: View>: View {
    let leadingWidths: ClosedRange<CGFloat>
    let idealLeadingWidth: CGFloat
    let detailMinimumWidth: CGFloat
    let leading: Leading
    let detail: Detail

    init(leadingWidths: ClosedRange<CGFloat> = 176...280, idealLeadingWidth: CGFloat = 208,
         detailMinimumWidth: CGFloat = 400,
         @ViewBuilder leading: () -> Leading, @ViewBuilder detail: () -> Detail) {
        self.leadingWidths = leadingWidths
        self.idealLeadingWidth = idealLeadingWidth
        self.detailMinimumWidth = detailMinimumWidth
        self.leading = leading()
        self.detail = detail()
    }

    var body: some View {
        HSplitView {
            leading
                .frame(minWidth: leadingWidths.lowerBound, idealWidth: idealLeadingWidth,
                       maxWidth: leadingWidths.upperBound, maxHeight: .infinity)
            detail
                .frame(minWidth: detailMinimumWidth, maxWidth: .infinity, maxHeight: .infinity)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(DesktopAppearance.contentBackground)
    }
}

struct DesktopPage<Content: View>: View {
    let title: String
    let subtitle: String
    let padding: CGFloat
    let content: Content

    init(_ title: String, subtitle: String, padding: CGFloat = DesktopMetrics.pagePadding,
         @ViewBuilder content: () -> Content) {
        self.title = title
        self.subtitle = subtitle
        self.padding = padding
        self.content = content()
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: DesktopMetrics.sectionSpacing) {
                VStack(alignment: .leading, spacing: 7) {
                    Text(title).font(.title2.weight(.semibold))
                    Text(subtitle)
                        .font(.body)
                        .foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                content
            }
            .frame(maxWidth: DesktopMetrics.contentWidth, alignment: .leading)
            .padding(padding)
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
            if let title { Text(title).font(.headline) }
            content
        }
        .desktopPanel()
    }
}

struct DesktopGroupBoxStyle: GroupBoxStyle {
    func makeBody(configuration: Configuration) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            configuration.label.font(.headline)
            configuration.content
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

struct WholeRowDisclosureStyle: DisclosureGroupStyle {
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    func makeBody(configuration: Configuration) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            Button {
                withAnimation(reduceMotion ? nil : .easeInOut(duration: 0.18)) {
                    configuration.isExpanded.toggle()
                }
            } label: {
                HStack(spacing: 8) {
                    configuration.label
                    Spacer(minLength: 8)
                    Image(systemName: "chevron.right")
                        .font(.caption.weight(.semibold))
                        .foregroundStyle(.secondary)
                        .rotationEffect(.degrees(configuration.isExpanded ? 90 : 0))
                }
                .frame(maxWidth: .infinity, minHeight: 30, alignment: .leading)
                .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .accessibilityValue(configuration.isExpanded ? L("已展开") : L("已收起"))
            if configuration.isExpanded { configuration.content }
        }
    }
}

struct DetailSheet<Content: View>: View {
    let title: String
    let content: Content
    @Environment(\.dismiss) private var dismiss

    init(_ title: String, @ViewBuilder content: () -> Content) {
        self.title = title
        self.content = content()
    }

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Text(title).font(.headline)
                Spacer()
                Button(L("完成")) { dismiss() }.keyboardShortcut(.defaultAction)
            }.padding(20)
            Divider()
            ScrollView {
                VStack(alignment: .leading, spacing: 16) { content }
                    .frame(maxWidth: .infinity, alignment: .leading).padding(20)
            }
        }
        .frame(width: 560, height: 480)
        .disclosureGroupStyle(WholeRowDisclosureStyle())
        .toggleStyle(.switch)
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
