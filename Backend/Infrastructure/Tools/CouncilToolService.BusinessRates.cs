using System.Text;
using HtmlAgilityPack;

namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] BusinessRatesKnowledgeMap =
    {
        (new[]{"what are business rates","business rates explained","how are business rates calculated","rateable value","what is rateable value","business rate bill"},
         new[]{"https://www.bradford.gov.uk/business/business-rates/what-are-business-rates/",
               "https://www.bradford.gov.uk/business/business-rates/business-rates-full-explanation/"},
         "What are business rates?",
         "Would you like to know about business rate reliefs and exemptions that could reduce your bill?"),

        (new[]{"pay business rates","pay rates bill","how to pay business rates","business rates payment","pay my rates"},
         new[]{"https://www.bradford.gov.uk/business/business-rates/pay-your-business-rates-bill/"},
         "Pay your business rates bill",
         "Would you like to know about business rate reliefs that could reduce your bill?"),

        (new[]{"business rate relief","rates relief","small business relief","retail relief","charity relief","rates exemption","rate discount","mandatory relief","discretionary relief","rural rate relief"},
         new[]{"https://www.bradford.gov.uk/business/business-rates/business-rate-reliefs-and-exemptions/"},
         "Business rate reliefs and exemptions",
         "Would you like to know how to apply, or check your rateable value and appeal?"),

        (new[]{"business rates appeal","rateable value appeal","challenge rateable value","valuation office","VOA","rates valuation","rates appeal"},
         new[]{"https://www.bradford.gov.uk/business/business-rates/business-rate-valuation-and-appeals/"},
         "Business rates valuation and appeals",
         "Would you like to know about reliefs that could reduce your bill while an appeal is in progress?"),

        (new[]{"business rates contact","contact business rates","business rates team","rates query","rates question","rates help"},
         new[]{"https://www.bradford.gov.uk/business/business-rates/contact-the-business-rates-team/"},
         "Contact the Bradford business rates team",
         "You can also email businessrates@bradford.gov.uk or call 01274 432233."),

        (new[]{"changes to business rates","business rates 2026","business rates update","rates revaluation","business rates news"},
         new[]{"https://www.bradford.gov.uk/business/business-rates/changes-to-business-rates/"},
         "Changes to business rates",
         "Would you like to know about reliefs and exemptions to reduce your bill?"),

        (new[]{"business improvement district","BID","business rates BID"},
         new[]{"https://www.bradford.gov.uk/business/business-rates/business-improvement-districts-bids/"},
         "Business Improvement Districts (BIDs)",
         "Would you like to know more about business support in Bradford?"),

        (new[]{"business rates","rates bill","commercial property rates","non-domestic rates","NDR"},
         new[]{"https://www.bradford.gov.uk/business/business-rates/business-rates/"},
         "Business rates in Bradford",
         "What do you need help with? I can assist with paying, reliefs, valuations, appeals, or contacting the team."),
    };

    private async Task<string> GetBusinessRatesInfoAsync(string query, CancellationToken ct)
        => await ScrapeKnowledgeMapAsync(query, BusinessRatesKnowledgeMap,
            "https://www.bradford.gov.uk/business/business-rates/business-rates/",
            "Business rates in Bradford",
            "What do you need help with? I can assist with paying, reliefs, valuations, appeals, or contacting the team.",
            "BRADFORD BUSINESS RATES INFORMATION", ct);
}
