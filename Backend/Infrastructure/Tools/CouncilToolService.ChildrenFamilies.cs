using System.Text;
using HtmlAgilityPack;

namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] ChildrenFamiliesKnowledgeMap =
    {
        (new[]{"report a child","concern about a child","child at risk","child abuse","child neglect","safeguarding child","worried about a child","child protection","report abuse child"},
         new[]{"https://www.bradford.gov.uk/children-young-people-and-families/talk-to-us-about-a-child/talk-to-us-about-a-child/"},
         "Report a concern about a child",
         "If a child is in immediate danger, call 999. For non-emergencies, Bradford's MAST team is on 01274 435400."),

        (new[]{"family support","support for families","early help","family hub","family hubs","parenting support","young people support","family advice"},
         new[]{"https://www.bradford.gov.uk/children-young-people-and-families/family-hubs/family-hubs/",
               "https://www.bradford.gov.uk/children-young-people-and-families/get-advice-and-support/support-for-families-and-young-people/"},
         "Family hubs and support for families",
         "Would you like to find your nearest family hub or know about other early help services?"),

        (new[]{"SEND","special educational needs","disabilities child","local offer","EHCP","education health care plan","SEN support","special needs child"},
         new[]{"https://www.bradford.gov.uk/children-young-people-and-families/does-your-child-have-special-educational-needs-or-disabilities/search-for-local-services-bradfords-local-offer/"},
         "SEND local offer — support for children with special needs",
         "Would you like to know about school admissions for children with SEND or the EHCP process?"),

        (new[]{"fostering","foster","become a foster carer","foster care","looked after children","children in care"},
         new[]{"https://www.bradford.gov.uk/children-young-people-and-families/bradford-children-and-families-trust/bradford-children-and-families-trust/"},
         "Bradford Children and Families Trust",
         "Would you like to know how to enquire about fostering in Bradford?"),

        (new[]{"childminder","registered childminder","childcare","childcare provider","become a childminder","ofsted registered childminder"},
         new[]{"https://www.bradford.gov.uk/children-young-people-and-families/working-with-children/how-to-become-a-registered-childminder/"},
         "How to become a registered childminder",
         "Would you like to know about childcare funding or free hours entitlement?"),

        (new[]{"children families","children services","children's social care","Bradford Children","children young people"},
         new[]{"https://www.bradford.gov.uk/children-young-people-and-families/",
               "https://www.bradford.gov.uk/children-young-people-and-families/bradford-children-and-families-trust/bradford-children-and-families-trust/"},
         "Children, young people and families services",
         "What support do you need? I can help with child safeguarding, family support, SEND, fostering, or childcare."),
    };

    private async Task<string> GetChildrenFamiliesInfoAsync(string query, CancellationToken ct)
        => await ScrapeKnowledgeMapAsync(query, ChildrenFamiliesKnowledgeMap,
            "https://www.bradford.gov.uk/children-young-people-and-families/",
            "Children, young people and families services",
            "What support do you need? I can help with child safeguarding, family support, SEND, fostering, or childcare.",
            "BRADFORD CHILDREN & FAMILIES INFORMATION", ct);
}
