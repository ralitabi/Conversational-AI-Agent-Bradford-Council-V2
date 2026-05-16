namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] ComplaintsKnowledgeMap =
    {
        (new[]{"make a complaint","complaint","complain","unhappy with service","council complaint","lodge a complaint","formal complaint","complaint about council"},
         new[]{"https://www.bradford.gov.uk/compliments-and-complaints/",
               "https://www.bradford.gov.uk/compliments-and-complaints/policies-and-procedures/the-councils-complaints-procedure/"},
         "Making a complaint to Bradford Council",
         "Would you like to know about escalating to the Local Government Ombudsman if you're not satisfied?"),

        (new[]{"complaint procedure","complaint process","how complaints work","complaint policy","complaint handling","what happens with complaint"},
         new[]{"https://www.bradford.gov.uk/compliments-and-complaints/policies-and-procedures/the-councils-complaints-procedure/"},
         "Bradford Council complaints procedure",
         "Would you like to know about escalation to the Local Government Ombudsman?"),

        (new[]{"local government ombudsman","LGO","ombudsman","LGSCO","housing ombudsman","escalate complaint","independent complaint"},
         new[]{"https://www.bradford.gov.uk/compliments-and-complaints/policies-and-procedures/the-councils-complaints-procedure/"},
         "Escalating your complaint — Ombudsman",
         "The Local Government and Social Care Ombudsman can be reached at lgo.org.uk or 0300 061 0614."),

        (new[]{"compliment","praise","thank","positive feedback","good service","well done","commend"},
         new[]{"https://www.bradford.gov.uk/compliments-and-complaints/"},
         "Compliments — share positive feedback",
         "Would you like to know how to submit a formal compliment to the Bradford Council team?"),

        (new[]{"complaint adult social care","social care complaint","care complaint","care home complaint"},
         new[]{"https://www.bradford.gov.uk/compliments-and-complaints/make-a-complaint/make-a-compliment-or-complaint-about-adult-social-care/"},
         "Complaint about adult social care",
         "Would you like to know about the Housing Ombudsman for social housing complaints?"),

        (new[]{"complaint","complain","unhappy","dissatisfied","feedback","bad service"},
         new[]{"https://www.bradford.gov.uk/compliments-and-complaints/"},
         "Complaints and compliments",
         "What service are you making a complaint about? I can guide you to the right process."),
    };

    private async Task<string> GetComplaintsInfoAsync(string query, CancellationToken ct)
        => await ScrapeKnowledgeMapAsync(query, ComplaintsKnowledgeMap,
            "https://www.bradford.gov.uk/compliments-and-complaints/",
            "Complaints and compliments",
            "What service are you making a complaint about? I can guide you to the right process.",
            "BRADFORD COMPLAINTS & COMPLIMENTS INFORMATION", ct);
}
