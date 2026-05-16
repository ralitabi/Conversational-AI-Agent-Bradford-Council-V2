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
    public BradfordHomesResult?            Properties         { get; set; }
    public List<SportsCentreOption>?       SportsCentres      { get; set; }
    public SportsCentreCard?               SportsCentreDetails { get; set; }
}

public class SportsCentreOption
{
    public int          Number     { get; set; }
    public string       Name       { get; set; } = string.Empty;
    public string       Address    { get; set; } = string.Empty;
    public string       Phone      { get; set; } = string.Empty;
    public string       Distance   { get; set; } = string.Empty;
    public List<string> Facilities { get; set; } = new();
    public string       Slug       { get; set; } = string.Empty;
    public string       Type       { get; set; } = string.Empty;
}

public class SportsCentreCard
{
    public string       Name         { get; set; } = string.Empty;
    public string       Address      { get; set; } = string.Empty;
    public string       Phone        { get; set; } = string.Empty;
    public string       Email        { get; set; } = string.Empty;
    public string       OpeningHours { get; set; } = string.Empty;
    public List<string> Facilities   { get; set; } = new();
    public string       PageUrl      { get; set; } = string.Empty;
    public string       Type         { get; set; } = string.Empty;
}

public class BradfordHomesProperty
{
    public string       Id          { get; set; } = string.Empty;
    public string       Title       { get; set; } = string.Empty;
    public string       Address     { get; set; } = string.Empty;
    public string       Rent        { get; set; } = string.Empty;
    public string       Landlord    { get; set; } = string.Empty;
    public string       Bedrooms    { get; set; } = string.Empty;
    public string       Distance    { get; set; } = string.Empty;
    public string       ImageUrl    { get; set; } = string.Empty;
    public string       DetailUrl   { get; set; } = string.Empty;
    public string       PropertyRef { get; set; } = string.Empty;
    public List<string> Features    { get; set; } = new();
}

public class BradfordHomesResult
{
    public List<BradfordHomesProperty> Items       { get; set; } = new();
    public int                         TotalFound  { get; set; }
    public string                      SearchUrl   { get; set; } = string.Empty;
    public string                      Location    { get; set; } = string.Empty;
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

public class TermPeriod
{
    public string Label { get; set; } = string.Empty;  // "Christmas Holiday"
    public string Dates { get; set; } = string.Empty;  // "22 Dec 2025 – 2 Jan 2026"
    public string Type  { get; set; } = string.Empty;  // "christmas" | "easter" | "halfterm" | "summer" | "term"
    public bool   Past  { get; set; }
}

public class SchoolCard
{
    public string Name          { get; set; } = string.Empty;
    public string Address       { get; set; } = string.Empty;
    public string Phone         { get; set; } = string.Empty;
    public string Website       { get; set; } = string.Empty;
    public string Phase         { get; set; } = string.Empty;
    public string Type          { get; set; } = string.Empty;
    public string Headteacher   { get; set; } = string.Empty;
    public string OfstedRating  { get; set; } = string.Empty;
    public string OfstedDate    { get; set; } = string.Empty;
    public string Pupils        { get; set; } = string.Empty;
    public string AgeRange      { get; set; } = string.Empty;
    public string Urn           { get; set; } = string.Empty;
    public string AdmissionsUrl { get; set; } = "https://www.bradford.gov.uk/education-and-skills/school-admissions/apply-for-a-place-at-one-of-bradford-districts-schools/";
    public string OfstedUrl     { get; set; } = "https://reports.ofsted.gov.uk/";
    public string TransportUrl  { get; set; } = "https://www.bradford.gov.uk/education-and-skills/travel-assistance/assistance-with-travel-to-home-school-and-college/";
    public string FreeMealsUrl  { get; set; } = "https://www.bradford.gov.uk/education-and-skills/school-meals/paying-for-school-meals/";
    public string TermDatesUrl  { get; set; } = "https://www.bradford.gov.uk/education-and-skills/school-holidays-and-term-dates/school-holidays-and-term-dates/";
    public List<TermPeriod>  TermPeriods { get; set; } = new();
    public string            AcademicYear { get; set; } = string.Empty;
    public bool              IsAcademy    { get; set; }
    public List<string>      Facilities   { get; set; } = new();
}
