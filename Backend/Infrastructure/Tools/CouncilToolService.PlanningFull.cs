namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] PlanningInfoKnowledgeMap =
    {
        // ── Do I need planning permission? ───────────────────────────────────────
        (new[]{"do i need planning permission","need planning permission","planning permission needed","permitted development",
               "extension planning permission","loft conversion","conservatory planning","outbuilding permission",
               "garage conversion planning","rear extension","side extension","planning permission extension",
               "planning permission house","householder planning","porch planning"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/planning-application-and-building-regulations-advice/do-i-need-planning-permission-advice-for-householders/"},
         "Do I need planning permission? — householder guide",
         "Remember: even if planning permission isn't needed, Building Regulations approval usually is. Would you like to know how to apply for building regulations?"),

        // ── View planning applications ────────────────────────────────────────────
        (new[]{"view planning application","search planning application","find planning application",
               "planning application reference","planning portal search","view planning","look up planning application",
               "planning application status","check planning status"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/planning-applications/view-planning-applications/"},
         "View planning applications",
         "Would you like to comment on an application, or check planning decisions for a specific address?"),

        // ── Comment / object to planning ─────────────────────────────────────────
        (new[]{"comment on planning","object to planning","planning objection","oppose planning","planning comment",
               "how to object planning","object planning application","object to neighbour extension",
               "planning representation","submit comments planning"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/planning-applications/comment-on-or-object-to-a-planning-application/"},
         "Comment on or object to a planning application",
         "Comments must be based on material planning considerations — views, property values, and business competition are not valid grounds."),

        // ── Make a planning application ───────────────────────────────────────────
        (new[]{"make planning application","submit planning application","apply for planning permission",
               "planning application form","how to apply planning","submit planning","planning portal submit",
               "planning application process","applying for planning"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/planning-applications/make-a-planning-application/"},
         "Make a planning application",
         "Applications are submitted via the Planning Portal. Would you like to know about planning fees or pre-application advice?"),

        // ── How planning decisions are made ──────────────────────────────────────
        (new[]{"how planning decisions","planning decision process","who decides planning","planning committee",
               "planning officer decision","delegated planning","planning determination","how long planning decision"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/planning-applications/how-planning-decisions-are-made/"},
         "How planning decisions are made",
         "Planning permission is valid for 3 years. Would you like to know about planning appeals if your application is refused?"),

        // ── Planning fees ─────────────────────────────────────────────────────────
        (new[]{"planning fees","planning application fee","how much planning","planning fee calculator",
               "planning fee schedule","planning application cost","planning fee 2026","planning charges"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/planning-applications/scale-of-planning-fees/"},
         "Scale of planning fees (from April 2026)",
         "Householder applications cost £272–£548 from April 2026. Would you like to know about pre-application advice before you apply?"),

        // ── Planning appeals ──────────────────────────────────────────────────────
        (new[]{"planning appeal","appeal planning","refused planning","planning refusal","appeal planning decision",
               "planning rejected","appeal to planning inspectorate","planning appeal process"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/planning-applications/view-and-comment-on-planning-appeals/"},
         "Planning appeals",
         "Only the applicant can appeal a planning decision — objectors cannot formally appeal but can submit views to the Planning Inspectorate."),

        // ── Street naming and numbering ───────────────────────────────────────────
        (new[]{"street naming","street numbering","new address","house number","address new development",
               "rename street","street name change","new property address","house name change"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/planning-applications/street-naming-and-numbering/"},
         "Street naming and numbering",
         "All utilities and emergency services require a Local Authority approved address. Would you like to request a new address or change?"),

        // ── Permission in principle ───────────────────────────────────────────────
        (new[]{"permission in principle","PiP","self build register","permission in principle application",
               "outline planning alternative"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/planning-applications/permission-in-principle/"},
         "Permission in Principle (PiP)",
         "Would you like to know about full planning applications or pre-application advice?"),

        // ── Pre-application advice ────────────────────────────────────────────────
        (new[]{"pre-application advice","pre-app","planning pre application","planning advice before applying",
               "pre application planning","planning advice service","pre application enquiry",
               "planning consultation","planning advice meeting"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/planning-application-and-building-regulations-advice/pre-application-advice-for-residential-and-commercial-developments/",
               "https://www.bradford.gov.uk/planning-and-building-control/planning-application-and-building-regulations-advice/pre-application-advice-for-major-developments/"},
         "Pre-application planning advice",
         "Pre-application fees (£300–£1,000) are non-refundable but can save money by identifying issues before a formal application."),

        // ── Developer contributions / CIL ─────────────────────────────────────────
        (new[]{"developer contributions","community infrastructure levy","CIL","section 106","S106",
               "planning obligations","infrastructure levy","habitat mitigation","biodiversity net gain payment",
               "developer levy","planning gain"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/developer-contributions/developer-contributions/"},
         "Developer contributions — CIL and Section 106",
         "Would you like to know about the CIL charging schedule or biodiversity net gain requirements?"),

        // ── Planning policy / Local Plan ──────────────────────────────────────────
        (new[]{"planning policy","local plan","core strategy","neighbourhood plan","development plan",
               "local development framework","Bradford local plan","planning policies","UDP"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/planning-policy/core-strategy-dpd/"},
         "Bradford planning policy and Local Plan",
         "Would you like to know about neighbourhood planning or developer contributions?"),

        // ── Lawful development certificate ────────────────────────────────────────
        (new[]{"lawful development certificate","LDC","certificate of lawfulness","lawful existing use",
               "lawful development","existing use certificate","planning immunity","4 year rule","10 year rule",
               "lawful development certificate existing"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/planning-applications/lawful-development-certificate-existing/"},
         "Lawful Development Certificate for existing use or development",
         "A Lawful Development Certificate gives legal protection against enforcement action. Would you like to know about planning enforcement?"),

        // ── Building regulations — general/apply ─────────────────────────────────
        (new[]{"building regulations","building regs","building control application","full plans","building notice",
               "apply for building regulations","building regulations application","building control approval",
               "building regs application","building regulations required"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/building-control/make-a-building-regulations-application/",
               "https://www.bradford.gov.uk/planning-and-building-control/building-control/full-plans-application/",
               "https://www.bradford.gov.uk/planning-and-building-control/building-control/building-notice-application/"},
         "Make a building regulations application",
         "Applications are submitted via the Planning Portal. Call Building Control on 01274 433807 for advice. Would you like to know about building regulation charges?"),

        // ── Building regulation fees / charges ────────────────────────────────────
        (new[]{"building regulation charges","building regs fee","building control fee","how much building regulations",
               "building regulations cost","pay building regulations","building control charges","building regs cost"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/building-control/building-regulation-charges/",
               "https://www.bradford.gov.uk/planning-and-building-control/building-control/pay-a-building-regulations-fee-or-invoice/"},
         "Building regulation charges and fees (2026)",
         "Would you like to know how to submit a building regulations application?"),

        // ── Building control inspections ──────────────────────────────────────────
        (new[]{"building control inspection","site inspection","building inspection","building control visit",
               "inspector visit","building inspector","building work inspection","stage inspection"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/building-control/site-inspections/"},
         "Building control site inspections",
         "Would you like to know about making a building regulations application or viewing existing applications?"),

        // ── Demolition ────────────────────────────────────────────────────────────
        (new[]{"demolition notice","demolish building","demolish property","demolition","knocking down building",
               "demolish","submit demolition notice"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/building-control/submit-a-demolition-notice/"},
         "Submit a demolition notice",
         "Would you like to know about building regulations for new development on the site?"),

        // ── Dangerous structure ───────────────────────────────────────────────────
        (new[]{"dangerous structure","dangerous building","unsafe building","unsafe structure","crumbling wall",
               "structural danger","report dangerous building","dangerous wall","falling building"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/building-control/report-a-dangerous-structure/"},
         "Report a dangerous structure",
         "For immediate structural emergencies, call Bradford Council 24-hour on 01274 431000."),

        // ── Fire risk assessment ──────────────────────────────────────────────────
        (new[]{"fire risk assessment","building control fire","fire safety building","fire assessment building",
               "fire regulations building","fire safety building regs"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/building-control/fire-risk-assessment/"},
         "Fire risk assessment — building control",
         "Would you like to know about fire safety licensing requirements for businesses?"),

        // ── Regularisation ────────────────────────────────────────────────────────
        (new[]{"regularisation","retrospective building regs","building work without approval",
               "building work done without building regulations","regularisation certificate",
               "building regs after work","no building regulations","missed building regs"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/building-control/regularisation-application/"},
         "Regularisation — building work done without approval",
         "Call Building Control on 01274 433807 to discuss your regularisation application."),

        // ── Duty holders / building safety ────────────────────────────────────────
        (new[]{"duty holder","principal designer","principal contractor","building safety","building regulations 2022",
               "competence building","Part 2A","duty to comply building regs","building regulations competence"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/building-control/duty-holders-and-compliance/"},
         "Duty holders and compliance (Building Regulations 2022)",
         "Would you like to know about making a building regulations application or building safety levy?"),

        // ── Building safety levy ──────────────────────────────────────────────────
        (new[]{"building safety levy","BSL","cladding levy","building levy","residential development levy",
               "building safety levy 2026","safety levy"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/building-control/building-safety-levy/"},
         "Building Safety Levy (from October 2026)",
         "The levy applies to residential development. Payment is required before occupation. Would you like to know about building regulation charges?"),

        // ── Building control partnership ─────────────────────────────────────────
        (new[]{"building control partnership","approved inspector","private building control","building control scheme",
               "initial notice","reversion","building control partnership scheme"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/building-control/building-control-partnership-scheme/"},
         "Building control partnership scheme",
         "Would you like to know about making a building regulations application?"),

        // ── Contact building control ──────────────────────────────────────────────
        (new[]{"contact building control","building control phone","building control email","building control contact",
               "building control team","speak to building control","building control bradford"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/building-control/contact-building-control/"},
         "Contact Building Control",
         "Building Control: 01274 433807 | buildingcontrol@bradford.gov.uk | 4th Floor Britannia House, Bradford BD1 1HX"),

        // ── Planning enforcement ──────────────────────────────────────────────────
        (new[]{"planning enforcement","breach of planning","planning violation","illegal development",
               "unauthorised development","planning breach","report planning breach","enforcement notice",
               "planning enforcement action","enforcement complaint"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/planning-enforcement/report-a-breach-of-planning-control/",
               "https://www.bradford.gov.uk/planning-and-building-control/planning-enforcement/a-guide-to-enforcement/"},
         "Report a planning enforcement breach",
         "Report to planning.enforcement@bradford.gov.uk — include full address, nature of works, start date and photos."),

        // ── View enforcement notices ──────────────────────────────────────────────
        (new[]{"view enforcement notice","enforcement notice","planning enforcement notice","enforcement order",
               "view planning enforcement","enforcement register"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/planning-enforcement/view-planning-enforcement-notices/"},
         "View planning enforcement notices",
         "Would you like to report a breach or know about lawful development certificates?"),

        // ── Neighbour altered property ────────────────────────────────────────────
        (new[]{"neighbour extension planning","neighbour building","neighbour altered house","neighbour added extension",
               "does neighbour need planning","neighbour converted garage","neighbour built outbuilding",
               "next door building work","neighbour work planning permission"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/planning-enforcement/my-neighbour-has-altered-or-extended-their-property-do-they-require-planning-permission/"},
         "Does my neighbour need planning permission?",
         "Would you like to comment on a planning application, or report a breach of planning control?"),

        // ── Complaints against you (enforcement) ─────────────────────────────────
        (new[]{"planning complaint against me","enforcement against me","council investigating me",
               "planning notice against me","planning complaint received","enforcement notice received"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/planning-enforcement/complaints-against-you/"},
         "Planning enforcement — complaints made against you",
         "Contact Planning Enforcement on 01274 434605 as soon as possible to discuss the situation."),

        // ── General planning & building control fallback ──────────────────────────
        (new[]{"planning","building control","planning permission","planning application","building regulations",
               "planning and building","bradford planning"},
         new[]{"https://www.bradford.gov.uk/planning-and-building-control/"},
         "Planning and Building Control in Bradford",
         "What planning or building control help do you need? I can assist with applications, fees, building regs, enforcement, and more."),
    };

    private async Task<string> GetPlanningInfoAsync(string query, CancellationToken ct)
        => await ScrapeKnowledgeMapAsync(query, PlanningInfoKnowledgeMap,
            "https://www.bradford.gov.uk/planning-and-building-control/",
            "Planning and Building Control in Bradford",
            "What planning or building control help do you need? I can assist with applications, fees, building regs, enforcement, and more.",
            "BRADFORD PLANNING & BUILDING CONTROL INFORMATION", ct);
}
