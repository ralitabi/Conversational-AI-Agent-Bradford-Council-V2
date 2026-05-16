namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] PayingForServicesKnowledgeMap =
    {
        // ── How to pay (general) ──────────────────────────────────────────────
        (new[]{"how to pay council","pay bradford council","pay for council service","payment bradford",
               "pay online bradford","what can i pay","i want to pay","make payment council"},
         new[]{"https://www.bradford.gov.uk/paying-for-services/i-want-to-pay-for/i-want-to-pay-for/"},
         "Pay for Bradford Council services",
         "You can pay online, by phone or by direct debit. Would you like to pay a council invoice, council tax, or another service?"),

        // ── Council invoices ──────────────────────────────────────────────────
        (new[]{"council invoice","invoice bradford","pay invoice","council bill","invoice from council",
               "received invoice","received bill from council","pay council bill"},
         new[]{"https://www.bradford.gov.uk/paying-for-services/council-invoices/council-invoices/",
               "https://www.bradford.gov.uk/paying-for-services/council-invoices/pay-your-bradford-council-invoice/"},
         "Pay a Bradford Council invoice",
         "Would you like to dispute an invoice, request a copy, or get help with managing the payment?"),

        // ── Pay council invoice ───────────────────────────────────────────────
        (new[]{"pay my invoice","pay council invoice","pay invoice online","how to pay invoice bradford",
               "online invoice payment"},
         new[]{"https://www.bradford.gov.uk/paying-for-services/council-invoices/pay-your-bradford-council-invoice/"},
         "Pay your Bradford Council invoice online",
         "Would you like to know how to dispute or query an invoice?"),

        // ── Dispute invoice ───────────────────────────────────────────────────
        (new[]{"dispute invoice","query invoice","wrong invoice","invoice incorrect","challenge invoice",
               "invoice dispute","incorrect bill","contest invoice"},
         new[]{"https://www.bradford.gov.uk/paying-for-services/council-invoices/dispute-a-bradford-council-invoice/"},
         "Dispute a Bradford Council invoice",
         "Would you like to request a copy of your invoice or contact the invoice team?"),

        // ── Request copy invoice ──────────────────────────────────────────────
        (new[]{"copy invoice","request invoice copy","need copy invoice","lost invoice","duplicate invoice",
               "copy of bill","reissue invoice"},
         new[]{"https://www.bradford.gov.uk/paying-for-services/council-invoices/request-a-copy-of-your-bradford-council-invoice/"},
         "Request a copy of your Bradford Council invoice",
         "Would you like to know how to pay or dispute your invoice?"),

        // ── Contact invoice team ──────────────────────────────────────────────
        (new[]{"contact invoice team","invoice team bradford","invoice query","invoice help",
               "invoice department","speak to invoice","invoice contact"},
         new[]{"https://www.bradford.gov.uk/paying-for-services/council-invoices/contact-from-the-council-invoice-team/"},
         "Contact the Bradford Council invoice team",
         "Would you like to dispute an invoice or pay online?"),

        // ── Direct debit ──────────────────────────────────────────────────────
        (new[]{"direct debit","set up direct debit","direct debit council","pay by direct debit",
               "monthly direct debit","direct debit mandate","standing order"},
         new[]{"https://www.bradford.gov.uk/paying-for-services/direct-debit-and-paperless-bills/direct-debit/"},
         "Pay by direct debit — Bradford Council",
         "Direct debit is available for council tax, business rates and other services. Would you like to know about paperless billing?"),

        // ── Paperless bills ───────────────────────────────────────────────────
        (new[]{"paperless bills","paperless billing","go paperless","paperless statements",
               "online bills","e-billing","electronic billing"},
         new[]{"https://www.bradford.gov.uk/paying-for-services/direct-debit-and-paperless-bills/paperless-bills/"},
         "Paperless bills — Bradford Council",
         "Would you like to set up a direct debit or manage your council payments online?"),

        // ── Money advice / debt help ──────────────────────────────────────────
        (new[]{"money advice","debt help","debt advice","struggling to pay","can't afford","financial difficulty",
               "help with debt","debt management","money problems","debt support bradford",
               "financial help","can't pay bill","help paying"},
         new[]{"https://www.bradford.gov.uk/paying-for-services/money-advice/help-with-managing-your-money-and-debt/",
               "https://www.bradford.gov.uk/paying-for-services/council-invoices/help-with-managing-money-and-debt/"},
         "Help with managing money and debt",
         "Free debt advice is available from StepChange (0800 138 1111), Citizens Advice Bradford, or Bradford Council's own money advice service."),

        // ── General fallback ──────────────────────────────────────────────────
        (new[]{"paying","payment","pay","bill","invoice","charges","fees"},
         new[]{"https://www.bradford.gov.uk/paying-for-services/i-want-to-pay-for/i-want-to-pay-for/"},
         "Paying for Bradford Council services",
         "What would you like to pay? I can help with council invoices, council tax, direct debits, and money advice."),
    };

    private async Task<string> GetPayingForServicesInfoAsync(string query, CancellationToken ct)
        => await ScrapeKnowledgeMapAsync(query, PayingForServicesKnowledgeMap,
            "https://www.bradford.gov.uk/paying-for-services/i-want-to-pay-for/i-want-to-pay-for/",
            "Paying for Bradford Council services",
            "What would you like to pay? I can help with council invoices, council tax, direct debits, and money advice.",
            "BRADFORD PAYING FOR SERVICES INFORMATION", ct);
}
