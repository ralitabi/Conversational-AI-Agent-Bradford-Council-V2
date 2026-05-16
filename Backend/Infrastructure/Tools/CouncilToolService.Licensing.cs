namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] LicensingKnowledgeMap =
    {
        (new[]{"taxi","private hire","hackney carriage","cab","minicab","taxi licence","PHV licence","taxi driver licence","taxi vehicle licence"},
         new[]{"https://www.bradford.gov.uk/transport-and-travel/hackney-carriages-and-private-hire/hackney-carriage-and-private-hire-service/",
               "https://www.bradford.gov.uk/transport-and-travel/hackney-carriages-and-private-hire/drivers/"},
         "Taxi and private hire licensing",
         "Would you like to know about vehicle licensing requirements or application fees?"),

        (new[]{"food business","food hygiene","food registration","register food","food premises","food business registration","food hygiene rating","food premises approval"},
         new[]{"https://www.bradford.gov.uk/business/licensing/food-business-registration/",
               "https://www.bradford.gov.uk/business/licensing/food-premises-approval/"},
         "Food business registration and licensing",
         "Would you like to know about environmental health inspections or food hygiene ratings?"),

        (new[]{"gambling","betting","gaming","casino","lottery","bingo","amusements","gambling licence","gambling act"},
         new[]{"https://www.bradford.gov.uk/business/licensing/gambling-act-2005/"},
         "Gambling licences (Gambling Act 2005)",
         "Would you like to know about other business licensing requirements?"),

        (new[]{"alcohol","alcohol licence","premises licence","personal licence","licensing act","bar","pub","club","off licence","entertainment licence","regulated entertainment"},
         new[]{"https://www.bradford.gov.uk/business/licensing/licensing-act-2003/",
               "https://www.bradford.gov.uk/business/licensing/premises-licence/"},
         "Alcohol and entertainment licensing (Licensing Act 2003)",
         "Would you like to know about temporary event notices or club premises certificates?"),

        (new[]{"temporary event notice","TEN","temporary event","one-off event","small event licence"},
         new[]{"https://www.bradford.gov.uk/business/licensing/temporary-event-notice/"},
         "Temporary Event Notice (TEN)",
         "Would you like to know about premises licences for permanent venues?"),

        (new[]{"street trading","street trader","market stall","street stall","market consent","street vendor","outdoor market"},
         new[]{"https://www.bradford.gov.uk/business/licensing/street-traders-consent/"},
         "Street trading consent",
         "Would you like to know about car boot sales or outdoor seating licences?"),

        (new[]{"outdoor seating","pavement licence","tables outside","al fresco","cafe seating outside"},
         new[]{"https://www.bradford.gov.uk/business/licensing/outdoor-seating-licence/"},
         "Outdoor seating licence",
         "Would you like to know about street trading consent or other business licences?"),

        (new[]{"car boot sale","market","occasional market","boot sale","car boot"},
         new[]{"https://www.bradford.gov.uk/business/licensing/car-boot-sale-or-occasional-market-authorisation/"},
         "Car boot sale or occasional market authorisation",
         "Would you like to know about street trading or other licensing requirements?"),

        (new[]{"scrap metal","scrap dealer","scrap metal dealer","scrap licence"},
         new[]{"https://www.bradford.gov.uk/business/licensing/scrap-metal-dealers-licence/"},
         "Scrap metal dealer licence",
         "Would you like to know about other licensing requirements?"),

        (new[]{"tattooing","tattoo","ear piercing","piercing","acupuncture","electrolysis","beauty licence"},
         new[]{"https://www.bradford.gov.uk/business/licensing/acupuncture-ear-piercing-electrolysis-and-tattooing/"},
         "Tattooing, piercing and beauty treatment licensing",
         "Would you like to know about other business licensing requirements?"),

        (new[]{"animal licence","zoo","dangerous animal","animal activity","pet shop licence"},
         new[]{"https://www.bradford.gov.uk/business/licensing/animal-licences/",
               "https://www.bradford.gov.uk/business/licensing/zoo-licence/"},
         "Animal licensing",
         "Would you like to know about other licensing requirements?"),

        (new[]{"licensing fees","licence fee","licence cost","how much licence"},
         new[]{"https://www.bradford.gov.uk/business/licensing/licensing-fees/"},
         "Licensing fees in Bradford",
         "Would you like to know about a specific type of licence?"),

        (new[]{"licence","licensing","permit","permission","business permit","business licence","apply for licence"},
         new[]{"https://www.bradford.gov.uk/licensing/",
               "https://www.bradford.gov.uk/business/licensing/licensing-team-licences/"},
         "Licensing services in Bradford",
         "What type of licence do you need? I can help with taxis, food, alcohol, gambling, street trading, and more."),
    };

    private async Task<string> GetLicensingInfoAsync(string query, CancellationToken ct)
        => await ScrapeKnowledgeMapAsync(query, LicensingKnowledgeMap,
            "https://www.bradford.gov.uk/licensing/",
            "Licensing services in Bradford",
            "What type of licence do you need? I can help with taxis, food, alcohol, gambling, street trading, and more.",
            "BRADFORD LICENSING INFORMATION", ct);
}
