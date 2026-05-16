namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] EnvironmentKnowledgeMap =
    {
        (new[]{"dog warden","dog control","dog fouling","stray dog","dog barking","dog noise","dangerous dog","dog order"},
         new[]{"https://www.bradford.gov.uk/environment/dog-control/dog-control-and-dog-wardens/"},
         "Dog control and dog wardens",
         "Would you like to know about reporting other environmental issues?"),

        (new[]{"public right of way","footpath","bridleway","rights of way","blocked path","countryside walk","public footpath","right of way blocked","definitive map"},
         new[]{"https://www.bradford.gov.uk/environment/countryside/countryside-and-rights-of-way/"},
         "Public rights of way and countryside",
         "Would you like to know about guided walks or countryside access in Bradford?"),

        (new[]{"conservation area","conservation areas","listed area","heritage area","building in conservation","planning conservation"},
         new[]{"https://www.bradford.gov.uk/environment/conservation-areas/conservation-areas/"},
         "Conservation areas in Bradford",
         "Would you like to know about listed buildings or planning permission in conservation areas?"),

        (new[]{"listed building","listed buildings","grade 1","grade 2","listed building consent","alter listed building","listed building works","heritage building"},
         new[]{"https://www.bradford.gov.uk/environment/listed-buildings/listed-buildings/"},
         "Listed buildings",
         "Would you like to know about conservation areas or applying for listed building consent?"),

        (new[]{"biodiversity","wildlife","nature","protected species","habitat","net gain","biodiversity net gain"},
         new[]{"https://www.bradford.gov.uk/environment/biodiversity/biodiversity/"},
         "Biodiversity and wildlife",
         "Would you like to know about conservation areas or countryside access?"),

        (new[]{"climate change","carbon","sustainability","renewable energy","electric vehicle","EV charging","decarbonisation","net zero","climate action"},
         new[]{"https://www.bradford.gov.uk/environment/climate-change/climate-change/"},
         "Climate change and sustainability",
         "Would you like to know about the Clean Air Zone or electric vehicle charging?"),

        (new[]{"saltaire","world heritage","saltaire world heritage","UNESCO"},
         new[]{"https://www.bradford.gov.uk/environment/saltaire-world-heritage-site/saltaire-world-heritage-site/"},
         "Saltaire World Heritage Site",
         "Would you like to know about conservation areas or listed buildings in Bradford?"),

        (new[]{"heritage","heritage zone","heritage grant","historic building grant","heritage action zone"},
         new[]{"https://www.bradford.gov.uk/environment/heritage-action-zone/heritage-action-zone/"},
         "Heritage Action Zone",
         "Would you like to know about grants for listed buildings or conservation area guidance?"),

        (new[]{"park","parks","public park","bradford park","registered park","open space"},
         new[]{"https://www.bradford.gov.uk/environment/registered-parks-and-battlefields/registered-parks-and-battlefields/"},
         "Parks and open spaces in Bradford",
         "Would you like to know about countryside walks or sport and leisure facilities?"),

        (new[]{"environment","environmental","countryside","nature","green space","ecology","outdoor"},
         new[]{"https://www.bradford.gov.uk/environment/"},
         "Environment services in Bradford",
         "What environmental topic can I help with? I can assist with dog control, footpaths, conservation, listed buildings, and climate."),
    };

    private async Task<string> GetEnvironmentInfoAsync(string query, CancellationToken ct)
        => await ScrapeKnowledgeMapAsync(query, EnvironmentKnowledgeMap,
            "https://www.bradford.gov.uk/environment/",
            "Environment services in Bradford",
            "What environmental topic can I help with? I can assist with dog control, footpaths, conservation, listed buildings, and climate.",
            "BRADFORD ENVIRONMENT INFORMATION", ct);
}
