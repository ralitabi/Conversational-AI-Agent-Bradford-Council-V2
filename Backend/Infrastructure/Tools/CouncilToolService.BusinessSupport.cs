namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] BusinessSupportKnowledgeMap =
    {
        // ── General business support ──────────────────────────────────────────
        (new[]{"business support","help for business","business advice","start a business","grow my business",
               "business grant","business funding","support for business","business help bradford",
               "enterprise support","business development"},
         new[]{"https://www.bradford.gov.uk/business/help-for-businesses/business-support/"},
         "Business support in Bradford",
         "Would you like to know about training courses, commercial premises or business rates reliefs?"),

        // ── Commercial premises / property search ─────────────────────────────
        (new[]{"commercial premises","commercial property","find business premises","business property search",
               "office to rent","shop to rent","industrial unit","warehouse","retail unit",
               "business space bradford","commercial space","find commercial property"},
         new[]{"https://www.bradford.gov.uk/business/help-for-businesses/commercial-premises-property-search/"},
         "Commercial premises and property search",
         "Would you like to know about renting or buying property from Bradford Council?"),

        // ── Training courses ──────────────────────────────────────────────────
        (new[]{"business training","training course","training for business","workforce training",
               "staff training","business skills","cpd","professional development",
               "training bradford business","business course"},
         new[]{"https://www.bradford.gov.uk/business/help-for-businesses/training-courses-for-businesses/"},
         "Training courses for businesses",
         "Would you like to know about other business support services or the Bradford economy strategy?"),

        // ── Fire safety responsibilities ──────────────────────────────────────
        (new[]{"fire safety business","fire safety responsible person","fire risk assessment business",
               "business fire safety","fire safety law","fire regulations business",
               "responsible person fire","fire safety duties","fire safety employer"},
         new[]{"https://www.bradford.gov.uk/business/help-for-businesses/fire-safety-responsibilities-for-the-responsible-person/"},
         "Fire safety responsibilities for businesses",
         "Contact Bradford Council's Fire Safety team or the West Yorkshire Fire Service for further guidance."),

        // ── Health and safety at work ─────────────────────────────────────────
        (new[]{"health and safety","health safety at work","hasaw","workplace safety","employer health safety",
               "health safety employer","health safety law","health safety duties","health safety inspection",
               "risk assessment workplace","workplace accident","injury at work"},
         new[]{"https://www.bradford.gov.uk/business/health-and-safety-at-work/health-and-safety-at-work/",
               "https://www.bradford.gov.uk/business/health-and-safety-at-work/investigating-hasaw-accidents-and-complaints/"},
         "Health and safety at work",
         "Would you like to know about reporting an accident or how Bradford Council investigates health and safety complaints?"),

        // ── H&S accident / complaints investigation ───────────────────────────
        (new[]{"report accident work","workplace accident report","hasaw accident","hasaw complaint",
               "health safety complaint","investigate accident","work injury report",
               "riddor","dangerous occurrence","near miss report"},
         new[]{"https://www.bradford.gov.uk/business/health-and-safety-at-work/investigating-hasaw-accidents-and-complaints/"},
         "Investigating health and safety accidents and complaints",
         "RIDDOR reportable incidents must also be reported to the Health and Safety Executive (HSE) at hse.gov.uk."),

        // ── Council properties for sale / to let ─────────────────────────────
        (new[]{"council property for sale","council property to let","council commercial let",
               "property from council","buy council property","rent from council",
               "council owned property","council land for sale","council building to rent"},
         new[]{"https://www.bradford.gov.uk/business/properties/properties-for-sale-and-to-let/"},
         "Council properties for sale and to let",
         "Would you like to know about buying land from the council or the process for renting council property?"),

        // ── Buying land / property from council ───────────────────────────────
        (new[]{"buy land from council","purchase council land","buy council building","council land purchase",
               "acquire council property","council asset disposal","buy land bradford council"},
         new[]{"https://www.bradford.gov.uk/business/properties/buying-land-or-property-from-the-council/"},
         "Buying land or property from Bradford Council",
         "Would you like to know about compulsory purchase orders or other council property options?"),

        // ── Renting property from council ─────────────────────────────────────
        (new[]{"rent council property","rent from bradford council","renting council premises",
               "council tenancy commercial","council lease business","guide to renting council"},
         new[]{"https://www.bradford.gov.uk/business/properties/a-guide-to-renting-property-from-the-council/",
               "https://www.bradford.gov.uk/business/properties/a-tenants-guide-to-business-leases/"},
         "Renting property from Bradford Council — tenant guide",
         "Would you like to know about council properties currently available to let?"),

        // ── Business leases ───────────────────────────────────────────────────
        (new[]{"business lease","commercial lease","leasehold","lease terms","lease agreement",
               "tenant business lease","lease renewal","business tenancy","leasehold property"},
         new[]{"https://www.bradford.gov.uk/business/properties/a-tenants-guide-to-business-leases/"},
         "Tenant's guide to business leases",
         "Would you like to know about renting council property or finding commercial premises in Bradford?"),

        // ── Compulsory purchase orders ────────────────────────────────────────
        (new[]{"compulsory purchase","CPO","compulsory purchase order","land compulsory purchase",
               "compulsory acquisition","council compulsory buy","forced purchase land"},
         new[]{"https://www.bradford.gov.uk/business/properties/compulsory-purchase-orders/"},
         "Compulsory purchase orders",
         "Would you like to know about buying land from the council or the Bradford economy strategy?"),

        // ── Bradford economy ──────────────────────────────────────────────────
        (new[]{"bradford economy","bradford economic","invest in bradford","inward investment",
               "bradford business economy","bradford gdp","bradford growth","bradford economic development",
               "invest bradford"},
         new[]{"https://www.bradford.gov.uk/business/bradford-economy/bradford-economy/",
               "https://www.bradford.gov.uk/business/bradford-economy/economic-intelligence/"},
         "Bradford economy and investment",
         "Would you like to know about the Bradford District Economic Strategy or business support services?"),

        // ── Economic intelligence / data ──────────────────────────────────────
        (new[]{"economic intelligence","bradford data","economic data","labour market","economic statistics",
               "bradford economic report","economic analysis","business intelligence bradford"},
         new[]{"https://www.bradford.gov.uk/business/bradford-economy/economic-intelligence/"},
         "Bradford economic intelligence and data",
         "Would you like to know about the Bradford District Economic Strategy?"),

        // ── Economic strategy ─────────────────────────────────────────────────
        (new[]{"economic strategy","bradford district economic strategy","district plan","bradford masterplan",
               "economic vision bradford","growth strategy","regeneration strategy"},
         new[]{"https://www.bradford.gov.uk/business/bradford-economy/bradford-district-economic-strategy/"},
         "Bradford District Economic Strategy",
         "Would you like to know about investment opportunities or business support services in Bradford?"),

        // ── Commissioning adult health & social care ──────────────────────────
        (new[]{"commissioning","commission care services","adult social care commissioning","care provider",
               "provider adult care","health social care commissioning","commission bradford",
               "care services tender","care framework"},
         new[]{"https://www.bradford.gov.uk/business/commissioning-adult-health-and-social-care-services/commissioning-adult-health-and-social-care-services/"},
         "Commissioning adult health and social care services",
         "Would you like to know about Bradford Council's procurement process or tendering for contracts?"),

        // ── Procurement / tendering ───────────────────────────────────────────
        (new[]{"procurement","tender","tendering","council contract","bid for contract","supplier",
               "supply council","become supplier","council supplier","procurement bradford",
               "contract opportunity","council procurement"},
         new[]{"https://www.bradford.gov.uk/business/doing-business-with-bradford-council/procurement/"},
         "Bradford Council procurement and tendering",
         "Council contracts are advertised on the YORtender portal. Would you like to know about other business support?"),

        // ── General business fallback ─────────────────────────────────────────
        (new[]{"business","bradford business","company","enterprise","trade","employer"},
         new[]{"https://www.bradford.gov.uk/business/"},
         "Business in Bradford",
         "What business support do you need? I can help with premises, training, health & safety, procurement, and economic data."),
    };

    private async Task<string> GetBusinessSupportInfoAsync(string query, CancellationToken ct)
        => await ScrapeKnowledgeMapAsync(query, BusinessSupportKnowledgeMap,
            "https://www.bradford.gov.uk/business/",
            "Business in Bradford",
            "What business support do you need? I can help with premises, training, health & safety, procurement, and economic data.",
            "BRADFORD BUSINESS SUPPORT INFORMATION", ct);
}
