using System.Text;
using HtmlAgilityPack;

namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] AdultSocialCareKnowledgeMap =
    {
        (new[]{"home care","stay at home","stay in my home","live at home","independent living","living independently","supported living","extra care housing","community meals","meals on wheels","equipment","adaptations","telecare","safe and sound"},
         new[]{"https://www.bradford.gov.uk/adult-social-care/living-independently/living-independently/",
               "https://www.bradford.gov.uk/adult-social-care/living-independently/home-care/"},
         "Living independently — adult social care",
         "Would you like to know how to request a care needs assessment?"),

        (new[]{"assessment","care assessment","needs assessment","care needs","eligibility","social care assessment","i need help","i need care","care plan"},
         new[]{"https://www.bradford.gov.uk/adult-social-care/i-want-an-assessment/i-want-an-assessment/"},
         "Adult social care assessment",
         "Would you like to know how Bradford Council charges for care and support?"),

        (new[]{"pay for care","paying for care","cost of care","care charges","residential care cost","nursing home cost","direct payment","deferred payment","financial assessment","means tested","savings threshold"},
         new[]{"https://www.bradford.gov.uk/adult-social-care/paying-for-support/paying-for-support/",
               "https://www.bradford.gov.uk/adult-social-care/paying-for-support/direct-payments/"},
         "Paying for adult social care",
         "Would you like to know about direct payments so you can manage your own care budget?"),

        (new[]{"residential care","nursing home","care home","residential home"},
         new[]{"https://www.bradford.gov.uk/adult-social-care/paying-for-support/paying-for-residential-care/"},
         "Paying for residential and nursing care",
         "Would you like to know about deferred payment agreements so you don't have to sell your home immediately?"),

        (new[]{"carer","caring for someone","caring for a relative","unpaid carer","family carer","carer support","carer break","respite","carer assessment","carer leave"},
         new[]{"https://www.bradford.gov.uk/adult-social-care/carers/caring-for-family-and-friends/",
               "https://www.bradford.gov.uk/adult-social-care/carers/carer-breaks/"},
         "Support for carers",
         "Did you know carers are entitled to their own needs assessment? Would you like to know more?"),

        (new[]{"safeguarding","abuse","adult abuse","neglect","financial abuse","domestic abuse adult","report abuse","exploitation","elder abuse","vulnerable adult"},
         new[]{"https://www.bradford.gov.uk/adult-social-care/adult-abuse/report-abuse/"},
         "Report adult abuse or safeguarding concern",
         "If someone is in immediate danger, call 999. For non-emergencies, call Bradford's social care team on 01274 435400."),

        (new[]{"disability","disabled adult","physical disability","learning disability","hearing impairment","visual impairment","deaf","blind","occupational therapy","OT","equipment loan","wheelchair","British Sign Language","BSL"},
         new[]{"https://www.bradford.gov.uk/adult-social-care/disabilities/i-have-a-disability/",
               "https://www.bradford.gov.uk/adult-social-care/disabilities/occupational-therapy/"},
         "Support for adults with disabilities",
         "Would you like to know about the Disabled Facilities Grant for home adaptations?"),

        (new[]{"mental health social work","mental health support","mental health care","CMHT","community mental health","mental health team","social worker mental health"},
         new[]{"https://www.bradford.gov.uk/adult-social-care/living-independently/mental-health-social-work-team/"},
         "Mental health social work",
         "For urgent mental health crisis support, call First Response on 0800 952 1181 (24/7)."),

        (new[]{"preparation for adulthood","transition","16 to 25","young adult care","EHCP adult","leaving care","transition to adult services"},
         new[]{"https://www.bradford.gov.uk/adult-social-care/living-independently/preparation-for-adulthood/"},
         "Preparation for adulthood (16-25 transition)",
         "Would you like to know about SEND local offer and support for young people with disabilities?"),

        (new[]{"adult social care","social care","social services","social worker","council care","care and support"},
         new[]{"https://www.bradford.gov.uk/adult-social-care/"},
         "Adult social care in Bradford",
         "What type of support are you looking for? I can help with assessments, home care, disability support, carers, or paying for care."),
    };

    private async Task<string> GetAdultSocialCareInfoAsync(string query, CancellationToken ct)
        => await ScrapeKnowledgeMapAsync(query, AdultSocialCareKnowledgeMap,
            "https://www.bradford.gov.uk/adult-social-care/",
            "Adult social care in Bradford",
            "What type of support are you looking for? I can help with assessments, home care, disability support, carers, or paying for care.",
            "BRADFORD ADULT SOCIAL CARE INFORMATION", ct);
}
