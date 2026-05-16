namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] JobsKnowledgeMap =
    {
        (new[]{"job vacancy","council job","work for bradford","bradford council jobs","council vacancies","apply for council job","job bradford","employment council"},
         new[]{"https://www.bradford.gov.uk/jobs/"},
         "Bradford Council job vacancies",
         "Would you like to know about apprenticeships or graduate opportunities?"),

        (new[]{"apprenticeship","apprentice","bradford apprenticeship","earn and learn","level 2","level 3","apprenticeship bradford"},
         new[]{"https://www.bradford.gov.uk/jobs/apprenticeships/apprenticeships/"},
         "Apprenticeships at Bradford Council",
         "Would you like to know about other job opportunities or volunteering?"),

        (new[]{"social care job","social worker job","care worker job","working with vulnerable","adult care job","children social work job"},
         new[]{"https://www.bradford.gov.uk/jobs/working-with-children/social-care-jobs/"},
         "Social care and social work jobs",
         "Would you like to know about other council vacancies or apprenticeship opportunities?"),

        (new[]{"teaching job","teacher bradford","school job","teaching assistant","TA job","education job"},
         new[]{"https://www.bradford.gov.uk/jobs/working-with-children/teaching-jobs/"},
         "Teaching jobs in Bradford",
         "Would you like to know about social care jobs or other council vacancies?"),

        (new[]{"volunteer","volunteering","voluntary work","volunteer bradford","community volunteer"},
         new[]{"https://www.bradford.gov.uk/jobs/volunteering/volunteering/"},
         "Volunteering in Bradford",
         "Would you like to know about apprenticeships or paid job opportunities at Bradford Council?"),

        (new[]{"graduate","graduate scheme","graduate programme","graduate job bradford"},
         new[]{"https://www.bradford.gov.uk/jobs/graduate-programmes/graduate-opportunities/"},
         "Graduate opportunities at Bradford Council",
         "Would you like to know about apprenticeships or other council vacancies?"),

        (new[]{"job","career","work","employment","vacancy","hire","recruitment","bradford career"},
         new[]{"https://www.bradford.gov.uk/jobs/"},
         "Jobs and careers at Bradford Council",
         "What type of role are you interested in? I can help with council vacancies, apprenticeships, social care, teaching, and volunteering."),
    };

    private async Task<string> GetJobsInfoAsync(string query, CancellationToken ct)
        => await ScrapeKnowledgeMapAsync(query, JobsKnowledgeMap,
            "https://www.bradford.gov.uk/jobs/",
            "Jobs and careers at Bradford Council",
            "What type of role are you interested in? I can help with council vacancies, apprenticeships, social care, teaching, and volunteering.",
            "BRADFORD JOBS & CAREERS INFORMATION", ct);
}
