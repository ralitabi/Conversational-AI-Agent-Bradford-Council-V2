using System.Text;
using HtmlAgilityPack;

namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] HealthKnowledgeMap =
    {
        (new[]{"mental health","mental wellbeing","anxiety","depression","stress","mental health support","healthy minds","first response","crisis","emotional wellbeing"},
         new[]{"https://www.bradford.gov.uk/health/getting-help/mental-health/"},
         "Mental health support in Bradford",
         "For urgent mental health crisis support, call First Response on 0800 952 1181 (free, 24/7) or call 111."),

        (new[]{"alcohol","drugs","substance","addiction","drug abuse","drink","alcoholism","drug treatment","recovery","new vision","drug service"},
         new[]{"https://www.bradford.gov.uk/health/getting-help/help-with-alcohol-or-drugs/"},
         "Help with alcohol or drugs",
         "Would you like to know about other mental health or wellbeing support services?"),

        (new[]{"gambling","gambling harm","betting","gambling addiction","gambling support"},
         new[]{"https://www.bradford.gov.uk/health/getting-help/help-with-gambling-harm/"},
         "Help with gambling harm",
         "Would you like to know about other support services for your health and wellbeing?"),

        (new[]{"sexual health","STI","STD","contraception","HIV","sexual health clinic","condoms","chlamydia","sexual health test"},
         new[]{"https://www.bradford.gov.uk/health/getting-help/sexual-health/"},
         "Sexual health services in Bradford",
         "Would you like to know about other health services or how to book a health check?"),

        (new[]{"weight","obesity","overweight","lose weight","weight management","healthy weight","body weight","BMI"},
         new[]{"https://www.bradford.gov.uk/health/getting-help/manage-your-weight/"},
         "Weight management support",
         "Would you like to know about healthy eating or staying active in Bradford?"),

        (new[]{"quit smoking","stop smoking","smoking cessation","give up smoking","nicotine","vaping","e-cigarette"},
         new[]{"https://www.bradford.gov.uk/health/improving-your-health/quit-smoking/"},
         "Stop smoking support",
         "Would you like to know about other ways to improve your health?"),

        (new[]{"health check","NHS health check","book health check","free health check","health screening"},
         new[]{"https://www.bradford.gov.uk/health/improving-your-health/book-a-health-check/"},
         "Book a free NHS health check",
         "Would you like to know about cancer screening or vaccination programmes?"),

        (new[]{"vaccine","vaccination","immunisation","jab","flu jab","covid","booster","adult vaccine"},
         new[]{"https://www.bradford.gov.uk/health/protecting-your-health/vaccines-for-adults/"},
         "Adult vaccinations",
         "Would you like to know about cancer screening or children's vaccines?"),

        (new[]{"cancer screening","breast screening","cervical screening","bowel screening","prostate","smear test","mammogram"},
         new[]{"https://www.bradford.gov.uk/health/protecting-your-health/cancer-screening/"},
         "Cancer screening programmes",
         "Would you like to know about booking a health check or adult vaccinations?"),

        (new[]{"needle","syringe","sharps","needle exchange","needle programme","drug needle"},
         new[]{"https://www.bradford.gov.uk/health/getting-help/bradford-needle-and-syringe-programme/"},
         "Bradford Needle and Syringe Programme",
         "Would you like to know about drug and alcohol support services?"),

        (new[]{"baby","child health","children's health","breastfeeding","weaning","infant","new parent","newborn","child unwell","sick child"},
         new[]{"https://www.bradford.gov.uk/health/looking-after-your-baby-or-child/looking-after-your-baby-or-child/"},
         "Looking after your baby or child's health",
         "Would you like to know about childhood vaccinations or healthy teeth for children?"),

        (new[]{"children vaccine","child vaccination","MMR","children jab","baby vaccine","school vaccine"},
         new[]{"https://www.bradford.gov.uk/health/looking-after-your-baby-or-child/vaccines-for-children/"},
         "Vaccines for children",
         "Would you like to know about other child health services or baby health checks?"),

        (new[]{"cold weather","warm space","keep warm","heating","hypothermia","cold home","warm welcoming","winter health"},
         new[]{"https://www.bradford.gov.uk/health/protecting-your-health/advice-for-cold-weather/",
               "https://www.bradford.gov.uk/health/protecting-your-health/warmwelcoming-spaces/"},
         "Cold weather health advice and warm spaces",
         "Would you like to know about cost of living support or energy bill help?"),

        (new[]{"health","wellbeing","public health","healthy living","staying healthy","bradford health"},
         new[]{"https://www.bradford.gov.uk/health/"},
         "Bradford public health and wellbeing",
         "What health support are you looking for? I can help with mental health, substance support, sexual health, weight, smoking, or vaccinations."),
    };

    private async Task<string> GetHealthInfoAsync(string query, CancellationToken ct)
        => await ScrapeKnowledgeMapAsync(query, HealthKnowledgeMap,
            "https://www.bradford.gov.uk/health/",
            "Bradford public health and wellbeing",
            "What health support are you looking for? I can help with mental health, substance support, sexual health, weight, smoking, or vaccinations.",
            "BRADFORD HEALTH INFORMATION", ct);
}
