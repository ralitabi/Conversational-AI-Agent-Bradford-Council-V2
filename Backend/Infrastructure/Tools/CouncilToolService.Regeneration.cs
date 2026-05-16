namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] RegenerationKnowledgeMap =
    {
        // ── Keighley Towns Fund (general) ─────────────────────────────────────
        (new[]{"keighley towns fund","keighley regeneration","keighley investment","keighley development",
               "towns fund keighley","keighley town deal","keighley future"},
         new[]{"https://www.bradford.gov.uk/regeneration/keighley-towns-fund/keighley-towns-fund/",
               "https://www.bradford.gov.uk/regeneration/keighley-towns-fund/key-benefits/"},
         "Keighley Towns Fund",
         "The Keighley Towns Fund is a £37m+ government investment programme. Would you like to know about Keighley community grants or the area map?"),

        // ── Keighley Towns Fund FAQs ──────────────────────────────────────────
        (new[]{"keighley towns fund faq","keighley fund questions","keighley fund how","keighley regeneration faq"},
         new[]{"https://www.bradford.gov.uk/regeneration/keighley-towns-fund/frequently-asked-questions/"},
         "Keighley Towns Fund — frequently asked questions",
         "Would you like to know about the Keighley community grants scheme or the fund vision?"),

        // ── Keighley community grants ─────────────────────────────────────────
        (new[]{"keighley community grant","keighley grant","keighley fund grant","keighley towns fund grant",
               "apply keighley grant","keighley community funding"},
         new[]{"https://www.bradford.gov.uk/regeneration/keighley-towns-fund/community-grants-scheme/"},
         "Keighley Towns Fund community grants scheme",
         "Would you like to know about other community grants in Bradford or the Shipley Towns Fund?"),

        // ── Keighley area map ─────────────────────────────────────────────────
        (new[]{"keighley towns fund area","keighley fund boundary","keighley fund map",
               "keighley regeneration area","keighley fund zone"},
         new[]{"https://www.bradford.gov.uk/regeneration/keighley-towns-fund/keighley-towns-fund-area-map/"},
         "Keighley Towns Fund area map",
         "Would you like to know about Keighley regeneration projects or the fund vision?"),

        // ── Keighley board and minutes ────────────────────────────────────────
        (new[]{"keighley towns fund board","keighley fund governance","keighley board minutes",
               "keighley fund board","keighley fund meetings"},
         new[]{"https://www.bradford.gov.uk/regeneration/keighley-towns-fund/keighley-towns-fund-board-and-minutes/"},
         "Keighley Towns Fund board and minutes",
         "Would you like to know about Keighley regeneration projects or the community grants scheme?"),

        // ── Keighley latest news ──────────────────────────────────────────────
        (new[]{"keighley towns fund news","keighley regeneration news","keighley fund update",
               "keighley development news","keighley investment news"},
         new[]{"https://www.bradford.gov.uk/regeneration/keighley-towns-fund/latest-news/"},
         "Keighley Towns Fund latest news",
         "Would you like to know about Keighley community grants or the Shipley Towns Fund?"),

        // ── Shipley Towns Fund (general) ──────────────────────────────────────
        (new[]{"shipley towns fund","shipley regeneration","shipley investment","shipley development",
               "towns fund shipley","shipley town deal","shipley future","shipley projects"},
         new[]{"https://www.bradford.gov.uk/regeneration/shipley-towns-fund/shipley-towns-fund/",
               "https://www.bradford.gov.uk/regeneration/shipley-towns-fund/projects/"},
         "Shipley Towns Fund",
         "The Shipley Towns Fund is a government-backed investment to regenerate Shipley town centre. Would you like to know about specific projects or FAQs?"),

        // ── Shipley Towns Fund FAQs ───────────────────────────────────────────
        (new[]{"shipley towns fund faq","shipley fund questions","shipley regeneration faq","shipley fund how"},
         new[]{"https://www.bradford.gov.uk/regeneration/shipley-towns-fund/frequently-asked-questions/"},
         "Shipley Towns Fund — frequently asked questions",
         "Would you like to know about Shipley regeneration projects or the Keighley Towns Fund?"),

        // ── Shipley area map ──────────────────────────────────────────────────
        (new[]{"shipley towns fund area","shipley fund boundary","shipley fund map",
               "shipley regeneration area","shipley fund zone"},
         new[]{"https://www.bradford.gov.uk/regeneration/shipley-towns-fund/shipley-towns-fund-area-map/"},
         "Shipley Towns Fund area map",
         "Would you like to know about Shipley regeneration projects or the fund vision?"),

        // ── Shipley board and minutes ─────────────────────────────────────────
        (new[]{"shipley towns fund board","shipley fund governance","shipley board minutes",
               "shipley fund board","shipley fund meetings"},
         new[]{"https://www.bradford.gov.uk/regeneration/shipley-towns-fund/shipley-towns-fund-board-and-minutes/"},
         "Shipley Towns Fund board and minutes",
         "Would you like to know about Shipley regeneration projects or the community grants scheme?"),

        // ── Shipley latest news ───────────────────────────────────────────────
        (new[]{"shipley towns fund news","shipley regeneration news","shipley fund update",
               "shipley development news","shipley investment news"},
         new[]{"https://www.bradford.gov.uk/regeneration/shipley-towns-fund/latest-news/"},
         "Shipley Towns Fund latest news",
         "Would you like to know about Shipley projects or the Keighley Towns Fund?"),

        // ── General regeneration fallback ─────────────────────────────────────
        (new[]{"regeneration","bradford regeneration","regeneration bradford","towns fund",
               "bradford development","investment bradford","bradford growth"},
         new[]{"https://www.bradford.gov.uk/regeneration/"},
         "Regeneration in Bradford",
         "What regeneration area would you like to know about? I can help with Keighley Towns Fund, Shipley Towns Fund, and community grants."),
    };

    private async Task<string> GetRegenerationInfoAsync(string query, CancellationToken ct)
        => await ScrapeKnowledgeMapAsync(query, RegenerationKnowledgeMap,
            "https://www.bradford.gov.uk/regeneration/",
            "Regeneration in Bradford",
            "What regeneration area would you like to know about? I can help with Keighley Towns Fund, Shipley Towns Fund, and community grants.",
            "BRADFORD REGENERATION INFORMATION", ct);
}
