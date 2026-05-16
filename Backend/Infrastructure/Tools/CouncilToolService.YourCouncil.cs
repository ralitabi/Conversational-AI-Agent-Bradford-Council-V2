namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] YourCouncilKnowledgeMap =
    {
        // ── About Bradford Council ────────────────────────────────────────────
        (new[]{"about bradford council","about the council","how does bradford council work","how council works",
               "council structure","council organisation","about city of bradford"},
         new[]{"https://www.bradford.gov.uk/your-council/about-bradford-council/about-bradford-council/",
               "https://www.bradford.gov.uk/your-council/about-bradford-council/how-bradford-council-works/"},
         "About Bradford Council",
         "Would you like to know about your local councillors or how to attend a council meeting?"),

        // ── Chief Executive / senior leadership ───────────────────────────────
        (new[]{"chief executive","corporate management","senior leadership","council leadership team",
               "head of bradford council","council ceo","director bradford council"},
         new[]{"https://www.bradford.gov.uk/your-council/about-bradford-council/chief-executive-and-the-corporate-management-team/"},
         "Chief Executive and Corporate Management Team",
         "Would you like to know how Bradford Council works or who your local councillor is?"),

        // ── Council constitution ───────────────────────────────────────────────
        (new[]{"council constitution","constitution bradford","council rules","council governance rules",
               "how decisions made council","council decision making","standing orders"},
         new[]{"https://www.bradford.gov.uk/your-council/about-bradford-council/councils-constitution/"},
         "Bradford Council constitution",
         "Would you like to know about committee meetings or how to attend a council meeting?"),

        // ── National Fraud Initiative ──────────────────────────────────────────
        (new[]{"national fraud initiative","NFI","data matching fraud","council fraud data","fraud initiative"},
         new[]{"https://www.bradford.gov.uk/your-council/about-bradford-council/national-fraud-initiative/"},
         "National Fraud Initiative",
         "Would you like to know about reporting fraud to Bradford Council?"),

        // ── Modern slavery ────────────────────────────────────────────────────
        (new[]{"modern slavery","modern slavery statement","human trafficking council",
               "slavery bradford","modern slavery act","trafficking statement"},
         new[]{"https://www.bradford.gov.uk/your-council/about-bradford-council/modern-slavery-statement/"},
         "Modern Slavery Statement",
         "Would you like to know about Bradford Council's equality and diversity commitments?"),

        // ── Political composition ─────────────────────────────────────────────
        (new[]{"political composition","council composition","which party controls","council party",
               "labour bradford","conservative bradford","how many councillors","political makeup",
               "which party bradford council","current council composition"},
         new[]{"https://www.bradford.gov.uk/your-council/about-bradford-council/the-political-composition-of-bradford-council/"},
         "Political composition of Bradford Council",
         "Would you like to know about your local councillors or upcoming elections?"),

        // ── Whistleblowing ────────────────────────────────────────────────────
        (new[]{"whistleblowing","whistle blowing","whistleblower","report wrongdoing council",
               "report misconduct council","council wrongdoing","public interest disclosure"},
         new[]{"https://www.bradford.gov.uk/your-council/about-bradford-council/whistleblowing-policy/"},
         "Whistleblowing policy",
         "For fraud specifically, you can also report via the National Fraud Initiative or Bradford Council's counter-fraud team."),

        // ── Best Value Notice ─────────────────────────────────────────────────
        (new[]{"best value notice","best value","council improvement","bradford improvement","government intervention",
               "best value inspection","council performance"},
         new[]{"https://www.bradford.gov.uk/your-council/about-bradford-council/best-value-notice/",
               "https://www.bradford.gov.uk/your-council/about-bradford-council/bradford-improvement-panel/"},
         "Best Value Notice and Bradford Improvement Panel",
         "Would you like to know about council budgets and spending or how Bradford Council works?"),

        // ── Bradford Improvement Panel ────────────────────────────────────────
        (new[]{"improvement panel","bradford panel","council improvement panel","government commissioner",
               "commissioner bradford","council oversight"},
         new[]{"https://www.bradford.gov.uk/your-council/about-bradford-council/bradford-improvement-panel/"},
         "Bradford Improvement Panel",
         "Would you like to know about Bradford Council's budget and spending plans?"),

        // ── Committees, meetings & minutes ─────────────────────────────────────
        (new[]{"committee meeting","council meeting","attend council meeting","council minutes",
               "meeting agenda","council agenda","committee agenda","watch council meeting",
               "public meeting bradford","full council meeting","cabinet meeting"},
         new[]{"https://www.bradford.gov.uk/your-council/committees-meetings-and-minutes/meetings-and-minutes/",
               "https://www.bradford.gov.uk/your-council/committees-meetings-and-minutes/council-meetings-minutes-reports-and-agendas/"},
         "Council committees, meetings and minutes",
         "Council meetings are open to the public. Would you like to know about scrutiny committees or your local councillors?"),

        // ── Councillors and democracy ──────────────────────────────────────────
        (new[]{"councillors and democracy","democratic services","council democracy","democratic accountability"},
         new[]{"https://www.bradford.gov.uk/your-council/committees-meetings-and-minutes/councillors-and-democracy/"},
         "Councillors and democracy",
         "Would you like to find your local councillor or know about upcoming council meetings?"),

        // ── Portfolio holders / cabinet ────────────────────────────────────────
        (new[]{"portfolio holder","cabinet member","councillor responsible","lead councillor",
               "portfolio bradford","cabinet bradford","who is responsible for"},
         new[]{"https://www.bradford.gov.uk/your-council/committees-meetings-and-minutes/portfolio-holders/"},
         "Portfolio holders and cabinet",
         "Would you like to know about committee meetings or how to contact Bradford Council?"),

        // ── Your councillors ──────────────────────────────────────────────────
        (new[]{"my councillor","my local councillor","who is my councillor","find my councillor",
               "ward councillor","local elected representative","councillor contact",
               "councillor bradford","elected member","find councillor"},
         new[]{"https://www.bradford.gov.uk/your-council/your-councillors/your-councillors/"},
         "Find your local councillor",
         "You can find your ward and councillors on the Bradford Council website using your postcode. Would you like to know about contacting your councillor?"),

        // ── Council budgets & spending ────────────────────────────────────────
        (new[]{"council budget","bradford budget","council spending","how council spends",
               "council finances","budget 2026","council accounts","how much council spends",
               "council revenue","capital programme","medium term financial"},
         new[]{"https://www.bradford.gov.uk/your-council/council-budgets-and-spending/council-budgets-and-spending/",
               "https://www.bradford.gov.uk/your-council/council-budgets-and-spending/bradford-council-fees-and-charges/"},
         "Council budgets and spending",
         "Would you like to know about the council's fees and charges or the budget consultation?"),

        // ── Council fees & charges ────────────────────────────────────────────
        (new[]{"council fees","council charges","fees and charges bradford","how much does council charge",
               "schedule of charges","council fee list"},
         new[]{"https://www.bradford.gov.uk/your-council/council-budgets-and-spending/bradford-council-fees-and-charges/"},
         "Bradford Council fees and charges",
         "Would you like to know about a specific service charge, such as planning fees or building regulation charges?"),

        // ── Council buildings / offices ───────────────────────────────────────
        (new[]{"council building","council office","city hall bradford","britannia house",
               "keighley town hall","margaret mcmillan tower","council offices bradford",
               "where is bradford council","visit bradford council","council address"},
         new[]{"https://www.bradford.gov.uk/your-council/council-buildings/council-buildings/",
               "https://www.bradford.gov.uk/your-council/council-buildings/britannia-house/"},
         "Bradford Council buildings and offices",
         "Bradford Council's main office is Britannia House, Hall Ings, Bradford BD1 1HX. Would you like directions or contact details?"),

        // ── City Hall ────────────────────────────────────────────────────────
        (new[]{"city hall","bradford city hall","hire city hall","city hall events","city hall bradford venue"},
         new[]{"https://www.bradford.gov.uk/your-council/council-buildings/city-hall/"},
         "Bradford City Hall",
         "Would you like to know about other council venues or the Lord Mayor?"),

        // ── Lord Mayor ────────────────────────────────────────────────────────
        (new[]{"lord mayor","lord mayor bradford","invite lord mayor","lord mayor appeal",
               "lord mayor event","mayor bradford","civic mayor","deputy lord mayor",
               "lord mayor ceremony","civic regalia"},
         new[]{"https://www.bradford.gov.uk/your-council/the-lord-mayor/the-lord-mayor/",
               "https://www.bradford.gov.uk/your-council/the-lord-mayor/inviting-the-lord-mayor-to-your-event/"},
         "The Lord Mayor of Bradford",
         "To invite the Lord Mayor to your event, complete the invitation request form on the Bradford Council website."),

        // ── Lord Mayor appeal ────────────────────────────────────────────────
        (new[]{"lord mayor appeal","mayor charity","lord mayor charity","mayor fundraising",
               "lord mayor fund","civic appeal"},
         new[]{"https://www.bradford.gov.uk/your-council/the-lord-mayor/lord-mayors-appeal/"},
         "Lord Mayor's Appeal",
         "Would you like to know about community grants or other funding opportunities in Bradford?"),

        // ── ePetitions ────────────────────────────────────────────────────────
        (new[]{"petition","epetition","sign petition","create petition","council petition",
               "bradford petition","online petition council","petition bradford council"},
         new[]{"https://www.bradford.gov.uk/your-council/epetitions/epetitions/",
               "https://www.bradford.gov.uk/your-council/epetitions/petition-guidance/"},
         "ePetitions — Bradford Council",
         "Petitions with 750+ signatures are considered for a full Council debate. Would you like guidance on creating or signing a petition?"),

        // ── Report fraud ──────────────────────────────────────────────────────
        (new[]{"report fraud","council fraud","benefit fraud","housing fraud","report benefit fraud",
               "council tax fraud","tenancy fraud","fraud bradford council","report fraud bradford",
               "suspected fraud","fraud hotline"},
         new[]{"https://www.bradford.gov.uk/your-council/report-fraud/report-fraud/",
               "https://www.bradford.gov.uk/your-council/report-fraud/types-of-fraud/"},
         "Report fraud to Bradford Council",
         "You can report fraud online, by phone, or anonymously via Crimestoppers 0800 555 111. Would you like to know about the types of fraud Bradford Council investigates?"),

        // ── Types / about fraud ───────────────────────────────────────────────
        (new[]{"types of fraud","what is fraud","council tax evasion","benefit fraud what",
               "housing fraud what","fraud examples","fraud explained"},
         new[]{"https://www.bradford.gov.uk/your-council/report-fraud/about-fraud/",
               "https://www.bradford.gov.uk/your-council/report-fraud/types-of-fraud/"},
         "Types of fraud Bradford Council investigates",
         "Would you like to know how to report fraud to Bradford Council?"),

        // ── Counter-fraud policy ──────────────────────────────────────────────
        (new[]{"counter fraud policy","anti fraud policy","anti bribery","fraud policy bradford",
               "bribery policy","anti money laundering","counter fraud strategy"},
         new[]{"https://www.bradford.gov.uk/your-council/report-fraud/counter-fraud-policy/"},
         "Counter-fraud and anti-bribery policy",
         "Would you like to know how to report suspected fraud?"),

        // ── Equality and diversity ────────────────────────────────────────────
        (new[]{"equality","diversity","equality and diversity","equality duty","public sector equality",
               "discrimination council","equality impact","protected characteristics",
               "bradford equality","council equality commitment"},
         new[]{"https://www.bradford.gov.uk/your-council/equality-and-diversity/equality-and-diversity/"},
         "Equality and diversity at Bradford Council",
         "Would you like to know about Bradford Council's anti-poverty strategy or community support?"),

        // ── Scrutiny ─────────────────────────────────────────────────────────
        (new[]{"scrutiny","overview and scrutiny","scrutiny committee","hold council to account",
               "scrutiny bradford","scrutiny review","challenge council decisions"},
         new[]{"https://www.bradford.gov.uk/your-council/scrutiny/scrutiny-at-bradford-council/"},
         "Scrutiny at Bradford Council",
         "Scrutiny committees hold the executive to account. Would you like to know about committee meetings or how to attend?"),

        // ── Parish & town councils ─────────────────────────────────────────────
        (new[]{"parish council","town council","local council","parish councillor","town councillor",
               "neighbourhood council","community council","parish meeting"},
         new[]{"https://www.bradford.gov.uk/your-council/parish-councils/parish-and-town-councils-local-councils/"},
         "Parish and town councils in Bradford",
         "Would you like to find your local councillor or know about Bradford Council's ward structure?"),

        // ── Community right to challenge ──────────────────────────────────────
        (new[]{"community right to challenge","right to challenge","community challenge",
               "run council service","take over council service","community bid service"},
         new[]{"https://www.bradford.gov.uk/your-council/the-community-right-to-challenge/the-community-right-to-challenge/"},
         "Community Right to Challenge",
         "Would you like to know about community grants or the Bradford District Partnership?"),

        // ── Bradford District Partnership ──────────────────────────────────────
        (new[]{"bradford district partnership","BDP","district partnership","partnership board",
               "bradford partnership","strategic partnership","public service partnership"},
         new[]{"https://www.bradford.gov.uk/your-council/bradford-district-partnership-structure/bradford-district-partnership-structure/"},
         "Bradford District Partnership",
         "Would you like to know about Bradford Council's key policies or community support?"),

        // ── Budget consultation ────────────────────────────────────────────────
        (new[]{"budget consultation","council budget consultation","have your say budget",
               "budget engagement","budget proposals","budget survey","spending consultation"},
         new[]{"https://www.bradford.gov.uk/your-council/council-budget-proposals-engagement-and-consultation/council-budget-proposals-engagement-and-consultation/"},
         "Bradford Council budget consultation",
         "Would you like to know about council budgets and spending, or the council's fees and charges?"),

        // ── General Your Council fallback ─────────────────────────────────────
        (new[]{"your council","bradford council information","council governance","how council works",
               "bradford metropolitan council","city of bradford","bradford mdcc"},
         new[]{"https://www.bradford.gov.uk/your-council/"},
         "Your Bradford Council",
         "What would you like to know about Bradford Council? I can help with councillors, meetings, budgets, fraud reporting, petitions and more."),
    };

    private async Task<string> GetYourCouncilInfoAsync(string query, CancellationToken ct)
        => await ScrapeKnowledgeMapAsync(query, YourCouncilKnowledgeMap,
            "https://www.bradford.gov.uk/your-council/",
            "Your Bradford Council",
            "What would you like to know about Bradford Council? I can help with councillors, meetings, budgets, fraud reporting, petitions and more.",
            "BRADFORD COUNCIL GOVERNANCE INFORMATION", ct);
}
