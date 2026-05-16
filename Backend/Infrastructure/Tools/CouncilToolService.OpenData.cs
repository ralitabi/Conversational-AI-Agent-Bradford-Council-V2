namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] OpenDataKnowledgeMap =
    {
        // ── Freedom of Information ────────────────────────────────────────────
        (new[]{"freedom of information","FOI","foi request","make foi request","submit foi",
               "information request","access information","public information request",
               "right to information","council records request","foi bradford"},
         new[]{"https://www.bradford.gov.uk/open-data/freedom-of-information/freedom-of-information/"},
         "Freedom of Information (FOI) requests",
         "Bradford Council must respond to FOI requests within 20 working days. Would you like to know your rights under data protection law?"),

        // ── FOI — business rates ──────────────────────────────────────────────
        (new[]{"foi business rates","freedom information business rates","foi rateable","foi rating"},
         new[]{"https://www.bradford.gov.uk/open-data/freedom-of-information/freedom-of-information-faqs-business-rates/"},
         "FOI frequently asked questions — business rates",
         "Would you like to know how to submit a general FOI request or access business rates information?"),

        // ── FOI — council tax enforcement ─────────────────────────────────────
        (new[]{"foi council tax","freedom information council tax","foi enforcement","foi council tax enforcement"},
         new[]{"https://www.bradford.gov.uk/open-data/freedom-of-information/freedom-of-information-council-tax-enforcement/"},
         "FOI — council tax enforcement information",
         "Would you like to know how to submit a FOI request or access council tax information directly?"),

        // ── Environmental Information Regulations ──────────────────────────────
        (new[]{"environmental information","EIR","environmental information regulations",
               "request environmental data","access environmental records","environmental records request"},
         new[]{"https://www.bradford.gov.uk/open-data/environmental-information-regulations/environmental-information-regulations/"},
         "Environmental Information Regulations (EIR)",
         "EIR requests cover information about the environment — air, water, land, waste, emissions. Would you like to submit a request?"),

        // ── Data protection / GDPR ────────────────────────────────────────────
        (new[]{"data protection","gdpr","general data protection","uk gdpr","data privacy",
               "my data bradford","personal data council","data rights","how council uses data"},
         new[]{"https://www.bradford.gov.uk/open-data/data-protection/data-protection/",
               "https://www.bradford.gov.uk/open-data/data-protection/the-uk-general-data-protection-regulation/"},
         "Data protection and GDPR — Bradford Council",
         "Would you like to know your rights under GDPR or how to make a subject access request?"),

        // ── Subject access request ────────────────────────────────────────────
        (new[]{"subject access request","SAR","access my data","see my data","data request",
               "personal data request","copy of my data","what data does council hold",
               "see information held","right of access"},
         new[]{"https://www.bradford.gov.uk/open-data/data-protection/make-a-data-protection-request/",
               "https://www.bradford.gov.uk/open-data/data-protection/rights-for-individuals-under-gdpr/"},
         "Make a data protection (subject access) request",
         "Bradford Council must respond to subject access requests within one month. Would you like to know about your rights under GDPR?"),

        // ── Rights under GDPR ─────────────────────────────────────────────────
        (new[]{"data rights","gdpr rights","right to erasure","right to be forgotten","right to rectification",
               "right to object","data portability","restrict processing","individual rights data"},
         new[]{"https://www.bradford.gov.uk/open-data/data-protection/rights-for-individuals-under-gdpr/"},
         "Your rights under GDPR",
         "Your rights include: access, rectification, erasure, restriction, portability, and objection. Would you like to make a data request?"),

        // ── Request CCTV footage ──────────────────────────────────────────────
        (new[]{"cctv footage","cctv request","request cctv","cctv video","cctv recording",
               "cctv bradford","obtain cctv footage","get cctv recording","cctv data"},
         new[]{"https://www.bradford.gov.uk/open-data/data-protection/request-cctv-footage/"},
         "Request CCTV footage from Bradford Council",
         "CCTV footage requests are handled as subject access requests. Would you like to know about making a data protection request?"),

        // ── National data opt-out ─────────────────────────────────────────────
        (new[]{"national data opt out","opt out data","opt out nhs data","health data opt out",
               "stop sharing data","data sharing opt out","nhs data opt out"},
         new[]{"https://www.bradford.gov.uk/open-data/data-protection/national-data-opt-out-policy/"},
         "National data opt-out",
         "You can opt out of your confidential patient data being used for research at nhs.uk/your-nhs-data-matters. Would you like to know more about data rights?"),

        // ── Open data / datasets ──────────────────────────────────────────────
        (new[]{"open data","council datasets","public data","data sets bradford","bradford open data",
               "download council data","council statistics","public datasets","data portal"},
         new[]{"https://www.bradford.gov.uk/open-data/our-datasets/our-datasets/"},
         "Bradford Council open datasets",
         "Bradford publishes datasets including expenditure, contracts, land assets, parking and population. Would you like to know about a specific dataset?"),

        // ── Maps ─────────────────────────────────────────────────────────────
        (new[]{"council maps","bradford map","ward map","district map","open data map",
               "gis map bradford","geographic data","mapping bradford"},
         new[]{"https://www.bradford.gov.uk/open-data/maps/maps/"},
         "Bradford Council maps",
         "Would you like to know about ward profiles, district maps or specific geographic data?"),

        // ── Publication scheme ────────────────────────────────────────────────
        (new[]{"publication scheme","what council publishes","council transparency","proactive disclosure",
               "routinely published information","council publication"},
         new[]{"https://www.bradford.gov.uk/open-data/publication-scheme/publication-scheme/"},
         "Bradford Council publication scheme",
         "Would you like to know about a specific type of published information or make a FOI request?"),

        // ── Records management ────────────────────────────────────────────────
        (new[]{"records management","council records","record retention","document retention",
               "records policy","document management","archive council"},
         new[]{"https://www.bradford.gov.uk/open-data/records-management/records-management/"},
         "Bradford Council records management",
         "Would you like to know about FOI requests or data protection?"),

        // ── General open data fallback ────────────────────────────────────────
        (new[]{"open data","information governance","transparency","data","public records"},
         new[]{"https://www.bradford.gov.uk/open-data/"},
         "Open data and information governance — Bradford Council",
         "What information do you need? I can help with FOI requests, data protection rights, subject access requests, CCTV footage, and open datasets."),
    };

    private async Task<string> GetOpenDataInfoAsync(string query, CancellationToken ct)
        => await ScrapeKnowledgeMapAsync(query, OpenDataKnowledgeMap,
            "https://www.bradford.gov.uk/open-data/",
            "Open data and information governance — Bradford Council",
            "What information do you need? I can help with FOI requests, data protection rights, subject access requests, CCTV footage, and open datasets.",
            "BRADFORD OPEN DATA & INFORMATION GOVERNANCE", ct);
}
