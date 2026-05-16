namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] EmergenciesKnowledgeMap =
    {
        // ── Flooding (general) ────────────────────────────────────────────────
        (new[]{"flood","flooding","flood risk","flood warning","flood alert","flood bradford",
               "river flooding","surface water flooding","flooded road","flooded property"},
         new[]{"https://www.bradford.gov.uk/emergencies/flooding/flooding/",
               "https://www.bradford.gov.uk/emergencies/flooding/flooding-information-and-support/"},
         "Flooding information and support",
         "For live flood warnings visit Environment Agency (gov.uk/check-flood-risk) or call Floodline 0345 988 1188 (24/7)."),

        // ── Flood pack ────────────────────────────────────────────────────────
        (new[]{"flood pack","flood kit","flood bag","flood emergency kit","prepare for flooding",
               "flood preparation","flood ready","flood essentials"},
         new[]{"https://www.bradford.gov.uk/emergencies/flooding/create-a-flood-pack/"},
         "Create a flood pack",
         "A flood pack should include medication, documents, torch, phone charger and warm clothing. Would you like full guidance on protecting your property?"),

        // ── Flood plans ───────────────────────────────────────────────────────
        (new[]{"flood plan","flood action plan","community flood plan","flood resilience plan",
               "flood response plan","local flood plan"},
         new[]{"https://www.bradford.gov.uk/emergencies/flooding/flood-plans/"},
         "Flood plans in Bradford",
         "Would you like to know how to protect your property from flooding or create a flood pack?"),

        // ── Protecting property from flooding ─────────────────────────────────
        (new[]{"protect property flood","flood proofing","flood barriers","flood door","air brick flood",
               "sandbags flooding","prevent flooding","protect home flood","flood resilience"},
         new[]{"https://www.bradford.gov.uk/emergencies/flooding/protecting-your-property/"},
         "Protecting your property from flooding",
         "Would you like to know about flood grants or what to do if it floods?"),

        // ── What to do if it floods ───────────────────────────────────────────
        (new[]{"what to do flood","flooded house","flood inside","water in house","house flooded",
               "basement flooded","flood response","during a flood","flood emergency"},
         new[]{"https://www.bradford.gov.uk/emergencies/flooding/what-to-do-if-it-floods/"},
         "What to do if it floods",
         "If there is a risk to life, call 999 immediately. Would you like to know about creating a flood pack or protecting your property?"),

        // ── Preparing for emergencies ─────────────────────────────────────────
        (new[]{"prepare emergency","emergency preparation","emergency plan","emergency kit",
               "what to do emergency","ready for emergency","emergency preparedness",
               "plan for disaster","household emergency plan"},
         new[]{"https://www.bradford.gov.uk/emergencies/what-to-do-in-an-emergency/preparing-for-emergencies-what-you-can-do/"},
         "Preparing for emergencies — what you can do",
         "Would you like to know about flooding specifically or Bradford's emergency management arrangements?"),

        // ── Emergency management ──────────────────────────────────────────────
        (new[]{"emergency management","civil contingencies","emergency planning","bradford emergency plan",
               "emergency response bradford","local resilience forum","civil emergency"},
         new[]{"https://www.bradford.gov.uk/emergencies/emergency-management/emergency-management/",
               "https://www.bradford.gov.uk/emergencies/emergency-management/emergency-response-arrangements/"},
         "Emergency management in Bradford",
         "Bradford Council works with West Yorkshire Police, Fire and NHS as part of the Local Resilience Forum. Would you like to know about business continuity?"),

        // ── Business continuity ───────────────────────────────────────────────
        (new[]{"business continuity","business disaster plan","business emergency plan",
               "business resilience","business recovery","business contingency"},
         new[]{"https://www.bradford.gov.uk/emergencies/emergency-management/business-continuity/"},
         "Business continuity planning",
         "Would you like to know about organising an event safely or Bradford's emergency response arrangements?"),

        // ── Planning an event (emergency safety) ──────────────────────────────
        (new[]{"event safety","event management plan","plan a public event","event emergency plan",
               "organising event bradford","event risk assessment","event licence safety",
               "public event planning","festival safety","event safety plan"},
         new[]{"https://www.bradford.gov.uk/emergencies/emergency-management/planning-and-organising-an-event/"},
         "Planning and organising a safe event in Bradford",
         "Large events may require a safety advisory group (SAG) consultation with police, fire and ambulance services."),

        // ── Council service disruptions ────────────────────────────────────────
        (new[]{"service disruption","council disruption","service unavailable","council closed",
               "disruption to services","council emergency closure","service interruption"},
         new[]{"https://www.bradford.gov.uk/emergencies/council-service-disruptions/council-service-disruptions/"},
         "Council service disruptions",
         "Would you like to know about bank holiday opening times or winter service arrangements?"),

        // ── Bank holidays ─────────────────────────────────────────────────────
        (new[]{"bank holiday","bank holiday opening","christmas opening","easter opening",
               "council bank holiday","open bank holiday","bradford bank holiday",
               "bin collection bank holiday","office bank holiday"},
         new[]{"https://www.bradford.gov.uk/emergencies/council-service-disruptions/bank-holiday-closure-times/"},
         "Bank holiday closure times — Bradford Council",
         "Would you like to know about bin collections on bank holidays or other service disruptions?"),

        // ── Winter and gritting (emergency) ────────────────────────────────────
        (new[]{"winter emergency","gritting emergency","icy road","snow emergency","ice road",
               "road gritting","snow bradford","winter roads","gritting routes"},
         new[]{"https://www.bradford.gov.uk/emergencies/winter-and-gritting/winter-and-gritting/",
               "https://www.bradford.gov.uk/transport-and-travel/winter-maintenance/view-gritting-routes/"},
         "Winter and gritting in Bradford",
         "Bradford grits priority routes when temperatures drop to 1°C or below. Would you like to view the gritting route map?"),

        // ── General emergencies fallback ───────────────────────────────────────
        (new[]{"emergency","urgent","crisis","disaster","incident","hazard bradford"},
         new[]{"https://www.bradford.gov.uk/emergencies/"},
         "Emergencies in Bradford",
         "For immediate danger to life always call 999. For Bradford Council emergency line call 01274 431000 (24/7)."),
    };

    private async Task<string> GetEmergenciesInfoAsync(string query, CancellationToken ct)
        => await ScrapeKnowledgeMapAsync(query, EmergenciesKnowledgeMap,
            "https://www.bradford.gov.uk/emergencies/",
            "Emergencies in Bradford",
            "For immediate danger to life always call 999. For Bradford Council emergency line call 01274 431000 (24/7).",
            "BRADFORD EMERGENCIES INFORMATION", ct);
}
