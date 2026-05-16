using System.Text;
using System.Text.Json;
using Bradford.Core.Models;

namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string Name, string Address, string Postcode, string Phone, string Email,
                              double Lat, double Lon, string Slug, string Type, string[] Facilities, string OpeningHours)[]
        BradfordSportsCentres =
    {
        ("Sedbergh Sports and Leisure Centre",
         "Cleckheaton Road, Low Moor, Bradford", "BD12 0HQ",
         "01274 434707", "sport-and-leisure@bradford.gov.uk",
         53.7639, -1.7700, "sedbergh-sports-and-leisure-centre", "multi",
         new[]{"80-station gym","Swimming pool","Fitness classes (spin, pump)","Badminton","5-a-side football","Swimming lessons","Junior gym (ages 11–15)","Martial arts"},
         "Mon–Thu 6am–10pm · Fri 6am–9pm · Sat 7:30am–5pm · Sun 8:30am–5pm"),

        ("The Leisure Centre Keighley",
         "Hard Ings Road, Victoria Park, Keighley", "BD21 3JN",
         "01535 618585", "onlinebookingqueries@bradford.gov.uk",
         53.8706, -1.9082, "the-leisure-centre-keighley", "pool",
         new[]{"Swimming pool","Fitness classes","ClubActive gym","Swimming lessons","Pool parties","Accessible facilities"},
         "Check timetables online — call 01535 618585 for current hours"),

        ("Shipley Pool and Gym",
         "Alexandra Road, Shipley", "BD18 3ER",
         "01274 437162", "onlinebookingqueries@bradford.gov.uk",
         53.8342, -1.7780, "shipley-pool-and-gym", "pool",
         new[]{"25m main pool","Diving pool (2 high boards, 2 springboards)","Teaching pool","Gym","Fitness classes","Swimming lessons","Changing villages"},
         "Check timetables online — call 01274 437162 for current hours"),

        ("Eccleshill Pool",
         "Harrogate Road, Bradford", "BD10 0QE",
         "01274 432723", "onlinebookingqueries@bradford.gov.uk",
         53.8210, -1.7430, "eccleshill-pool", "pool",
         new[]{"33.5m main pool","Learner pool","20m waterslide","Diving boards","Gym","Fitness classes","Swimming lessons","Meeting room"},
         "Check timetables online — call 01274 432723 for current hours"),

        ("Bowling Pool and Gym",
         "Flockton Road, East Bowling, Bradford", "BD4 7RH",
         "01274 431750", "onlinebookingqueries@bradford.gov.uk",
         53.7820, -1.7262, "bowling-pool", "pool",
         new[]{"25m accessible pool (heated 32.5°C)","Gym","Studio room","Sauna","Changing Places facility","Swimming lessons","Aqua fitness","Pool parties"},
         "Check timetables online — call 01274 431750 for current hours"),

        ("Ilkley Pool and Lido",
         "Denton Road, Ilkley", "LS29 0BZ",
         "01943 436201", "ilkley.swimming@bradford.gov.uk",
         53.9230, -1.8190, "ilkley-pool-and-lido", "pool",
         new[]{"Indoor swimming pool","Outdoor lido (seasonal, unheated)","Tennis courts","Yoga & fitness classes","Swimming lessons","Swimming club","Café & picnic area"},
         "Lido opens seasonally (from May). Check timetables online — call 01943 436201"),

        ("Manningham Sports Centre",
         "Carlisle Road, Bradford", "BD8 8BA",
         "01274 436617", "onlinebookingqueries@bradford.gov.uk",
         53.8015, -1.7727, "manningham-sports-centre", "sports-hall",
         new[]{"Fitness centre","Double sports hall","5-a-side indoor football","Outdoor floodlit astroturf 6-a-side","Outdoor MACA 5-a-side pitch","ClubActive memberships"},
         "Mon 3pm–10pm · Tue–Fri 3pm–10:30pm · Sat 9am–5:30pm · Sun 10am–6:30pm"),

        ("Marley Activities and Coaching Centre",
         "Aireworth Road, Keighley", "BD21 4DB",
         "01535 618448", "marley.pitch@bradford.gov.uk",
         53.8632, -1.9076, "marley-activities-and-coaching-centre", "sports-hall",
         new[]{"Full-size floodlit 4G FieldTurf pitch","Indoor sports hall","8 grass football pitches","Rugby pitch","12 changing rooms","Pitch hire available"},
         "Mon–Thu 9am–10pm · Fri 9am–9pm · Sat–Sun 9am–6pm"),

        ("Thornton Recreation Centre",
         "Leaventhorpe Lane, Bradford", "BD13 3BH",
         "01274 436022", "thornton.recreation@bradford.gov.uk",
         53.7972, -1.8460, "thornton-recreation-centre", "gym",
         new[]{"ClubActive gym","Yoga","Pilates","Body combat","TRX","Circuit training","Live studio cycling","Virtual Les Mills classes"},
         "Mon 9am–9:30pm · Tue 6am–9:30pm · Wed 9am–9:30pm · Thu 6am–9:30pm · Fri 9am–9pm · Sat 8am–6pm · Sun 9am–4pm"),

        ("Wyke Community Sports Village",
         "Wilson Road, Wyke, Bradford", "BD12 9HA",
         "01274 437281", "clubactive.prices@bradford.gov.uk",
         53.7588, -1.7622, "wyke-community-sports-village", "multi",
         new[]{"Sports facilities","ClubActive gym memberships","Community sports"},
         "Mon–Fri 9am–10pm · Sat–Sun 9am–5pm"),
    };

    private async Task<string> FindSportsCentresNearPostcodeAsync(string postcode, CancellationToken ct)
    {
        postcode = postcode.Trim().ToUpper();
        if (!postcode.Contains(' ') && postcode.Length >= 5)
            postcode = postcode[..^3] + " " + postcode[^3..];

        var ll = await GetPostcodeLatLngAsync(postcode, ct);
        if (!ll.HasValue)
            return $"Could not locate postcode {postcode}. View all sports centres at: https://www.bradford.gov.uk/sport-and-activities/sports-centres-and-pools/sports-centres-and-pools/";

        var (userLat, userLon) = ll.Value;

        var sorted = BradfordSportsCentres
            .Select(c => (centre: c, miles: DistanceMiles(userLat, userLon, c.Lat, c.Lon)))
            .OrderBy(x => x.miles)
            .Select((x, i) => new SportsCentreOption
            {
                Number     = i + 1,
                Name       = x.centre.Name,
                Address    = $"{x.centre.Address}, {x.centre.Postcode}",
                Phone      = x.centre.Phone,
                Distance   = x.miles < 0.1 ? "< 0.1 mi" : $"{x.miles:F1} mi",
                Facilities = x.centre.Facilities.Take(4).ToList(),
                Slug       = x.centre.Slug,
                Type       = x.centre.Type
            })
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("[[SPORTS_CENTRES_LIST]]");
        sb.AppendLine(JsonSerializer.Serialize(sorted));
        sb.AppendLine("[[/SPORTS_CENTRES_LIST]]");
        sb.AppendLine();
        sb.AppendLine($"Found {sorted.Count} Bradford sports centres sorted by distance from {postcode}.");
        sb.AppendLine($"INSTRUCTION: Write ONE short sentence only — e.g. \"Here are Bradford's sports centres nearest to {postcode} — tap one to see full details and opening hours.\" Do NOT list them.");

        return sb.ToString();
    }

    private async Task<string> GetSportsCentreDetailsAsync(string centreName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(centreName))
            return "Please provide a sports centre name.";

        var match = BradfordSportsCentres
            .Select(c => (c, score: SportsCentreMatchScore(c.Name, centreName)))
            .OrderByDescending(x => x.score)
            .FirstOrDefault();

        if (match.c.Name == null || match.score == 0)
            return $"Sports centre '{centreName}' not found. View all at: https://www.bradford.gov.uk/sport-and-activities/sports-centres-and-pools/sports-centres-and-pools/";

        var centre = match.c;
        var pageUrl = $"https://www.bradford.gov.uk/sport-and-activities/sports-centres-and-pools/{centre.Slug}/";

        var card = new SportsCentreCard
        {
            Name         = centre.Name,
            Address      = $"{centre.Address}, {centre.Postcode}",
            Phone        = centre.Phone,
            Email        = centre.Email,
            OpeningHours = centre.OpeningHours,
            Facilities   = centre.Facilities.ToList(),
            PageUrl      = pageUrl,
            Type         = centre.Type
        };

        var sb = new StringBuilder();
        sb.AppendLine("[[SPORTS_CENTRE_CARD]]");
        sb.AppendLine(JsonSerializer.Serialize(card));
        sb.AppendLine("[[/SPORTS_CENTRE_CARD]]");
        sb.AppendLine();
        sb.AppendLine($"OFFICIAL_BRADFORD_LINK: [{centre.Name}]({pageUrl})");
        sb.AppendLine("INSTRUCTION: The UI shows a full details card automatically. Summarise in 1–2 sentences: key facilities and how to book.");

        return sb.ToString();
    }

    private static int SportsCentreMatchScore(string candidate, string query)
    {
        var c = candidate.ToLowerInvariant();
        var q = query.ToLowerInvariant();
        if (c == q) return 100;
        if (c.Contains(q) || q.Contains(c)) return 80;
        return query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Count(w => candidate.Contains(w, StringComparison.OrdinalIgnoreCase)) * 20;
    }
}
