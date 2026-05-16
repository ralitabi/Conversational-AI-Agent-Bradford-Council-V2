using System.Text;
using HtmlAgilityPack;

namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] HousingKnowledgeMap =
    {
        // Homelessness — emergency / nowhere to sleep tonight
        (new[]{"homeless","nowhere to sleep","sleeping rough","rough sleeping","no home","evicted tonight","emergency housing","urgent housing","nowhere to stay"},
         new[]{"https://www.bradford.gov.uk/housing/homelessness/i-have-nowhere-to-sleep-tonight/",
               "https://www.bradford.gov.uk/housing/homelessness/getting-help/"},
         "Emergency homelessness help",
         "Would you like to know about emergency food providers or other urgent support services?"),

        // Risk of homelessness / prevention
        (new[]{"risk of homelessness","at risk","might be homeless","could be homeless","preventing homelessness","help avoid homelessness","could lose my home","eviction notice"},
         new[]{"https://www.bradford.gov.uk/housing/homelessness/helping-people-at-risk-of-homelessness/",
               "https://www.bradford.gov.uk/housing/homelessness/getting-help/"},
         "Help if you're at risk of homelessness",
         "Would you like to know about the Housing Options Service or what support is available for rent arrears?"),

        // Behind on rent / mortgage arrears
        (new[]{"behind on rent","rent arrears","can't pay rent","mortgage arrears","can't pay mortgage","eviction","behind on mortgage","rent debt"},
         new[]{"https://www.bradford.gov.uk/housing/homelessness/i-am-behind-on-my-rent/",
               "https://www.bradford.gov.uk/housing/homelessness/help-with-mortgage-arrears/"},
         "Help with rent or mortgage arrears",
         "Would you like to know about the Housing Benefit or discretionary housing payments that could help cover your rent?"),

        // Cold weather / rough sleeping
        (new[]{"cold weather","rough sleeper","sleeping outside","sleeping on street","cold night","winter help","rough sleep"},
         new[]{"https://www.bradford.gov.uk/housing/homelessness/help-for-homeless-people-during-cold-weather/"},
         "Help for rough sleepers and cold weather",
         "Would you like to know about emergency accommodation or housing support services?"),

        // Finding a home / Bradford Homes register
        (new[]{"find a home","looking for home","council house","housing association","social housing","affordable housing","bradfordhomes","housing register","apply for housing","waiting list","band 1","band 2","band 3","band 4","housing application"},
         new[]{"https://www.bradford.gov.uk/housing/finding-a-home/how-can-i-find-a-home/",
               "https://www.bradfordhomes.org.uk/"},
         "Finding a home through Bradford Homes",
         "Would you like to search for currently available properties? I can show you what's listed right now."),

        // Home improvements / financial assistance for homeowners
        (new[]{"home improvement","home repair","home appreciation loan","HAL","repair grant","house repair","fix my home","leaking roof","electrical fault","heating","kitchen replacement","bathroom"},
         new[]{"https://www.bradford.gov.uk/housing/housing-assistance/what-financial-assistance-is-available/",
               "https://www.bradford.gov.uk/housing/housing-assistance/financial-assistance-for-homeowners-with-home-improvements-and-repairs/"},
         "Financial assistance for home improvements",
         "Would you like to know if you qualify for a Disabled Facilities Grant for adaptations?"),

        // Disabled adaptations / Disabled Facilities Grant
        (new[]{"disabled adaptation","disability grant","disabled facilities","DFG","stairlift","ramp","level access shower","widen doorway","ceiling hoist","adaptation","wheelchair","occupational therapy"},
         new[]{"https://www.bradford.gov.uk/housing/disabled-adaptations/disabled-facilities-grant/",
               "https://www.bradford.gov.uk/housing/disabled-adaptations/who-can-get-a-disabled-facilities-grant/"},
         "Disabled Facilities Grant",
         "Would you like to know about adult social care occupational therapy assessments, which are needed before applying?"),

        // Private sector lettings scheme (for landlords / tenants)
        (new[]{"private sector lettings","lettings scheme","landlord scheme","rent guarantee","bond guarantee","landlord liaison","find tenant","private rented","private rental"},
         new[]{"https://www.bradford.gov.uk/housing/private-sector-lettings-scheme/private-sector-lettings-scheme/"},
         "Bradford Council Private Sector Lettings Scheme",
         "Would you like to know about landlord legal duties or the Bradford Homes register for social housing?"),

        // Landlord legal duties / advice
        (new[]{"landlord","landlord duty","landlord legal","gas safety","electrical safety","fire safety","epc","energy performance","right to rent","deposit protection","tenancy agreement","HMO licence","letting property"},
         new[]{"https://www.bradford.gov.uk/housing/advice-for-landlords/advice-for-landlords/",
               "https://www.bradford.gov.uk/housing/advice-for-landlords/landlords-legal-duties/"},
         "Advice for landlords",
         "Would you like details on HMO licensing requirements or the minimum energy efficiency standards?"),

        // Tenant rights / advice
        (new[]{"tenant","tenant rights","renting","landlord not fixing","repair","damp","mould","eviction","retaliatory eviction","rent repayment","housing standards","disrepair","my landlord"},
         new[]{"https://www.bradford.gov.uk/housing/advice-for-tenants/advice-for-tenants/",
               "https://www.bradford.gov.uk/housing/advice-for-tenants/your-rights-and-responsibilities/"},
         "Advice for tenants",
         "Would you like to know how to report a repair issue to the Housing Standards Team, or about rent repayment orders?"),

        // HMO
        (new[]{"HMO","house in multiple occupation","multiple occupancy","shared house","bedsit","licensable","hmo licence"},
         new[]{"https://www.bradford.gov.uk/housing/houses-in-multiple-occupation/houses-in-multiple-occupation/",
               "https://www.bradford.gov.uk/housing/houses-in-multiple-occupation/do-i-need-a-hmo-licence/"},
         "Houses in Multiple Occupation (HMO)",
         "Would you like to know about HMO licence fees or standards required?"),

        // Empty homes
        (new[]{"empty home","empty property","vacant property","long-term empty","bring back into use","empty house","abandoned property"},
         new[]{"https://www.bradford.gov.uk/housing/empty-homes/empty-homes/",
               "https://www.bradford.gov.uk/housing/empty-homes/about-empty-homes/"},
         "Empty homes",
         "Would you like to know about financial assistance or loans available to bring an empty property back into use?"),

        // Supported housing
        (new[]{"supported housing","floating support","housing support","hostel","temporary accommodation","domestic violence housing","mental health housing","supported living","independence advice"},
         new[]{"https://www.bradford.gov.uk/housing/housing-related-support/what-is-housing-related-support/"},
         "Housing related support",
         "Would you like to know about homelessness services or Bradford's social housing options?"),

        // Council housing / Incommunities complaints
        (new[]{"council house","incommunities","council housing","housing complaint","social housing complaint","fletcher court","housing ombudsman"},
         new[]{"https://www.bradford.gov.uk/housing/council-housing/feedback-and-complaints-about-your-social-housing/"},
         "Council housing and complaints",
         "Would you like to know how to escalate a complaint to the Housing Ombudsman Service?"),

        // General fallback
        (new[]{"housing","home","accommodation","property","house","flat"},
         new[]{"https://www.bradford.gov.uk/housing/",
               "https://www.bradford.gov.uk/housing/finding-a-home/how-can-i-find-a-home/"},
         "Housing services in Bradford",
         "What housing help do you need? I can assist with homelessness, finding a home, repairs, landlord/tenant advice, and more."),
    };

    private async Task<string> GetHousingInfoAsync(string query, CancellationToken ct)
    {
        var q    = query.ToLower();
        var urls = new List<string>();
        var title    = "";
        var followUp = "";

        foreach (var (keywords, pages, pageTitle, pageFollowUp) in HousingKnowledgeMap)
        {
            if (keywords.Any(k => q.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var u in pages)
                    if (!urls.Contains(u)) urls.Add(u);
                if (string.IsNullOrEmpty(title))
                {
                    title    = pageTitle;
                    followUp = pageFollowUp;
                }
                if (urls.Count >= 2) break;
            }
        }

        if (urls.Count == 0)
        {
            urls.Add("https://www.bradford.gov.uk/housing/");
            title    = "Housing services in Bradford";
            followUp = "What housing help do you need? I can assist with homelessness, finding a home, repairs, landlord/tenant advice, and more.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"BRADFORD HOUSING INFORMATION — query: \"{query}\"");
        sb.AppendLine();

        foreach (var url in urls.Take(2))
        {
            var html = await FetchHtmlAsync(url, ct);
            if (string.IsNullOrEmpty(html)) continue;

            var doc = new HtmlDocument();
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

            var text = CleanText(main.InnerText);
            sb.AppendLine($"--- SOURCE: {url} ---");
            sb.AppendLine(TruncateText(text, 3500));
            sb.AppendLine();
        }

        sb.AppendLine($"OFFICIAL_BRADFORD_LINK: [{title}]({urls[0]})");
        if (!string.IsNullOrEmpty(followUp))
            sb.AppendLine($"FOLLOW_UP_SUGGESTION: {followUp}");

        return sb.ToString();
    }
}
