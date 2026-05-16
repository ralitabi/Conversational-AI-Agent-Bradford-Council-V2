namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] ElectionsKnowledgeMap =
    {
        (new[]{"register to vote","electoral register","voter registration","register for voting","am I registered","register vote"},
         new[]{"https://www.bradford.gov.uk/your-council/elections-and-voting/register-to-vote/"},
         "Register to vote",
         "Would you like to know about voter ID requirements or how to apply for a postal vote?"),

        (new[]{"postal vote","vote by post","apply postal vote","postal ballot","vote from home"},
         new[]{"https://www.bradford.gov.uk/your-council/elections-and-voting/voting-by-post/"},
         "Voting by post (postal vote)",
         "Would you like to know about proxy voting or voter ID requirements?"),

        (new[]{"proxy vote","vote by proxy","someone vote for me","proxy ballot"},
         new[]{"https://www.bradford.gov.uk/your-council/elections-and-voting/voting-by-proxy/"},
         "Voting by proxy",
         "Would you like to know about postal voting or voter ID requirements?"),

        (new[]{"voter ID","photo ID","ID to vote","polling station ID","what ID do I need","identification to vote"},
         new[]{"https://www.bradford.gov.uk/your-council/elections-and-voting/voter-id/"},
         "Voter ID requirements",
         "Would you like to know how to register to vote or apply for a postal vote?"),

        (new[]{"how to vote","polling station","vote in person","election day","where to vote","polling card","voting booth"},
         new[]{"https://www.bradford.gov.uk/your-council/elections-and-voting/how-to-vote-in-person/"},
         "How to vote in person",
         "Would you like to know about voter ID requirements or postal voting?"),

        (new[]{"stand as candidate","stand for election","become a councillor","council candidate","stand in election","nomination","candidate","run for council"},
         new[]{"https://www.bradford.gov.uk/your-council/elections-and-voting/standing-as-a-candidate/"},
         "Standing as a candidate in Bradford elections",
         "Would you like to know about election results or the current political composition of Bradford Council?"),

        (new[]{"election results","election result","council election","ward result","who won","who is my councillor","local election"},
         new[]{"https://www.bradford.gov.uk/your-council/elections-and-voting/election-results/",
               "https://www.bradford.gov.uk/your-council/elections-and-voting/district-council-elections-2026/"},
         "Bradford election results",
         "Would you like to know about upcoming scheduled elections or your local ward map?"),

        (new[]{"ward map","ward","constituency","my ward","electoral ward","ward boundary"},
         new[]{"https://www.bradford.gov.uk/your-council/elections-and-voting/ward-maps/"},
         "Bradford ward maps",
         "Would you like to know who your local councillor is or how to register to vote?"),

        (new[]{"election","vote","voting","democracy","ballot","council vote","local election","electoral"},
         new[]{"https://www.bradford.gov.uk/elections/"},
         "Elections and voting in Bradford",
         "What do you need help with? I can assist with registering to vote, postal votes, voter ID, polling stations, and standing as a candidate."),
    };

    private async Task<string> GetElectionsInfoAsync(string query, CancellationToken ct)
        => await ScrapeKnowledgeMapAsync(query, ElectionsKnowledgeMap,
            "https://www.bradford.gov.uk/elections/",
            "Elections and voting in Bradford",
            "What do you need help with? I can assist with registering to vote, postal votes, voter ID, polling stations, and standing as a candidate.",
            "BRADFORD ELECTIONS INFORMATION", ct);
}
