namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] SportsLeisureKnowledgeMap =
    {
        // ── Sports centres & pools (general) ──────────────────────────────────
        (new[]{"leisure centre","sports centre","gym","fitness centre","swimming pool","pool",
               "sports facility","leisure facility","bradford leisure centres","sports centres bradford"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/sports-centres-and-pools/sports-centres-and-pools/"},
         "Sports centres and swimming pools in Bradford",
         "Would you like to know about a specific centre, membership prices, or the Bradford Leisure Card?"),

        // ── Individual centres ─────────────────────────────────────────────────
        (new[]{"sedbergh","sedbergh sports","sedbergh leisure"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/sports-centres-and-pools/sedbergh-sports-and-leisure-centre/"},
         "Sedbergh Sports and Leisure Centre",
         "Would you like to know about membership prices or the Bradford Leisure Card?"),

        (new[]{"keighley leisure","keighley sports centre","keighley pool","keighley gym","leisure centre keighley"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/sports-centres-and-pools/the-leisure-centre-keighley/"},
         "The Leisure Centre Keighley",
         "Would you like to know about swimming lessons or fitness classes at Keighley?"),

        (new[]{"shipley pool","shipley gym","shipley leisure","shipley sports"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/sports-centres-and-pools/shipley-pool-and-gym/"},
         "Shipley Pool and Gym",
         "Would you like to know about membership prices or swimming lessons?"),

        (new[]{"eccleshill pool","eccleshill leisure","eccleshill swim"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/sports-centres-and-pools/eccleshill-pool/"},
         "Eccleshill Pool",
         "Would you like to know about other pools near you or swimming lessons?"),

        (new[]{"bowling pool","bowling leisure","bowling gym"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/sports-centres-and-pools/bowling-pool/"},
         "Bowling Pool",
         "Would you like to know about the Bradford Leisure Card or other leisure centres?"),

        (new[]{"ilkley pool","ilkley lido","ilkley leisure","ilkley swim"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/sports-centres-and-pools/ilkley-pool-and-lido/"},
         "Ilkley Pool and Lido",
         "Would you like to know about open-air swimming at the Lido or membership prices?"),

        (new[]{"manningham sports","manningham centre","manningham leisure","manningham gym"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/sports-centres-and-pools/manningham-sports-centre/"},
         "Manningham Sports Centre",
         "Would you like to know about fitness classes or the Bradford Leisure Card?"),

        (new[]{"marley activities","marley coaching","marley centre","marley sports"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/sports-centres-and-pools/marley-activities-and-coaching-centre/"},
         "Marley Activities and Coaching Centre",
         "Would you like to know about other sports facilities in Bradford?"),

        (new[]{"thornton recreation","thornton centre","thornton leisure"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/sports-centres-and-pools/thornton-recreation-centre/"},
         "Thornton Recreation Centre",
         "Would you like to know about membership or other leisure centres near you?"),

        (new[]{"wyke sports","wyke community","wyke leisure","wyke sports village"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/sports-centres-and-pools/wyke-community-sports-village/"},
         "Wyke Community Sports Village",
         "Would you like to know about the Bradford Leisure Card or other sports facilities?"),

        (new[]{"squire lane","new sports centre","new leisure centre bradford"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/new-sports-centres/squire-lane-sports-and-leisure-centre/",
               "https://www.bradford.gov.uk/sport-and-activities/new-sports-centres/new-sports-centres/"},
         "Squire Lane Sports and Leisure Centre (new)",
         "Would you like to know about other new sports facilities or the Bradford Leisure Card?"),

        (new[]{"accessible facilities","disabled sport","disability sport","inclusive sport",
               "wheelchair sport","disability swimming","disabled leisure","inclusive fitness"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/sports-centres-and-pools/accessible-facilities/",
               "https://www.bradford.gov.uk/sport-and-activities/membership-and-prices/inclusive-fitness-initiative/"},
         "Accessible and inclusive sports facilities in Bradford",
         "Would you like to know about the Bradford Leisure Card or specific accessible facilities?"),

        // ── Swimming ──────────────────────────────────────────────────────────
        (new[]{"swimming lesson","learn to swim","aquatics","swim lessons","kids swimming",
               "children swimming","adult swimming lessons","swimming tuition"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/swimming/swimming-lessons-with-bradford-aquatics/"},
         "Swimming lessons with Bradford Aquatics",
         "Would you like to know about pool prices, diving lessons or the competition programme?"),

        (new[]{"diving","diving lessons","diving bradford","competitive diving","learn to dive"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/swimming/diving/"},
         "Diving in Bradford",
         "Would you like to know about Bradford Aquatics swimming lessons or competition programmes?"),

        (new[]{"competitive swimming","swimming competition","aquatics competition","swimming club bradford",
               "swimming squad","swim development"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/swimming/bradford-aquatics-competition-development-programme/"},
         "Bradford Aquatics competition development programme",
         "Would you like to know about swimming lessons or pool prices?"),

        // ── Fitness classes ────────────────────────────────────────────────────
        (new[]{"fitness class","exercise class","aerobics","aqua class","group fitness",
               "aqua blast","aqua pulse","aquacise","yoga","pilates","water exercise"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/clubactive/clubactive-health-and-fitness/",
               "https://www.bradford.gov.uk/sport-and-activities/fitness-classes/aqua-blast/"},
         "Fitness classes and Clubactive in Bradford",
         "Would you like to know about Clubactive membership or the Bradford Leisure Card?"),

        // ── Membership & prices ───────────────────────────────────────────────
        (new[]{"leisure card","bradford leisure card","discount gym","reduced gym",
               "concessionary leisure","affordable gym","leisure card bradford"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/membership-and-prices/bradford-leisure-card/",
               "https://www.bradford.gov.uk/sport-and-activities/membership-and-prices/sports-centre-and-swimming-pool-prices/"},
         "Bradford Leisure Card and membership prices",
         "Would you like to know which leisure centres are near you or how to book activities?"),

        (new[]{"military gym","armed forces gym","free leisure","forces leisure","military free gym",
               "armed forces sport","veteran sport"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/membership-and-prices/free-access-to-leisure-centres-and-swimming-pools-for-members-of-the-british-armed-forces/"},
         "Free gym access for armed forces members",
         "Would you like to know about other leisure facilities or the Bradford Leisure Card?"),

        // ── Outdoor adventure ─────────────────────────────────────────────────
        (new[]{"outdoor adventure","canoeing","kayaking","water activities","Doe Park","doe park",
               "paddle","outdoor activities","adventure","outdoor education","outdoor pursuits"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/outdoor-adventure/doe-park-water-activities-centre/",
               "https://www.bradford.gov.uk/sport-and-activities/outdoor-adventure/adventure-activities-development-unit/"},
         "Outdoor adventure activities in Bradford",
         "Would you like to know about children's holiday courses, paddle activities or Buckden House?"),

        (new[]{"buckden house","buckden","outdoor residential","residential activity","outdoor centre buckden"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/activities/buckden-house/"},
         "Buckden House outdoor activity centre",
         "Would you like to know about Doe Park or other outdoor adventure activities?"),

        (new[]{"children holiday course","kids holiday sport","holiday activity doe park",
               "summer activity children","holiday course doe park"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/outdoor-adventure/doe-park-childrens-holiday-courses/"},
         "Doe Park children's holiday courses",
         "Would you like to know about the Holiday Activities and Food (HAF) programme or other activities?"),

        (new[]{"paddle and play","paddle play","paddleboarding","paddleboard","stand up paddle",
               "sup bradford","canoe play"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/outdoor-adventure/paddle-and-play/",
               "https://www.bradford.gov.uk/sport-and-activities/outdoor-adventure/paddle-and-pause/"},
         "Paddle and Play water activities",
         "Would you like to know about Doe Park or other water activity sessions?"),

        // ── Walking ───────────────────────────────────────────────────────────
        (new[]{"walking","guided walk","countryside walk","walking route","hiking","walking bradford",
               "walk bradford","country walk"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/walking/walking/",
               "https://www.bradford.gov.uk/sport-and-activities/walking/countryside-guided-walks/"},
         "Walking routes and guided walks in Bradford",
         "Would you like to see self-guided walk routes around specific areas of Bradford?"),

        (new[]{"self guided walk","self-guided walk","airedale walk","haworth walk","worth valley walk",
               "wharfedale walk","greenline","bradford walk route","walking map","walk route"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/walking/self-guided-walks-around-airedale/",
               "https://www.bradford.gov.uk/sport-and-activities/walking/self-guided-walks-around-haworth-stanbury-and-the-worth-valley/",
               "https://www.bradford.gov.uk/sport-and-activities/walking/self-guided-walks-around-wharfedale/"},
         "Self-guided walks around Bradford District",
         "Routes cover Airedale, Haworth, Worth Valley, Wharfedale and South Bradford. Would you like a specific area?"),

        // ── Cycling ───────────────────────────────────────────────────────────
        (new[]{"cycling","bikeability","cycle training","bicycle","cycling map","bike","cycle route",
               "cycling bradford","bike route"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/cycling/bikeability/",
               "https://www.bradford.gov.uk/sport-and-activities/cycling/bradford-district-cycle-map/"},
         "Cycling and Bikeability training in Bradford",
         "Would you like to know about cycling to work, active travel hubs or bicycle parking?"),

        (new[]{"active travel hub","ebike","e-bike","electric bike","bike hub","cycle hub",
               "active travel","cycling to work","cycle to work scheme","commute cycling"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/cycling/active-travel-hubs/",
               "https://www.bradford.gov.uk/sport-and-activities/cycling/cycling-to-work/"},
         "Active travel hubs and cycling to work",
         "Would you like to know about the Bradford e-bike share scheme or cycle parking?"),

        (new[]{"bicycle parking","bike parking","cycle parking","cycle storage","secure cycle"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/cycling/bicycle-parking/"},
         "Bicycle parking in Bradford",
         "Would you like to know about cycling routes or the Bradford cycle map?"),

        // ── Sports development ─────────────────────────────────────────────────
        (new[]{"sports camp","activity camp","summer camp sport","holiday sport camp",
               "kids sports camp","children sport camp","holiday activities"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/sports-development/sports-and-activities-camps/"},
         "Sports and activities camps in Bradford",
         "Would you like to know about the Holiday Activities and Food (HAF) programme or other youth activities?"),

        (new[]{"dance for life","dance fitness","dance class","dance bradford",
               "dance health","community dance"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/sports-development/dance-for-life/"},
         "Dance for Life",
         "Would you like to know about other fitness classes or over 50s activities?"),

        (new[]{"sports clubs","local sports club","sports club bradford","community sport",
               "join sports club","sports club district","find sports club"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/activities/sports-clubs-in-the-district/",
               "https://www.bradford.gov.uk/sport-and-activities/sports-development/community-sports-and-activities-development-unit/"},
         "Sports clubs and community sport in Bradford",
         "Would you like to know about sports development support or finding activities near you?"),

        // ── Book activities & stay active ──────────────────────────────────────
        (new[]{"book sports","book activities","book leisure","book swimming","book gym",
               "online booking sport","reserve sport"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/activities/book-sports-activities/"},
         "Book sports activities online",
         "Would you like to know about membership prices or the Bradford Leisure Card?"),

        (new[]{"stay active","get active","be active","active lifestyle","sport health",
               "physical activity","how to get fit","start exercising"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/activities/stay-active/"},
         "Getting active in Bradford",
         "Would you like to know about leisure centres, walking routes or fitness classes near you?"),

        // ── Events ────────────────────────────────────────────────────────────
        (new[]{"ride bradford","ride bradford 2026","cycling event","bike event bradford",
               "mass cycling","sportive bradford"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/activities/ride-bradford-2026/"},
         "Ride Bradford 2026",
         "Would you like to know about cycling routes or Bikeability training?"),

        (new[]{"over 50","50+","older adults activity","senior fitness","mature activities",
               "older people sport","50s sport","older adults fitness"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/activities/over-50s-activities/"},
         "Over 50s activities",
         "Would you like to know about the Bradford Leisure Card for discounted access?"),

        // ── Sports policies ────────────────────────────────────────────────────
        (new[]{"sports policy","swimming policy","swimming rules","pool rules","sport rules",
               "swimming regulations","leisure policy"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/policies/sports-policies/",
               "https://www.bradford.gov.uk/sport-and-activities/policies/swimming-policies-and-regulations/"},
         "Sports and swimming policies",
         "Would you like to know about accessible facilities or specific activity information?"),

        // ── General fallback ──────────────────────────────────────────────────
        (new[]{"sport","leisure","swim","gym","fitness","activity","recreation","active","exercise",
               "bradford sport","bradford leisure","bradford activities"},
         new[]{"https://www.bradford.gov.uk/sport-and-activities/"},
         "Sport and activities in Bradford",
         "What are you looking for? I can help with leisure centres, swimming, fitness classes, outdoor adventure, cycling, walking, and sports clubs."),
    };

    private async Task<string> GetSportsLeisureInfoAsync(string query, CancellationToken ct)
        => await ScrapeKnowledgeMapAsync(query, SportsLeisureKnowledgeMap,
            "https://www.bradford.gov.uk/sport-and-activities/",
            "Sport and activities in Bradford",
            "What are you looking for? I can help with leisure centres, swimming, fitness classes, outdoor adventure, cycling, walking, and sports clubs.",
            "BRADFORD SPORT & LEISURE INFORMATION", ct);
}
