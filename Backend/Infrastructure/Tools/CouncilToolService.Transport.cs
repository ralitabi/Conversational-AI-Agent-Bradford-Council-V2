using System.Text;
using HtmlAgilityPack;

namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] TransportKnowledgeMap =
    {
        (new[]{"pothole","uneven surface","road damage","hole in road","report road","cracked road","damaged road"},
         new[]{"https://www.bradford.gov.uk/transport-and-travel/report-issues/report-a-pothole-or-uneven-surface/"},
         "Report a pothole or uneven road surface",
         "Would you like to report other street issues like blocked drains or faulty street lights?"),

        (new[]{"parking permit","resident permit","parking zone","controlled parking","parking restriction","zone permit"},
         new[]{"https://www.bradford.gov.uk/transport-and-travel/parking/parking-permits/"},
         "Parking permits in Bradford",
         "Would you like to know about parking charges or pay a parking penalty?"),

        (new[]{"parking penalty","parking fine","PCN","penalty charge notice","pay parking fine","bus lane fine","challenge parking fine","appeal parking"},
         new[]{"https://www.bradford.gov.uk/transport-and-travel/parking/pay-your-parking-penalty-or-bus-lane-penalty/",
               "https://www.bradford.gov.uk/transport-and-travel/parking/view-evidence-or-challenge-your-parking-or-bus-lane-penalty/"},
         "Pay or challenge a parking penalty",
         "Would you like to know about parking permits or parking zones in Bradford?"),

        (new[]{"blue badge","disabled parking","disabled badge","disabled driver","blue badge parking","blue badge misuse"},
         new[]{"https://www.bradford.gov.uk/transport-and-travel/transport-for-disabled-people/blue-badge-scheme/",
               "https://www.bradford.gov.uk/transport-and-travel/transport-for-disabled-people/transport-for-disabled-people/"},
         "Blue Badge scheme for disabled drivers",
         "Would you like to know about concessionary travel passes for disabled people?"),

        (new[]{"bus pass","concessionary fares","free bus pass","older person bus pass","disabled bus pass","free travel","bus pass elderly","senior bus pass"},
         new[]{"https://www.bradford.gov.uk/transport-and-travel/transport-for-disabled-people/concessionary-fares-scheme-for-disabled-people-and-older-people/"},
         "Concessionary fares and free bus passes",
         "Would you like to know about the Blue Badge scheme for disabled parking?"),

        (new[]{"roadworks","road closure","road closed","roadworks map","planned roadworks","traffic","diversion"},
         new[]{"https://www.bradford.gov.uk/transport-and-travel/roadworks/map-of-roadworks/"},
         "Roadworks map and planned road closures",
         "Would you like to report a pothole or other road issue?"),

        (new[]{"gritting","grit","gritting route","salt","snow road","icy road","winter road","grit bin"},
         new[]{"https://www.bradford.gov.uk/transport-and-travel/winter-maintenance/winter-maintenance/",
               "https://www.bradford.gov.uk/transport-and-travel/winter-maintenance/how-when-and-where-we-grit/"},
         "Winter gritting and road maintenance",
         "Would you like to find your nearest grit bin location?"),

        (new[]{"abandoned vehicle","nuisance vehicle","abandoned car","broken down car","untaxed car","remove vehicle"},
         new[]{"https://www.bradford.gov.uk/transport-and-travel/abandoned-vehicles/abandoned-and-nuisance-vehicles/"},
         "Report an abandoned or nuisance vehicle",
         "Would you like to know about other issues you can report to Bradford Council?"),

        (new[]{"cycling","cycle route","cycle lane","bike","cycling infrastructure","cycle path","queensbury tunnel","bikeability"},
         new[]{"https://www.bradford.gov.uk/transport-and-travel/cycling/cycling/"},
         "Cycling in Bradford",
         "Would you like to know about the Queensbury Tunnel Greenway cycling route?"),

        (new[]{"street light","street lighting","broken light","faulty light","report street light","lamp post"},
         new[]{"https://www.bradford.gov.uk/transport-and-travel/report-issues/report-faulty-street-lights/"},
         "Report a faulty street light",
         "Would you like to report other issues like potholes, fly-tipping, or blocked drains?"),

        (new[]{"fly tipping","flytipping","illegal dumping","dump rubbish","report fly tip","waste dumped"},
         new[]{"https://www.bradford.gov.uk/transport-and-travel/report-issues/report-fly-tipping/"},
         "Report fly-tipping",
         "Would you like to know about household waste recycling centres for legal disposal?"),

        (new[]{"blocked drain","blocked gully","flooded drain","drain blocked","gully","drainage"},
         new[]{"https://www.bradford.gov.uk/transport-and-travel/report-issues/report-blocked-gullies-and-drains/"},
         "Report blocked gullies and drains",
         "Would you like to report other road or street issues?"),

        (new[]{"taxi","private hire","hackney carriage","cab","minicab","taxi driver","taxi licence","PHV"},
         new[]{"https://www.bradford.gov.uk/transport-and-travel/hackney-carriages-and-private-hire/hackney-carriage-and-private-hire-service/"},
         "Taxis and private hire in Bradford",
         "Would you like to know about taxi driver licences or vehicle requirements?"),

        (new[]{"road safety","speeding","speed camera","dangerous driving","op snap","dashcam","dash cam"},
         new[]{"https://www.bradford.gov.uk/transport-and-travel/road-safety/road-safety/"},
         "Road safety",
         "Would you like to report a community concern about speeding via Op SNAP?"),

        (new[]{"public transport","bus","train","travel","getting around bradford","transport bradford"},
         new[]{"https://www.bradford.gov.uk/transport-and-travel/public-transport/public-transport/"},
         "Public transport in Bradford",
         "Would you like to know about bus passes or concessionary fares?"),

        (new[]{"transport","road","parking","travel","highway","traffic"},
         new[]{"https://www.bradford.gov.uk/transport-and-travel/"},
         "Transport and travel in Bradford",
         "What transport help do you need? I can assist with parking, potholes, bus passes, roadworks, cycling, and more."),
    };

    private async Task<string> GetTransportInfoAsync(string query, CancellationToken ct)
        => await ScrapeKnowledgeMapAsync(query, TransportKnowledgeMap,
            "https://www.bradford.gov.uk/transport-and-travel/",
            "Transport and travel in Bradford",
            "What transport help do you need? I can assist with parking, potholes, bus passes, roadworks, cycling, and more.",
            "BRADFORD TRANSPORT & TRAVEL INFORMATION", ct);
}
