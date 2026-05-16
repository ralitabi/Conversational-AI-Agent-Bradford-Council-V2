using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bradford.Core.Interfaces;
using Bradford.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService : IToolService
{
    private readonly HttpClient _http;
    private readonly ILogger<CouncilToolService> _logger;
    private readonly IMemoryCache _cache;
    private readonly string? _getAddressApiKey;

    public CouncilToolService(IHttpClientFactory factory, ILogger<CouncilToolService> logger, IMemoryCache cache, IConfiguration config)
    {
        _http             = factory.CreateClient("Crawler");
        _logger           = logger;
        _cache            = cache;
        _getAddressApiKey = config["GetAddress:ApiKey"];
    }

    // ── Tool Definitions ─────────────────────────────────────────────────────
    public List<ToolDefinition> GetToolDefinitions() => new()
    {
        new ToolDefinition
        {
            Function = new FunctionDef
            {
                Name        = "lookup_addresses_for_postcode",
                Description = "Look up real UK property addresses for a Bradford postcode. ALWAYS call this first when the user provides a postcode for bin collection DATES (i.e. they want to know WHEN their bin is collected). Do NOT call this for missed collections, bin info, or recycling questions.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new { postcode = new { type = "string", description = "Bradford postcode e.g. BD1 1HY" } },
                    required   = new[] { "postcode" }
                }
            }
        },
        new ToolDefinition
        {
            Function = new FunctionDef
            {
                Name        = "get_bin_dates_for_address",
                Description = "Get bin collection dates for a specific Bradford address after the user has selected their property. Call this once the user has confirmed their address.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        postcode = new { type = "string", description = "The postcode" },
                        address  = new { type = "string", description = "The full selected address line" }
                    },
                    required = new[] { "postcode", "address" }
                }
            }
        },
        new ToolDefinition
        {
            Function = new FunctionDef
            {
                Name        = "search_bradford_council",
                Description = "Search the Bradford Council website for any council service, policy or information.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new { query = new { type = "string", description = "What to search for on bradford.gov.uk" } },
                    required   = new[] { "query" }
                }
            }
        },
        new ToolDefinition
        {
            Function = new FunctionDef
            {
                Name        = "fetch_council_page",
                Description = "Fetch and read the full content of a specific Bradford Council web page.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new { url = new { type = "string", description = "Full URL of the Bradford Council page to fetch" } },
                    required   = new[] { "url" }
                }
            }
        },
        new ToolDefinition
        {
            Function = new FunctionDef
            {
                Name        = "get_council_tax_info",
                Description = "Get council tax bands, rates, how to pay or apply for discounts/exemptions.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        query   = new { type = "string", description = "What the user wants to know about council tax" },
                        address = new { type = "string", description = "Address or postcode to look up their specific band (optional)" }
                    },
                    required = new[] { "query" }
                }
            }
        },
        new ToolDefinition
        {
            Function = new FunctionDef
            {
                Name        = "check_planning_application",
                Description = "Search Bradford Council's planning portal for applications, permissions and decisions.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        reference = new { type = "string", description = "Planning application reference number (optional)" },
                        address   = new { type = "string", description = "Address or postcode to search for (optional)" }
                    }
                }
            }
        },
        new ToolDefinition
        {
            Function = new FunctionDef
            {
                Name        = "find_local_services",
                Description = "Find Bradford Council local services near a location. For libraries with a postcode, returns ALL Bradford libraries sorted by distance. Also handles leisure centres, parks, schools, recycling centres.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        service  = new { type = "string", description = "Type of service: library, leisure centre, school, park, recycling" },
                        location = new { type = "string", description = "Bradford postcode or area name" }
                    },
                    required = new[] { "service" }
                }
            }
        },
        new ToolDefinition
        {
            Function = new FunctionDef
            {
                Name        = "get_library_details",
                Description = "Get full details for a specific Bradford library: facilities, opening hours, address, phone, and how to join / apply for a library card. Call this when the user selects or asks about a specific library.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        library_name = new { type = "string", description = "Name of the Bradford library e.g. 'Wyke Library', 'Bradford Central Library'" }
                    },
                    required = new[] { "library_name" }
                }
            }
        },
        new ToolDefinition
        {
            Function = new FunctionDef
            {
                Name        = "lookup_council_tax_band",
                Description = "Look up the council tax band and annual amount for a Bradford property by postcode or address. Returns the property's band (A–H) and the corresponding Bradford Council tax rate. Always call this when the user asks about their council tax amount, band, or how much they pay.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        postcode = new { type = "string", description = "Bradford postcode e.g. BD1 1HY" },
                        address  = new { type = "string", description = "Optional house number or street to narrow results" }
                    },
                    required   = new[] { "postcode" }
                }
            }
        },
        new ToolDefinition
        {
            Function = new FunctionDef
            {
                Name        = "find_schools_near_postcode",
                Description = "Find schools near a Bradford postcode. Returns a list of nearby schools with Ofsted rating, phase (primary/secondary), type, and distance. Call this whenever the user asks about schools near them, school finder, or which schools are in their area.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        postcode = new { type = "string", description = "Bradford postcode e.g. BD7 3AB" },
                        phase    = new { type = "string", description = "Optional filter: Primary, Secondary, Nursery, All-through, Sixth Form" }
                    },
                    required = new[] { "postcode" }
                }
            }
        },
        new ToolDefinition
        {
            Function = new FunctionDef
            {
                Name        = "get_school_details",
                Description = "Get full details for a specific Bradford school including phase, type, headteacher, address, phone, website, and links. Call this when the user selects or asks about a specific school. Pass the urn if available from a previous school list.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        school_name = new { type = "string", description = "Name of the school" },
                        urn         = new { type = "string", description = "BSO URN number if known from a previous school list result e.g. '50082'" }
                    },
                    required = new[] { "school_name" }
                }
            }
        },
        new ToolDefinition
        {
            Function = new FunctionDef
            {
                Name        = "get_education_info",
                Description = "Fetch Bradford Council education policies, school admissions, SEND support, free school meals, term dates, and general education guidance from bradford.gov.uk.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        topic = new { type = "string", description = "Education topic e.g. 'school admissions', 'SEND', 'free school meals', 'term dates', 'starting school'" }
                    },
                    required = new[] { "topic" }
                }
            }
        },
        new ToolDefinition
        {
            Function = new FunctionDef
            {
                Name        = "get_bin_info",
                Description = "Get detailed Bradford Council bin and recycling information scraped live from bradford.gov.uk. CALL THIS for: 'my bin wasn't collected / was missed', 'what goes in grey/green/brown bin', 'garden waste subscription cost', 'book bulky waste', 'bad weather bin', 'assisted collection', 'new/replacement bin', 'food waste rollout', 'rural collection', 'recycling centre / tip', 'hazardous waste', 'electrical items', 'sharps/needles', 'aluminium foil'. Do NOT call this for checking collection dates for a specific address.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new { query = new { type = "string", description = "What the user wants to know about bins or recycling e.g. 'what goes in grey bin', 'missed collection', 'brown bin subscription cost', 'bulky waste'" } },
                    required   = new[] { "query" }
                }
            }
        },
        new ToolDefinition
        {
            Function = new FunctionDef
            {
                Name        = "get_school_transport",
                Description = "Get full Bradford Council school transport / travel assistance information: eligibility rules, distance thresholds, how to apply, application deadlines, SEN/EHCP transport, application forms (5-16 and post-16), contact details and all downloadable policy documents. Call this whenever a user asks about school transport, bus pass, travel to school, eligibility for free transport, or applying for transport assistance.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new { query = new { type = "string", description = "What the user wants to know about school transport" } },
                    required   = new[] { "query" }
                }
            }
        },
        new ToolDefinition
        {
            Function = new FunctionDef
            {
                Name        = "get_housing_info",
                Description = "Get Bradford Council housing information scraped live from bradford.gov.uk. CALL THIS for: 'homeless', 'nowhere to sleep', 'rough sleeping', 'at risk of homelessness', 'behind on rent', 'mortgage arrears', 'find a home', 'council house', 'social housing', 'housing register', 'Bradford Homes', 'home improvements', 'repair grant', 'disabled adaptations', 'DFG', 'stairlift', 'landlord advice', 'tenant rights', 'damp', 'disrepair', 'HMO', 'empty homes', 'supported housing', 'private sector lettings', 'Incommunities', 'housing complaint'. Do NOT call this for searching available properties — use search_bradford_homes instead.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new { query = new { type = "string", description = "What the user wants to know about housing e.g. 'homeless tonight', 'disabled facilities grant', 'landlord legal duties', 'HMO licence'" } },
                    required   = new[] { "query" }
                }
            }
        },
        new ToolDefinition
        {
            Function = new FunctionDef
            {
                Name        = "search_bradford_homes",
                Description = "Search Bradford Homes for currently available properties to rent. CALL THIS whenever the user asks to see available properties, social housing listings, what homes are available, or property search. Returns a visual property card grid. Supports filtering by location (postcode or area), number of bedrooms, max rent, and property type.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        location      = new { type = "string",  description = "Bradford postcode (e.g. BD5 8LT) or area name (e.g. Bradford, Keighley, Shipley)" },
                        bedrooms      = new { type = "string",  description = "Number of bedrooms: 0=Studio, 1, 2, 3, 4, 5+" },
                        max_rent      = new { type = "string",  description = "Maximum weekly/monthly rent amount" },
                        property_type = new { type = "string",  description = "Property type: house, flat, bungalow, maisonette" },
                        radius        = new { type = "string",  description = "Search radius in miles (default 10)" }
                    },
                    required = new[] { "location" }
                }
            }
        },
        new ToolDefinition
        {
            Function = new FunctionDef
            {
                Name        = "get_adult_social_care_info",
                Description = "Get Bradford adult social care information. Call for: home care, care assessment, paying for care, direct payments, carers support, carer breaks, adult safeguarding, abuse, disability support, occupational therapy, mental health social work, supported living, residential care, preparation for adulthood.",
                Parameters  = new { type = "object", properties = new { query = new { type = "string", description = "What the user wants to know about adult social care" } }, required = new[] { "query" } }
            }
        },
        new ToolDefinition
        {
            Function = new FunctionDef
            {
                Name        = "get_children_families_info",
                Description = "Get Bradford children and families information. Call for: report concern about a child, child safeguarding, family support, family hubs, early help, SEND local offer, fostering, childminder, Bradford Children and Families Trust.",
                Parameters  = new { type = "object", properties = new { query = new { type = "string", description = "What the user wants to know about children and families services" } }, required = new[] { "query" } }
            }
        },
        new ToolDefinition
        {
            Function = new FunctionDef
            {
                Name        = "get_transport_info",
                Description = "Get Bradford transport and travel information. Call for: potholes, parking permits, parking fines, Blue Badge, bus pass, concessionary fares, roadworks, gritting, abandoned vehicles, cycling, street lights, fly-tipping, blocked drains, taxis, road safety, public transport.",
                Parameters  = new { type = "object", properties = new { query = new { type = "string", description = "What the user wants to know about transport e.g. 'report a pothole', 'parking permit', 'blue badge', 'bus pass'" } }, required = new[] { "query" } }
            }
        },
        new ToolDefinition
        {
            Function = new FunctionDef
            {
                Name        = "get_health_info",
                Description = "Get Bradford public health information. Call for: mental health, alcohol/drugs, addiction, sexual health, weight management, quit smoking, health check, vaccines, cancer screening, child health, baby health, cold weather advice, warm spaces, needle and syringe programme.",
                Parameters  = new { type = "object", properties = new { query = new { type = "string", description = "What the user wants to know about health e.g. 'mental health support', 'quit smoking', 'sexual health clinic'" } }, required = new[] { "query" } }
            }
        },
        new ToolDefinition
        {
            Function = new FunctionDef
            {
                Name        = "get_clean_air_zone_info",
                Description = "Get Bradford Clean Air Zone (CAZ) information. Call for: check if vehicle needs to pay, daily CAZ charge, how to pay CAZ, CAZ exemptions, CAZ grants, CAZ penalty, appeal CAZ penalty, what is CAZ, where is CAZ, visiting Bradford CAZ.",
                Parameters  = new { type = "object", properties = new { query = new { type = "string", description = "What the user wants to know about the Clean Air Zone e.g. 'do I need to pay', 'exemption', 'penalty charge'" } }, required = new[] { "query" } }
            }
        },
        new ToolDefinition
        {
            Function = new FunctionDef
            {
                Name        = "get_business_rates_info",
                Description = "Get Bradford business rates information. Call for: what are business rates, pay business rates, business rate reliefs, small business relief, rateable value, business rates appeal, valuation office, contact business rates team.",
                Parameters  = new { type = "object", properties = new { query = new { type = "string", description = "What the user wants to know about business rates e.g. 'pay my rates', 'small business relief', 'appeal rateable value'" } }, required = new[] { "query" } }
            }
        },
        new ToolDefinition { Function = new FunctionDef { Name = "get_licensing_info",     Description = "Bradford licensing info: taxis, food business registration, gambling, alcohol/entertainment, premises licence, temporary event notice, street trading, outdoor seating, scrap metal, tattooing, animal licences, licensing fees.", Parameters = new { type="object", properties=new{query=new{type="string",description="Licensing topic"}}, required=new[]{"query"} } } },
        new ToolDefinition { Function = new FunctionDef { Name = "get_environment_info",   Description = "Bradford environment info: dog wardens/control, public rights of way/footpaths, conservation areas, listed buildings, biodiversity, climate change, Saltaire World Heritage, parks.", Parameters = new { type="object", properties=new{query=new{type="string",description="Environment topic"}}, required=new[]{"query"} } } },
        new ToolDefinition { Function = new FunctionDef { Name = "get_community_info",     Description = "Bradford community info: allotments, domestic abuse, community grants, gypsies and travellers, armed forces, asylum seekers/refugees.", Parameters = new { type="object", properties=new{query=new{type="string",description="Community topic"}}, required=new[]{"query"} } } },
        new ToolDefinition { Function = new FunctionDef { Name = "get_sports_leisure_info",Description = "Bradford sport and leisure: leisure centres, swimming lessons, fitness classes, Leisure Card prices, outdoor adventure, walking routes, cycling, over 50s activities, free armed forces gym access.", Parameters = new { type="object", properties=new{query=new{type="string",description="Sport or leisure topic"}}, required=new[]{"query"} } } },
        new ToolDefinition { Function = new FunctionDef { Name = "get_elections_info",     Description = "Bradford elections and voting: register to vote, postal vote, voter ID, how to vote in person, proxy vote, standing as a candidate, election results, ward maps.", Parameters = new { type="object", properties=new{query=new{type="string",description="Elections topic"}}, required=new[]{"query"} } } },
        new ToolDefinition { Function = new FunctionDef { Name = "get_arts_culture_info",  Description = "Bradford arts and culture: museums, galleries, Bradford City of Film, Bradford 2025, arts/culture grants, City Park, busking, what's on, visit Bradford.", Parameters = new { type="object", properties=new{query=new{type="string",description="Arts or culture topic"}}, required=new[]{"query"} } } },
        new ToolDefinition { Function = new FunctionDef { Name = "get_complaints_info",    Description = "Bradford complaints and compliments: how to make a complaint, complaints procedure, adult social care complaints, Local Government Ombudsman, Housing Ombudsman, how to give compliments.", Parameters = new { type="object", properties=new{query=new{type="string",description="Complaints topic"}}, required=new[]{"query"} } } },
        new ToolDefinition { Function = new FunctionDef { Name = "get_jobs_info",          Description = "Bradford Council jobs and careers: job vacancies, apprenticeships, social care jobs, teaching jobs, volunteering, graduate programmes.", Parameters = new { type="object", properties=new{query=new{type="string",description="Jobs topic"}}, required=new[]{"query"} } } },
        new ToolDefinition
        {
            Function = new FunctionDef
            {
                Name        = "get_benefits_info",
                Description = "Get Bradford Council benefits information scraped live from bradford.gov.uk. CALL THIS for: 'housing benefit', 'council tax reduction', 'council tax support', 'free school meals', 'universal credit', 'crisis fund', 'emergency help', 'food bank', 'hardship', 'discretionary housing payment', 'rent shortfall', 'assisted purchase scheme', 'household items', 'overpayment', 'payment dates', 'missing payment', 'appeal benefit', 'change of circumstances', 'backdating', 'cost of living', 'welfare advice', 'benefit forms', 'landlord benefits', 'myinfo review', 'benefit notification letter'. Do NOT call this for council tax band lookups — use lookup_council_tax_band instead.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new { query = new { type = "string", description = "What the user wants to know about benefits e.g. 'housing benefit eligibility', 'how to apply for council tax reduction', 'free school meals', 'crisis fund'" } },
                    required   = new[] { "query" }
                }
            }
        },
        new ToolDefinition { Function = new FunctionDef { Name = "get_planning_info", Description = "Bradford planning and building control information. Call for: do I need planning permission, householder extensions, loft conversions, outbuildings, permitted development, make/submit/view/search planning applications, planning fees, planning appeals, planning decisions, planning committee, pre-application advice, permission in principle, lawful development certificates, planning enforcement, breach of planning, enforcement notices, neighbour building work, street naming and numbering, developer contributions, CIL, Section 106, planning policy, local plan, building regulations applications, building regulation fees, building control site inspections, demolition notice, dangerous structures, fire risk assessment, regularisation (building work without approval), duty holders, building safety levy, building control partnership, contact building control.", Parameters = new { type="object", properties=new{query=new{type="string",description="Planning or building control topic e.g. 'do I need planning permission for an extension', 'how to appeal a planning decision', 'building regulations application', 'report a dangerous structure'"}}, required=new[]{"query"} } } },
        new ToolDefinition { Function = new FunctionDef { Name = "get_business_support_info", Description = "Bradford business support information. Call for: business advice, grants, business support services, commercial premises search, office/shop/warehouse to rent, training courses for businesses, fire safety responsibilities, health and safety at work, workplace accident reporting, RIDDOR, council properties for sale or to let, buying land from council, renting council property, business leases, compulsory purchase orders, Bradford economy, economic intelligence, Bradford District Economic Strategy, commissioning adult care services, procurement, tendering, council contracts, becoming a supplier.", Parameters = new { type="object", properties=new{query=new{type="string",description="Business support topic e.g. 'business grants', 'commercial premises', 'health and safety at work', 'tender for council contract'"}}, required=new[]{"query"} } } }
    };

    // ── Tool Executor ─────────────────────────────────────────────────────────
    public async Task<string> ExecuteAsync(string toolName, string argumentsJson, CancellationToken ct = default)
    {
        _logger.LogInformation("Tool: {Tool} | Args: {Args}", toolName, argumentsJson);

        try
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, string>>(argumentsJson)
                       ?? new Dictionary<string, string>();

            return toolName switch
            {
                "lookup_addresses_for_postcode" => await LookupAddressesAsync(args.GetValueOrDefault("postcode", ""), ct),
                "get_bin_info"                  => await GetBinInfoAsync(args.GetValueOrDefault("query", ""), ct),
                "get_bin_dates_for_address"     => await GetBinDatesForAddressAsync(args.GetValueOrDefault("postcode", ""), args.GetValueOrDefault("address", ""), ct),
                "search_bradford_council"       => await SearchCouncilAsync(args.GetValueOrDefault("query", ""), ct),
                "fetch_council_page"            => IsBradfordUrl(args.GetValueOrDefault("url", ""))
                                                     ? await FetchPageAsync(args.GetValueOrDefault("url", ""), ct)
                                                     : "Only https://www.bradford.gov.uk pages can be fetched.",
                "get_council_tax_info"          => await GetCouncilTaxAsync(args.GetValueOrDefault("query", ""), args.GetValueOrDefault("address", ""), ct),
                "check_planning_application"    => await CheckPlanningAsync(args.GetValueOrDefault("reference", ""), args.GetValueOrDefault("address", ""), ct),
                "find_local_services"           => await FindLocalServicesAsync(args.GetValueOrDefault("service", ""), args.GetValueOrDefault("location", ""), ct),
                "get_library_details"           => await GetLibraryDetailsAsync(args.GetValueOrDefault("library_name", ""), ct),
                "lookup_council_tax_band"       => await LookupCouncilTaxBandAsync(args.GetValueOrDefault("postcode", ""), args.GetValueOrDefault("address", ""), ct),
                "find_schools_near_postcode"    => await FindSchoolsNearPostcodeAsync(args.GetValueOrDefault("postcode", ""), args.GetValueOrDefault("phase", ""), ct),
                "get_school_details"            => await GetSchoolDetailsAsync(args.GetValueOrDefault("school_name", ""), args.GetValueOrDefault("urn", ""), ct),
                "get_education_info"            => await GetEducationInfoAsync(args.GetValueOrDefault("topic", "schools"), ct),
                "get_school_transport"          => await GetSchoolTransportAsync(args.GetValueOrDefault("query", ""), ct),
                "get_housing_info"              => await GetHousingInfoAsync(args.GetValueOrDefault("query", ""), ct),
                "search_bradford_homes"         => await SearchBradfordHomesAsync(
                                                     args.GetValueOrDefault("location", "Bradford"),
                                                     args.GetValueOrDefault("bedrooms", ""),
                                                     args.GetValueOrDefault("max_rent", ""),
                                                     args.GetValueOrDefault("property_type", ""),
                                                     args.GetValueOrDefault("radius", "10"), ct),
                "get_licensing_info"            => await GetLicensingInfoAsync(args.GetValueOrDefault("query", ""), ct),
                "get_environment_info"          => await GetEnvironmentInfoAsync(args.GetValueOrDefault("query", ""), ct),
                "get_community_info"            => await GetCommunityInfoAsync(args.GetValueOrDefault("query", ""), ct),
                "get_sports_leisure_info"       => await GetSportsLeisureInfoAsync(args.GetValueOrDefault("query", ""), ct),
                "get_elections_info"            => await GetElectionsInfoAsync(args.GetValueOrDefault("query", ""), ct),
                "get_arts_culture_info"         => await GetArtsCultureInfoAsync(args.GetValueOrDefault("query", ""), ct),
                "get_complaints_info"           => await GetComplaintsInfoAsync(args.GetValueOrDefault("query", ""), ct),
                "get_jobs_info"                 => await GetJobsInfoAsync(args.GetValueOrDefault("query", ""), ct),
                "get_adult_social_care_info"    => await GetAdultSocialCareInfoAsync(args.GetValueOrDefault("query", ""), ct),
                "get_children_families_info"    => await GetChildrenFamiliesInfoAsync(args.GetValueOrDefault("query", ""), ct),
                "get_transport_info"            => await GetTransportInfoAsync(args.GetValueOrDefault("query", ""), ct),
                "get_health_info"               => await GetHealthInfoAsync(args.GetValueOrDefault("query", ""), ct),
                "get_clean_air_zone_info"       => await GetCleanAirZoneInfoAsync(args.GetValueOrDefault("query", ""), ct),
                "get_business_rates_info"       => await GetBusinessRatesInfoAsync(args.GetValueOrDefault("query", ""), ct),
                "get_benefits_info"             => await GetBenefitsInfoAsync(args.GetValueOrDefault("query", ""), ct),
                "get_planning_info"             => await GetPlanningInfoAsync(args.GetValueOrDefault("query", ""), ct),
                "get_business_support_info"     => await GetBusinessSupportInfoAsync(args.GetValueOrDefault("query", ""), ct),
                _                               => $"Unknown tool: {toolName}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool {Tool} failed", toolName);
            return $"Tool error: {ex.Message}";
        }
    }

    // ── Shared knowledge-map scraper (used by all info tools) ────────────────
    private async Task<string> ScrapeKnowledgeMapAsync(
        string query,
        (string[] Keywords, string[] Urls, string Title, string FollowUp)[] map,
        string fallbackUrl, string fallbackTitle, string fallbackFollowUp,
        string header,
        CancellationToken ct)
    {
        var q    = query.ToLower();
        var urls = new List<string>();
        var title    = "";
        var followUp = "";

        foreach (var (keywords, pages, pageTitle, pageFollowUp) in map)
        {
            if (keywords.Any(k => q.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var u in pages)
                    if (!urls.Contains(u)) urls.Add(u);
                if (string.IsNullOrEmpty(title)) { title = pageTitle; followUp = pageFollowUp; }
                if (urls.Count >= 2) break;
            }
        }

        if (urls.Count == 0)
        {
            urls.Add(fallbackUrl);
            title    = fallbackTitle;
            followUp = fallbackFollowUp;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{header} — query: \"{query}\"");
        sb.AppendLine();

        foreach (var url in urls.Take(2))
        {
            var html = await FetchHtmlAsync(url, ct);
            if (string.IsNullOrEmpty(html)) continue;

            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);
            foreach (var tag in new[] { "nav", "header", "footer", "script", "style", "aside", "noscript" })
            {
                var nodes = doc.DocumentNode.SelectNodes($"//{tag}");
                if (nodes != null) foreach (var n in nodes.ToList()) n.Remove();
            }
            var main = doc.DocumentNode.SelectSingleNode("//main")
                    ?? doc.DocumentNode.SelectSingleNode("//article")
                    ?? doc.DocumentNode.SelectSingleNode("//body");
            if (main == null) continue;

            sb.AppendLine($"--- SOURCE: {url} ---");
            sb.AppendLine(TruncateText(CleanText(main.InnerText), 3500));
            sb.AppendLine();
        }

        sb.AppendLine($"OFFICIAL_BRADFORD_LINK: [{title}]({urls[0]})");
        if (!string.IsNullOrEmpty(followUp))
            sb.AppendLine($"FOLLOW_UP_SUGGESTION: {followUp}");

        return sb.ToString();
    }

    // ── Shared helpers ────────────────────────────────────────────────────────
    private static bool IsBradfordUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        (uri.Host.Equals("www.bradford.gov.uk", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.Equals("bradford.gov.uk",     StringComparison.OrdinalIgnoreCase));

    private static string ToTitleCase(string s) =>
        System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());

    private static int ScoreAddressMatch(string candidate, string target)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return 0;
        var c = candidate.ToLower();
        var t = target.ToLower();
        if (c.Contains(t) || t.Contains(c)) return 2;
        // Match on house number + first word of street
        var tParts = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tParts.Count(p => c.Contains(p)) >= 2 ? 1 : 0;
    }

    private async Task<string?> FetchHtmlAsync(string url, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("Accept", "text/html,application/json,application/xhtml+xml");
            req.Headers.Add("User-Agent", "BradfordCouncilAI/1.0 (council service assistant)");
            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Fetch failed {Url}: {Msg}", url, ex.Message);
            return null;
        }
    }

    private static string CleanText(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim()).Where(l => l.Length > 2);
        return string.Join('\n', lines);
    }

    private static string TruncateText(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";

    private static string BuildAddressLine(NominatimAddress addr)
    {
        var parts = new[] { addr.HouseNumber, addr.HouseName, addr.Road, addr.Suburb, addr.Neighbourhood }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(", ", parts);
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────
    private sealed class NominatimResult
    {
        [JsonPropertyName("address")] public NominatimAddress? Address { get; set; }
    }
    private sealed class NominatimAddress
    {
        [JsonPropertyName("house_number")] public string? HouseNumber   { get; set; }
        [JsonPropertyName("house_name")]   public string? HouseName     { get; set; }
        [JsonPropertyName("road")]         public string? Road          { get; set; }
        [JsonPropertyName("suburb")]       public string? Suburb        { get; set; }
        [JsonPropertyName("neighbourhood")]public string? Neighbourhood { get; set; }
        [JsonPropertyName("city")]         public string? City          { get; set; }
        [JsonPropertyName("town")]         public string? Town          { get; set; }
        [JsonPropertyName("village")]      public string? Village       { get; set; }
        [JsonPropertyName("postcode")]     public string? Postcode      { get; set; }
    }
    private sealed class PostcodeResponse
    {
        [JsonPropertyName("status")] public int          Status { get; set; }
        [JsonPropertyName("result")] public PostcodeData? Result { get; set; }
    }
    private sealed class PostcodeData
    {
        [JsonPropertyName("admin_district")] public string? AdminDistrict { get; set; }
        [JsonPropertyName("admin_ward")]     public string? AdminWard     { get; set; }
        [JsonPropertyName("parish")]         public string? Parish        { get; set; }
        [JsonPropertyName("postcode")]       public string? Postcode      { get; set; }
        public bool IsTerminated { get; set; }
    }
}
