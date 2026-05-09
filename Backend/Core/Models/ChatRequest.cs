namespace Bradford.Core.Models;

public class ChatRequest
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string Message { get; set; } = string.Empty;
    public bool StreamResponse { get; set; } = false;
}

public class ChatResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string Reply { get; set; } = string.Empty;
    public List<string> Sources { get; set; } = new();
    public bool UsedRag { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Structured data for rich UI rendering
    public List<AddressOption>?            Addresses          { get; set; }
    public BinDateCard?                    BinDates           { get; set; }
    public List<LibraryOption>?            Libraries          { get; set; }
    public CouncilTaxCard?                 CouncilTaxInfo     { get; set; }
    public List<CouncilTaxPropertyOption>? CouncilTaxProperties { get; set; }
    public List<SchoolOption>?             Schools            { get; set; }
    public SchoolCard?                     SchoolDetails      { get; set; }
}

public class CouncilTaxPropertyOption
{
    public int    Number        { get; set; }
    public string Address       { get; set; } = string.Empty;
    public string Band          { get; set; } = string.Empty;
    public string AnnualAmount  { get; set; } = string.Empty;
    public string MonthlyAmount { get; set; } = string.Empty;
    public string Postcode      { get; set; } = string.Empty;
}

public class LibraryOption
{
    public int    Number   { get; set; }
    public string Name     { get; set; } = string.Empty;
    public string Address  { get; set; } = string.Empty;
    public string Distance { get; set; } = string.Empty;   // e.g. "0.7 mi"
    public string Phone    { get; set; } = string.Empty;
    public string Slug     { get; set; } = string.Empty;   // for page URL
}

public class AddressOption
{
    public int    Number   { get; set; }
    public string Line1    { get; set; } = string.Empty;
    public string City     { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public string Uprn     { get; set; } = string.Empty;
    public string FullAddress => $"{Line1}, {City}, {Postcode}".Trim(',', ' ');
}

public class BinDateCard
{
    public string Address    { get; set; } = string.Empty;
    // Next collection date (single string) — kept for display in the row
    public string GreyBin    { get; set; } = string.Empty;
    public string GreenBin   { get; set; } = string.Empty;
    public string BrownBin   { get; set; } = string.Empty;
    // Full upcoming schedule lists (populated when HasDates=true)
    public List<string> GreyBinDates  { get; set; } = new();
    public List<string> GreenBinDates { get; set; } = new();
    public List<string> BrownBinDates { get; set; } = new();
    public string CheckerUrl { get; set; } = "https://www.bradford.gov.uk/recycling-and-waste/bin-collections/check-your-bin-collection-dates/";
    // When true, grey/green values are specific next-collection dates; when false they are schedule descriptions
    public bool HasDates     { get; set; } = false;
}

public class CouncilTaxCard
{
    public string Address       { get; set; } = string.Empty;
    public string Band          { get; set; } = string.Empty;
    public string AnnualAmount  { get; set; } = string.Empty;
    public string MonthlyAmount { get; set; } = string.Empty;
    public string TaxYear       { get; set; } = "2025/26";
    public List<CouncilTaxBandRate> AllBands { get; set; } = new();
    public string PayUrl        { get; set; } = "https://www.bradford.gov.uk/council-tax/pay-your-council-tax/";
    public string BandLookupUrl { get; set; } = "https://www.tax.service.gov.uk/check-if-you-need-to-contact-voa";
}

public class CouncilTaxBandRate
{
    public string Band          { get; set; } = string.Empty;
    public string AnnualAmount  { get; set; } = string.Empty;
    public string MonthlyAmount { get; set; } = string.Empty;
}

public class SchoolOption
{
    public int    Number       { get; set; }
    public string Name         { get; set; } = string.Empty;
    public string Address      { get; set; } = string.Empty;
    public string Phase        { get; set; } = string.Empty;
    public string Type         { get; set; } = string.Empty;
    public string OfstedRating { get; set; } = string.Empty;
    public string Distance     { get; set; } = string.Empty;
    public string Urn          { get; set; } = string.Empty;
    public string Website      { get; set; } = string.Empty;
    public string Phone        { get; set; } = string.Empty;
}

public class SchoolCard
{
    public string Name          { get; set; } = string.Empty;
    public string Address       { get; set; } = string.Empty;
    public string Phone         { get; set; } = string.Empty;
    public string Website       { get; set; } = string.Empty;
    public string Phase         { get; set; } = string.Empty;
    public string Type          { get; set; } = string.Empty;
    public string OfstedRating  { get; set; } = string.Empty;
    public string OfstedDate    { get; set; } = string.Empty;
    public string Pupils        { get; set; } = string.Empty;
    public string AgeRange      { get; set; } = string.Empty;
    public string Urn           { get; set; } = string.Empty;
    public string AdmissionsUrl { get; set; } = "https://www.bradford.gov.uk/education-and-skills/schools/school-admissions/";
    public string OfstedUrl     { get; set; } = "https://reports.ofsted.gov.uk/";
}
