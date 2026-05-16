namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] BirthsDeathsMarriagesKnowledgeMap =
    {
        // ── Register a birth ──────────────────────────────────────────────────
        (new[]{"register a birth","register birth","registering a birth","baby born","new baby register",
               "birth certificate","birth registration","register my baby","register newborn"},
         new[]{"https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/births-and-naming/register-a-birth/",
               "https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/births-and-naming/births-and-naming/"},
         "Register a birth in Bradford",
         "Births must be registered within 42 days. Would you like to know about naming ceremonies or getting a copy birth certificate?"),

        // ── Register a stillbirth ─────────────────────────────────────────────
        (new[]{"stillbirth","register stillbirth","registering stillbirth","baby died","born sleeping"},
         new[]{"https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/births-and-naming/register-a-stillbirth/"},
         "Register a stillbirth",
         "Bradford Register Office offers a sensitive and private appointment. Would you like the contact details?"),

        // ── Naming ceremonies ─────────────────────────────────────────────────
        (new[]{"naming ceremony","baby naming","child naming ceremony","non-religious naming","humanist naming",
               "welcome ceremony","name day ceremony"},
         new[]{"https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/births-and-naming/naming-ceremonies/"},
         "Naming ceremonies at Bradford Register Office",
         "Naming ceremonies are a non-legal celebration. Would you like to know about approved venues in Bradford?"),

        // ── Changing a child's name ───────────────────────────────────────────
        (new[]{"change child name","change childs name","change baby name","change name on birth certificate",
               "deed poll child","change name register","child name change"},
         new[]{"https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/births-and-naming/changing-a-childs-name/"},
         "Changing a child's name",
         "Would you like to know about deed polls or getting a copy of a birth certificate?"),

        // ── Register a death ──────────────────────────────────────────────────
        (new[]{"register a death","register death","registering a death","someone has died","death registration",
               "death certificate","tell a death","register death bradford"},
         new[]{"https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/deaths/register-a-death/",
               "https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/deaths/bereavement-burials-and-cremations/"},
         "Register a death in Bradford",
         "Deaths must be registered within 5 days. Would you like to know about funerals, burials or cremations in Bradford?"),

        // ── Bereavement, funerals, burials ────────────────────────────────────
        (new[]{"bereavement","funeral","burial","cemetery","grave","bury","interment",
               "bradford cemetery","bereavement service","funeral arrangements bradford"},
         new[]{"https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/deaths/bereavement-burials-and-cremations/",
               "https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/deaths/burials-and-cemeteries/"},
         "Bereavement, burials and cemeteries in Bradford",
         "Would you like to know about cremation services or the cost of bereavement services?"),

        // ── Burials and cemeteries ────────────────────────────────────────────
        (new[]{"cemetery","cemeteries","burial plot","grave plot","bury someone","bradford cemeteries",
               "grave purchase","burial rights"},
         new[]{"https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/deaths/burials-and-cemeteries/"},
         "Burials and cemeteries in Bradford",
         "Would you like to know about bereavement service prices or cremation options?"),

        // ── Cremations ────────────────────────────────────────────────────────
        (new[]{"cremation","crematorium","cremate","cremation bradford","wibsey crematorium",
               "nab wood crematorium","cremation services","cremation cost"},
         new[]{"https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/deaths/cremations-and-crematoria/",
               "https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/deaths/crematoria-price-information/"},
         "Cremations and crematoria in Bradford",
         "Bradford has crematoria at Nab Wood and Scholemoor. Would you like to know about bereavement service prices?"),

        // ── Bereavement costs ─────────────────────────────────────────────────
        (new[]{"funeral cost","burial cost","cremation cost","bereavement price","bereavement fees",
               "how much funeral bradford","cost of burial bradford","interment fee"},
         new[]{"https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/deaths/bereavement-service-price-information/",
               "https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/deaths/crematoria-price-information/"},
         "Bereavement service prices in Bradford",
         "Would you like to know about council-run cemeteries or crematorium locations?"),

        // ── Family history searches ───────────────────────────────────────────
        (new[]{"family history","genealogy","ancestors","trace family","family records","birth records",
               "death records","marriage records","family tree","register office records"},
         new[]{"https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/deaths/family-history-searches/",
               "https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/certificates/family-history-certificates/"},
         "Family history searches and records",
         "Would you like to know how to get a copy certificate for genealogy purposes?"),

        // ── Marriages and civil partnerships ──────────────────────────────────
        (new[]{"get married","marriage","civil partnership","wedding bradford","register office wedding",
               "marry bradford","marriage ceremony","civil ceremony","wedding ceremony bradford",
               "notice of marriage","give notice"},
         new[]{"https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/marriages-and-civil-partnerships/marriages-and-civil-partnerships/",
               "https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/marriages-and-civil-partnerships/notice-of-marriage-or-civil-partnership/"},
         "Marriages and civil partnerships in Bradford",
         "You must give notice of marriage at least 28 days before the ceremony. Would you like to know about approved premises or ceremony fees?"),

        // ── Notice of marriage ────────────────────────────────────────────────
        (new[]{"notice of marriage","give notice","book notice","marriage notice","notice civil partnership",
               "notice intention to marry","legal notice marriage"},
         new[]{"https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/marriages-and-civil-partnerships/notice-of-marriage-or-civil-partnership/"},
         "Give notice of marriage or civil partnership",
         "Both parties must give notice in person at a register office. Would you like to know about urgent marriages?"),

        // ── Urgent marriages ──────────────────────────────────────────────────
        (new[]{"urgent marriage","emergency marriage","fast marriage","deathbed marriage",
               "urgent wedding","marriage at hospital","marriage terminally ill"},
         new[]{"https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/marriages-and-civil-partnerships/urgent-marriages-and-civil-partnerships/"},
         "Urgent marriages and civil partnerships",
         "Urgent marriage applications are made to the Registrar General. Would you like to contact the Bradford Register Office?"),

        // ── Civil partnership to marriage conversion ──────────────────────────
        (new[]{"convert civil partnership to marriage","civil partnership to marriage","change civil partnership",
               "convert partnership","marriage conversion"},
         new[]{"https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/marriages-and-civil-partnerships/civil-partnership-to-marriage-conversion/"},
         "Converting a civil partnership to a marriage",
         "Would you like to know about renewing your vows or other ceremony options?"),

        // ── Renew vows ────────────────────────────────────────────────────────
        (new[]{"renew vows","vow renewal","renew marriage","anniversary ceremony","reaffirm vows"},
         new[]{"https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/marriages-and-civil-partnerships/renew-your-marriage-or-civil-partnership-vows/"},
         "Renew your marriage or civil partnership vows",
         "Would you like to know about approved ceremony venues in Bradford?"),

        // ── Get a copy certificate ────────────────────────────────────────────
        (new[]{"copy certificate","copy birth certificate","copy death certificate","copy marriage certificate",
               "get certificate","replacement certificate","order certificate","certified copy"},
         new[]{"https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/certificates/get-a-copy-certificate/",
               "https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/fees/certificate-and-ceremony-fees/"},
         "Get a copy certificate — birth, death or marriage",
         "Standard certificates cost £12.50. Would you like to order online or by post?"),

        // ── Citizenship ceremonies ────────────────────────────────────────────
        (new[]{"citizenship ceremony","naturalisation ceremony","citizenship bradford","become british citizen ceremony",
               "citizenship oath","citizenship pledge"},
         new[]{"https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/citizenship/citizenship-ceremonies/"},
         "Citizenship ceremonies in Bradford",
         "Citizenship ceremonies are arranged by Bradford Council on behalf of the Home Office. Would you like to know about other Register Office services?"),

        // ── Coroner ──────────────────────────────────────────────────────────
        (new[]{"coroner","inquest","coroners office","refer death coroner","coroner bradford",
               "west yorkshire coroner","unexpected death","suspicious death","death investigation"},
         new[]{"https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/coroners/the-coroners-office/",
               "https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/coroners/the-coroners-office-and-inquests/"},
         "The Coroner's Office — Bradford",
         "The Coroner investigates sudden, unexpected or unexplained deaths. Would you like to know how to refer a death or contact the coroner?"),

        // ── Refer a death to coroner ──────────────────────────────────────────
        (new[]{"refer death","report death coroner","death coroner referral","who refers death",
               "notify coroner","send death coroner"},
         new[]{"https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/coroners/refer-a-death-to-the-coroner/"},
         "Refer a death to the Coroner",
         "Deaths are usually referred by a doctor, police or hospital. Would you like to contact the Coroner's Office directly?"),

        // ── Approved premises ─────────────────────────────────────────────────
        (new[]{"approved premises","approved venue marriage","marry outside register office",
               "licensed venue wedding","hotel wedding bradford","venue for wedding bradford",
               "register building worship","approved for marriage"},
         new[]{"https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/approved-premises/approved-premises-in-bradford-and-keighley/"},
         "Approved premises for marriages in Bradford",
         "Would you like to know about ceremony fees or giving notice of marriage?"),

        // ── Fees ─────────────────────────────────────────────────────────────
        (new[]{"register office fees","ceremony fees","certificate fee","marriage fee","civil ceremony cost",
               "how much register office","naming ceremony cost","register office cost"},
         new[]{"https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/fees/certificate-and-ceremony-fees/"},
         "Register Office certificate and ceremony fees",
         "Would you like to know about a specific ceremony or certificate cost?"),

        // ── General fallback ──────────────────────────────────────────────────
        (new[]{"register office","births deaths marriages","births deaths","civil partnerships",
               "register office bradford","bradford register office"},
         new[]{"https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/births-deaths-and-marriages/"},
         "Bradford Register Office — births, deaths and marriages",
         "What would you like help with? I can assist with registering births or deaths, marriages, civil partnerships, certificates and more."),
    };

    private async Task<string> GetBirthsDeathsMarriagesInfoAsync(string query, CancellationToken ct)
        => await ScrapeKnowledgeMapAsync(query, BirthsDeathsMarriagesKnowledgeMap,
            "https://www.bradford.gov.uk/births-deaths-marriages-and-civil-partnerships/births-deaths-and-marriages/",
            "Bradford Register Office — births, deaths and marriages",
            "What would you like help with? I can assist with registering births or deaths, marriages, civil partnerships, certificates and more.",
            "BRADFORD BIRTHS DEATHS & MARRIAGES INFORMATION", ct);
}
