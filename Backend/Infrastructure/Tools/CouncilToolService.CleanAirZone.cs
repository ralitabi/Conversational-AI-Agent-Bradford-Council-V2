using System.Text;
using HtmlAgilityPack;

namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] CleanAirZoneKnowledgeMap =
    {
        (new[]{"do i need to pay","check if i need to pay","caz charge","need to pay caz","vehicle check","my vehicle caz","will i be charged"},
         new[]{"https://www.bradford.gov.uk/clean-air-zone/payments-and-charges/check-if-you-need-to-pay/"},
         "Check if your vehicle needs to pay the Bradford CAZ charge",
         "Would you like to know the daily charge amounts or how to pay?"),

        (new[]{"how to pay caz","pay caz","pay clean air zone","daily caz charge","pay the charge"},
         new[]{"https://www.bradford.gov.uk/clean-air-zone/payments-and-charges/how-to-pay-the-daily-caz-charge/",
               "https://www.bradford.gov.uk/clean-air-zone/payments-and-charges/list-of-daily-charges/"},
         "How to pay the Bradford CAZ daily charge",
         "Would you like to know about CAZ exemptions or vehicle upgrade grants?"),

        (new[]{"caz exemption","exempt","exempt vehicle","exemption apply","am i exempt","caz exempt","not pay caz"},
         new[]{"https://www.bradford.gov.uk/clean-air-zone/what-help-is-available/exemptions/"},
         "CAZ exemptions — vehicles that don't pay",
         "Would you like to apply for an exemption or find out about vehicle upgrade grants?"),

        (new[]{"caz grant","vehicle grant","upgrade vehicle","retrofit","clean vehicle","grant electric","scrappage","vehicle funding"},
         new[]{"https://www.bradford.gov.uk/clean-air-zone/what-help-is-available/grants/"},
         "CAZ vehicle upgrade grants",
         "Would you like to check if your current vehicle needs to pay the charge?"),

        (new[]{"caz penalty","penalty notice","pcn caz","penalty charge caz","pay penalty caz","appeal caz","challenge caz penalty","caz fine"},
         new[]{"https://www.bradford.gov.uk/clean-air-zone/payments-and-charges/how-to-pay-the-caz-penalty-charge/",
               "https://www.bradford.gov.uk/clean-air-zone/payments-and-charges/how-to-appeal-a-caz-penalty-charge/"},
         "CAZ penalty charge — pay or appeal",
         "Would you like to know what happens if you don't pay a CAZ penalty?"),

        (new[]{"what happens if don't pay","non payment caz","ignore caz","bailiff caz","enforcement caz"},
         new[]{"https://www.bradford.gov.uk/clean-air-zone/payments-and-charges/what-will-happen-if-i-do-not-pay/"},
         "What happens if you don't pay the CAZ charge",
         "Would you like to know how to pay or appeal a CAZ charge?"),

        (new[]{"what is clean air zone","what is caz","caz information","how does caz work","why caz","bradford caz","air quality","where is caz","caz boundary","caz map"},
         new[]{"https://www.bradford.gov.uk/clean-air-zone/clean-air-zone-information/what-is-the-clean-air-zone-and-how-does-it-work/",
               "https://www.bradford.gov.uk/clean-air-zone/clean-air-zone-information/where-is-the-clean-air-zone/"},
         "What is the Bradford Clean Air Zone?",
         "Would you like to check if your vehicle needs to pay, or find out about exemptions?"),

        (new[]{"visiting bradford caz","visitor caz","visiting bradford","tourist caz","pass through bradford"},
         new[]{"https://www.bradford.gov.uk/clean-air-zone/clean-air-zone-information/visiting-bradford/"},
         "Visiting Bradford — Clean Air Zone information",
         "Would you like to check if your vehicle needs to pay the Bradford CAZ charge?"),

        (new[]{"caz faq","caz questions","clean air zone faq","clean air zone help"},
         new[]{"https://www.bradford.gov.uk/clean-air-zone/clean-air-zone-information/frequently-asked-questions/"},
         "Clean Air Zone frequently asked questions",
         "Would you like to check if your vehicle needs to pay or find out about exemptions?"),

        (new[]{"clean air zone","CAZ","air quality","emission","pollution","ULEZ","low emission"},
         new[]{"https://www.bradford.gov.uk/clean-air-zone/"},
         "Bradford Clean Air Zone",
         "Would you like to check if your vehicle needs to pay, or find out about exemptions and grants?"),
    };

    private async Task<string> GetCleanAirZoneInfoAsync(string query, CancellationToken ct)
        => await ScrapeKnowledgeMapAsync(query, CleanAirZoneKnowledgeMap,
            "https://www.bradford.gov.uk/clean-air-zone/",
            "Bradford Clean Air Zone",
            "Would you like to check if your vehicle needs to pay, or find out about exemptions and grants?",
            "BRADFORD CLEAN AIR ZONE INFORMATION", ct);
}
