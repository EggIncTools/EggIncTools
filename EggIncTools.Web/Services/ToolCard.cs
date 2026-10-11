using EggIdentity.UI.Suite;

namespace EggIncTools.Web.Services;

public sealed record ToolCard(SuiteEntry Tool, string App, IReadOnlyList<string> Points) {
    public string Slug => Tool.Slug;
}

public static class ToolCards {
    public static IReadOnlyList<ToolCard> Default { get; } = [
        new(Entry("ledger"), "eggledger", [
            "View and filter all ships you've ever launched",
            "Write reports to parse your data",
            "Export ship drops to XLSX/CSV",
        ]),
        new(Entry("incognito"), "eggincognito", [
            "Live API data for all things Egg, Inc.",
            "Realtime notifications for new game releases",
            "Historical feed of Egg, Inc. protobuf versions",
        ]),
        new(Entry("abacus"), "eggabacus", []),
    ];

    public static ToolCard? Find(string? slug) =>
        slug is null ? null : Default.FirstOrDefault(c => string.Equals(c.Slug, slug, StringComparison.OrdinalIgnoreCase));

    private static SuiteEntry Entry(string slug) =>
        SuiteCatalog.Fallback.First(t => string.Equals(t.Slug, slug, StringComparison.OrdinalIgnoreCase));
}
