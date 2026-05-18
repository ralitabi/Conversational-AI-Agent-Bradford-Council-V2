namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] CommunityKnowledgeMap =
    {
        (new[]{"allotment","allotments","grow your own","vegetable plot","waiting list allotment","apply allotment","allotment rent","allotment price"},
         new[]{"https://www.bradford.gov.uk/your-community/allotments/allotments/",
               "https://www.bradford.gov.uk/your-community/allotments/apply-for-an-allotment-and-view-the-allotments-waiting-list/"},
         "Allotments in Bradford",
         "Would you like to know the current waiting list situation or allotment rental costs?"),

        (new[]{"domestic abuse","domestic violence","DV","coercive control","abuse relationship","abusive partner","safe from abuse","refuge","DASV"},
         new[]{"https://www.bradford.gov.uk/your-community/domestic-abuse/domestic-and-sexual-abuse/"},
         "Domestic abuse support in Bradford",
         "If you are in immediate danger, call 999. The DASV Bradford helpline is 01274 431 080 (24/7)."),

        (new[]{"community grant","community funding","community chest","local grant","funding for community","community group funding","voluntary sector grant","Pride in Place"},
         new[]{"https://www.bradford.gov.uk/your-community/community-grants/community-grants/"},
         "Community grants and funding",
         "Would you like to know which area your grant application falls under — Bradford East, West, South, Keighley, or Shipley?"),

        (new[]{"gypsies","travellers","gypsy","traveller","encampment","travelling community","Romany","unauthorised encampment"},
         new[]{"https://www.bradford.gov.uk/your-community/gypsies-and-travellers/gypsies-and-travellers/"},
         "Gypsies and Travellers support",
         "Would you like to know about other community support services in Bradford?"),

        (new[]{"armed forces","military","veteran","veterans","forces covenant","armed forces community","military personnel","service personnel","reservist"},
         new[]{"https://www.bradford.gov.uk/your-community/armed-forces-community-support/armed-forces-community-support-in-bradford-district/"},
         "Armed forces community support",
         "Would you like to know about other community support services or benefits for veterans?"),

        (new[]{"asylum seeker","refugee","sanctuary","city of sanctuary","migrant","immigration support","new arrival Bradford"},
         new[]{"https://www.bradford.gov.uk/your-community/asylum-seekers-and-refugees/about-refugees-and-asylum-seekers/"},
         "Support for asylum seekers and refugees",
         "Would you like to know about other community support services in Bradford?"),

        (new[]{"community","community support","voluntary","neighbourhood","local community","your community"},
         new[]{"https://www.bradford.gov.uk/your-community/"},
         "Community services in Bradford",
         "What community support are you looking for? I can help with allotments, grants, domestic abuse, armed forces, and more."),
    };

    private async Task<string> GetCommunityInfoAsync(string query, CancellationToken ct)
        => await ScrapeKnowledgeMapAsync(query, CommunityKnowledgeMap,
            "https://www.bradford.gov.uk/your-community/",
            "Community services in Bradford",
            "What community support are you looking for? I can help with allotments, grants, domestic abuse, armed forces, and more.",
            "BRADFORD COMMUNITY INFORMATION", ct);
}
