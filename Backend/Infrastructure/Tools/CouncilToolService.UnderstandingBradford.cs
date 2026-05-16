namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] UnderstandingBradfordKnowledgeMap =
    {
        // ── Population ────────────────────────────────────────────────────────
        (new[]{"bradford population","how many people bradford","population bradford",
               "bradford demographics","bradford size","bradford census","2021 census bradford"},
         new[]{"https://www.bradford.gov.uk/understanding-bradford-district/bradford-in-focus/population/",
               "https://www.bradford.gov.uk/understanding-bradford-district/bradford-in-focus/2021-census/"},
         "Bradford District population and census data",
         "Bradford is the sixth largest city in England with around 550,000 residents. Would you like to know about ward profiles or local economy data?"),

        // ── Ethnicity and religion ────────────────────────────────────────────
        (new[]{"ethnicity bradford","religion bradford","diversity bradford","bradford ethnicity",
               "bradford religion","communities bradford","multicultural bradford",
               "ethnic groups bradford","faith bradford"},
         new[]{"https://www.bradford.gov.uk/understanding-bradford-district/bradford-in-focus/ethnicity-and-religion/"},
         "Ethnicity and religion in Bradford District",
         "Would you like to know about Bradford's population profile or deprivation data?"),

        // ── Unemployment / economy ────────────────────────────────────────────
        (new[]{"unemployment bradford","jobless bradford","bradford employment","work bradford",
               "bradford unemployment rate","employment rate bradford","jobs market bradford",
               "claimant count bradford","labour market bradford"},
         new[]{"https://www.bradford.gov.uk/understanding-bradford-district/bradford-in-focus/unemployment-in-bradford-district/",
               "https://www.bradford.gov.uk/understanding-bradford-district/bradford-in-focus/local-economy/"},
         "Unemployment and local economy in Bradford",
         "Would you like to know about Bradford's economic strategy or business support services?"),

        // ── Health and life expectancy ────────────────────────────────────────
        (new[]{"life expectancy bradford","health bradford","bradford life expectancy","health inequalities bradford",
               "bradford health statistics","public health data bradford","health outcomes bradford"},
         new[]{"https://www.bradford.gov.uk/understanding-bradford-district/bradford-in-focus/health-and-life-expectancy/"},
         "Health and life expectancy in Bradford District",
         "Would you like to know about Bradford's public health services or poverty and deprivation data?"),

        // ── Poverty and deprivation ───────────────────────────────────────────
        (new[]{"poverty bradford","deprivation bradford","bradford poverty","bradford deprivation",
               "inequality bradford","deprived areas bradford","imd bradford","index deprivation"},
         new[]{"https://www.bradford.gov.uk/understanding-bradford-district/bradford-in-focus/poverty-and-deprivation/"},
         "Poverty and deprivation in Bradford District",
         "Would you like to know about Bradford's anti-poverty strategy or cost of living support?"),

        // ── Local economy data ────────────────────────────────────────────────
        (new[]{"local economy bradford","bradford economy data","gdp bradford","bradford economic data",
               "bradford businesses","bradford economic statistics","bradford economic profile"},
         new[]{"https://www.bradford.gov.uk/understanding-bradford-district/bradford-in-focus/local-economy/"},
         "Local economy data — Bradford District",
         "Would you like to know about the Bradford District Economic Strategy or business support?"),

        // ── Ward profiles ─────────────────────────────────────────────────────
        (new[]{"ward profile","bradford ward","ward data","ward statistics","my ward bradford",
               "ward information","local ward data","neighbourhood data"},
         new[]{"https://www.bradford.gov.uk/understanding-bradford-district/constituency-and-ward-profiles/ward-profiles/"},
         "Bradford ward profiles",
         "Ward profiles include population, demographics and deprivation data. Would you like to find your ward using your postcode?"),

        // ── Constituency profiles ─────────────────────────────────────────────
        (new[]{"constituency profile","bradford constituency","constituency data","mp constituency",
               "constituency statistics","parliamentary constituency bradford"},
         new[]{"https://www.bradford.gov.uk/understanding-bradford-district/constituency-and-ward-profiles/constituency-profiles/"},
         "Bradford constituency profiles",
         "Would you like to know about ward profiles or how to find your local councillor?"),

        // ── District and ward maps ────────────────────────────────────────────
        (new[]{"ward map","district map","bradford ward map","ward boundary","ward boundaries",
               "constituency map","bradford boundary","local area map"},
         new[]{"https://www.bradford.gov.uk/understanding-bradford-district/maps/district-and-ward-maps/",
               "https://www.bradford.gov.uk/your-council/elections-and-voting/ward-maps/"},
         "Bradford district and ward maps",
         "Would you like to find your ward or look up your local councillor?"),

        // ── General fallback ──────────────────────────────────────────────────
        (new[]{"understanding bradford","bradford facts","bradford statistics","about bradford district",
               "bradford information","bradford data","bradford profile","what is bradford like"},
         new[]{"https://www.bradford.gov.uk/understanding-bradford-district/understanding-bradford-district/"},
         "Understanding Bradford District",
         "What would you like to know about Bradford? I can help with population, economy, ward profiles, health data, and deprivation statistics."),
    };

    private async Task<string> GetUnderstandingBradfordInfoAsync(string query, CancellationToken ct)
        => await ScrapeKnowledgeMapAsync(query, UnderstandingBradfordKnowledgeMap,
            "https://www.bradford.gov.uk/understanding-bradford-district/understanding-bradford-district/",
            "Understanding Bradford District",
            "What would you like to know about Bradford? I can help with population, economy, ward profiles, health data, and deprivation statistics.",
            "UNDERSTANDING BRADFORD DISTRICT INFORMATION", ct);
}
