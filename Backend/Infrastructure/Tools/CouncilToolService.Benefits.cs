using System.Text;
using HtmlAgilityPack;

namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    // ── Benefits knowledge base ───────────────────────────────────────────────
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] BenefitsKnowledgeMap =
    {
        // Housing Benefit & Council Tax Reduction — main page
        (new[]{"housing benefit","council tax reduction","council tax support","CTR","CTB","HB","housing and council tax","reduction scheme","apply for housing"},
         new[]{"https://www.bradford.gov.uk/benefits/applying-for-benefits/housing-benefit-and-council-tax-reduction/"},
         "Housing Benefit and Council Tax Reduction",
         "Would you like to know how to apply, or what documents you'll need to provide?"),

        // Free school meals
        (new[]{"free school meals","school meals","fsm","pupil premium","free lunch","free meals","school lunch free","eligible school meals"},
         new[]{"https://www.bradford.gov.uk/benefits/applying-for-benefits/free-school-meals/"},
         "Free school meals",
         "Would you like to know about school transport assistance or other support for families?"),

        // Universal Credit
        (new[]{"universal credit","UC","managed migration","move to UC","migrate","DWP","dwp","tax credits","income support"},
         new[]{"https://www.bradford.gov.uk/benefits/universal-credit/universal-credit/"},
         "Universal Credit and Bradford benefits",
         "Did you know you may also need to apply separately for Council Tax Reduction even if you're on Universal Credit?"),

        // Crisis fund / emergency help
        (new[]{"crisis","resilience fund","household support","emergency help","hardship","urgent help","struggling financially","emergency payment","fuel assistance","crisis fund"},
         new[]{"https://www.bradford.gov.uk/benefits/applying-for-benefits/crisis-and-resilience-fund/",
               "https://www.bradford.gov.uk/benefits/general-benefits-information/help-with-cost-of-living/"},
         "Crisis and Resilience Fund",
         "Would you like a list of emergency food providers in Bradford, or information about the Assisted Purchase Scheme for household items?"),

        // Emergency food providers
        (new[]{"food bank","food banks","food provider","emergency food","hungry","food parcel","foodbank","food voucher","food help"},
         new[]{"https://www.bradford.gov.uk/benefits/applying-for-benefits/emergency-food-providers/",
               "https://www.bradford.gov.uk/benefits/applying-for-benefits/crisis-and-resilience-fund/"},
         "Emergency food providers in Bradford",
         "Would you like to know about the Crisis and Resilience Fund for broader financial emergency support?"),

        // Housing Payment (Discretionary Housing Payment)
        (new[]{"housing payment","discretionary housing","rent shortfall","rent deposit","advance rent","spare room subsidy","bedroom tax","eviction","rent arrears","local housing allowance","LHA","DHP"},
         new[]{"https://www.bradford.gov.uk/benefits/applying-for-benefits/housing-payment/"},
         "Discretionary Housing Payment",
         "Would you like to know more about Housing Benefit or Council Tax Reduction?"),

        // Assisted Purchase Scheme
        (new[]{"assisted purchase","household goods","household items","white goods","second hand","furniture voucher","bed","cooker","fridge","sofa","washing machine","microwave","assisted scheme"},
         new[]{"https://www.bradford.gov.uk/benefits/applying-for-benefits/assisted-purchase-scheme/"},
         "Assisted Purchase Scheme",
         "Would you like to know about the Crisis and Resilience Fund for other types of emergency help?"),

        // Overpayments
        (new[]{"overpayment","overpaid","repay","pay back","recovery","deduction","clawback","too much benefit"},
         new[]{"https://www.bradford.gov.uk/benefits/benefit-payments/what-happens-if-you-are-overpaid/"},
         "Benefit overpayments",
         "Would you like to know how to appeal a benefit decision if you think it's wrong?"),

        // Payment dates / how benefit is paid
        (new[]{"payment date","when paid","when is my benefit","payment schedule","BACS","cheque","benefit paid","how is benefit paid","payment method","benefit date"},
         new[]{"https://www.bradford.gov.uk/benefits/benefit-payments/housing-benefit-payment-dates/",
               "https://www.bradford.gov.uk/benefits/benefit-payments/how-we-pay-housing-benefit-and-council-tax-reduction/"},
         "Housing Benefit payment dates",
         "Would you like to know what to do if a benefit payment goes missing?"),

        // Missing payment
        (new[]{"missing payment","not received","payment not arrived","payment hasn't arrived","lost payment","didn't receive","no payment"},
         new[]{"https://www.bradford.gov.uk/benefits/benefit-payments/what-if-a-benefit-payment-goes-missing/"},
         "What to do if a benefit payment goes missing",
         "Would you like to check the regular payment dates for your benefit?"),

        // Appeals and reviews
        (new[]{"appeal","dispute","disagree","challenge","review","tribunal","decision wrong","wrong decision","mandatory reconsideration"},
         new[]{"https://www.bradford.gov.uk/benefits/general-benefits-information/benefits-appeals-and-reviews/"},
         "Benefits appeals and reviews",
         "Would you like advice on what documents you might need to support your appeal?"),

        // Proof / documents
        (new[]{"proof","documents","evidence","ID","identification","what do I need","supporting documents","paperwork","what to bring","required documents"},
         new[]{"https://www.bradford.gov.uk/benefits/general-benefits-information/proof-you-need-to-provide/"},
         "Documents and proof needed for benefits",
         "Would you like to know how to apply for Housing Benefit or Council Tax Reduction?"),

        // Change of circumstances
        (new[]{"change of circumstances","circumstances changed","moved","new job","lost job","income changed","got married","separated","new baby","new address","household changed","tell us about a change","report a change"},
         new[]{"https://www.bradford.gov.uk/benefits/report-a-change-in-your-circumstances/tell-us-about-a-change-in-your-circumstances/"},
         "Report a change in your circumstances",
         "Would you like to know how this change might affect your benefit amount?"),

        // Backdating
        (new[]{"backdate","back date","late claim","late application","retrospective","backdating","claim late"},
         new[]{"https://www.bradford.gov.uk/benefits/general-benefits-information/can-my-claim-for-benefit-or-council-tax-reduction-be-backdated/"},
         "Backdating your benefit claim",
         "Would you like to know how to apply for Housing Benefit or Council Tax Reduction now?"),

        // Cost of living / financial difficulty
        (new[]{"cost of living","energy bills","fuel","heating","struggling","financial help","financial difficulty","low income","afford","money problems","debt"},
         new[]{"https://www.bradford.gov.uk/benefits/general-benefits-information/help-with-cost-of-living/",
               "https://www.bradford.gov.uk/benefits/applying-for-benefits/crisis-and-resilience-fund/"},
         "Help with cost of living",
         "Would you like information about the Crisis and Resilience Fund or emergency food providers near you?"),

        // Alternative maximum assistance
        (new[]{"alternative maximum","second adult rebate","alternative assistance","second adult"},
         new[]{"https://www.bradford.gov.uk/benefits/applying-for-benefits/alternative-maximum-assistance/"},
         "Alternative maximum assistance (Council Tax Reduction)",
         "Would you like to know more about the full Council Tax Reduction scheme?"),

        // Landlord information
        (new[]{"landlord","tenant","direct payment","landlord payment","housing benefit landlord","rent paid direct","letting agent"},
         new[]{"https://www.bradford.gov.uk/benefits/benefit-information-for-landlords/benefits-information-for-landlords/"},
         "Benefits information for landlords",
         "Would you like to know about discretionary housing payments or how overpayment recovery works for landlords?"),

        // MyInfo / benefit review
        (new[]{"myinfo","my info","benefit review","online account","portal","review letter","review form","annual review"},
         new[]{"https://www.bradford.gov.uk/benefits/myinfo/myinfo/",
               "https://www.bradford.gov.uk/benefits/myinfo/housing-benefit-review/"},
         "MyInfo — online benefit review",
         "Would you like help understanding a benefit notification you've received?"),

        // Notification / decision letter
        (new[]{"notification","decision letter","benefit letter","award letter","notice","what does my letter mean","benefit notice"},
         new[]{"https://www.bradford.gov.uk/benefits/general-benefits-information/benefits-notification-explained/",
               "https://www.bradford.gov.uk/benefits/general-benefits-information/housing-benefit-decision-notice-faqs/"},
         "Understanding your benefit notification",
         "If you disagree with the decision, you have the right to appeal. Would you like to know how?"),

        // Welfare advice / help
        (new[]{"advice","welfare","citizens advice","help","support","advisor","adviser","benefits advice","where to get help","who can help"},
         new[]{"https://www.bradford.gov.uk/benefits/general-benefits-information/benefits-and-welfare-advice-and-help/",
               "https://www.bradford.gov.uk/benefits/general-benefits-information/other-people-who-can-help-you/"},
         "Benefits and welfare advice in Bradford",
         "Would you like to know what benefits you might be entitled to, or how to apply?"),

        // Application forms list
        (new[]{"application form","forms","benefit form","download form","form to fill"},
         new[]{"https://www.bradford.gov.uk/benefits/applying-for-benefits/list-of-benefit-application-forms/"},
         "Benefit application forms",
         "Would you like guidance on what documents to provide with your application?"),

        // Council tax reduction scheme changes
        (new[]{"proposed changes","new scheme","2026 changes","council tax reduction change","scheme change","reduction consultation"},
         new[]{"https://www.bradford.gov.uk/benefits/universal-credit/council-tax-reduction-proposed-change-to-the-scheme/"},
         "Proposed changes to the Council Tax Reduction scheme",
         "Would you like to know about the current Council Tax Reduction and how to apply?"),

        // General fallback
        (new[]{"benefit","welfare","entitlement","claim","what am I entitled to","what benefits"},
         new[]{"https://www.bradford.gov.uk/benefits/applying-for-benefits/what-benefits-are-available/",
               "https://www.bradford.gov.uk/benefits/general-benefits-information/general-benefits-information/"},
         "Benefits available in Bradford",
         "What specific benefit would you like to know more about? I can help with Housing Benefit, Council Tax Reduction, Free School Meals, Universal Credit, and more."),
    };

    private async Task<string> GetBenefitsInfoAsync(string query, CancellationToken ct)
    {
        var q = query.ToLower();

        var urls     = new List<string>();
        var title    = "";
        var followUp = "";

        foreach (var (keywords, pages, pageTitle, pageFollowUp) in BenefitsKnowledgeMap)
        {
            if (keywords.Any(k => q.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var u in pages)
                    if (!urls.Contains(u)) urls.Add(u);
                if (string.IsNullOrEmpty(title))
                {
                    title    = pageTitle;
                    followUp = pageFollowUp;
                }
                if (urls.Count >= 2) break;
            }
        }

        if (urls.Count == 0)
        {
            urls.Add("https://www.bradford.gov.uk/benefits/applying-for-benefits/what-benefits-are-available/");
            title    = "Benefits available in Bradford";
            followUp = "What specific benefit would you like to know more about? I can help with Housing Benefit, Council Tax Reduction, Free School Meals, Universal Credit, and more.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"BRADFORD COUNCIL — BENEFITS INFORMATION — query: \"{query}\"");
        sb.AppendLine();

        foreach (var url in urls.Take(2))
        {
            var html = await FetchHtmlAsync(url, ct);
            if (string.IsNullOrEmpty(html)) continue;

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            foreach (var tag in new[] { "nav", "header", "footer", "script", "style", "aside", "noscript" })
            {
                var nodes = doc.DocumentNode.SelectNodes($"//{tag}");
                if (nodes != null) foreach (var n in nodes.ToList()) n.Remove();
            }
            var main = doc.DocumentNode.SelectSingleNode("//main")
                    ?? doc.DocumentNode.SelectSingleNode("//article")
                    ?? doc.DocumentNode.SelectSingleNode("//body");
            if (main == null) continue;

            var text = CleanText(main.InnerText);
            sb.AppendLine($"--- SOURCE: {url} ---");
            sb.AppendLine(TruncateText(text, 3500));
            sb.AppendLine();
        }

        sb.AppendLine($"OFFICIAL_BRADFORD_LINK: [{title}]({urls[0]})");
        if (!string.IsNullOrEmpty(followUp))
            sb.AppendLine($"FOLLOW_UP_SUGGESTION: {followUp}");

        return sb.ToString();
    }
}
