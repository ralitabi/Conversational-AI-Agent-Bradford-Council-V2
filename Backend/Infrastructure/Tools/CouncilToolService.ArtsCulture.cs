namespace Bradford.Infrastructure.Tools;

public partial class CouncilToolService
{
    private static readonly (string[] Keywords, string[] Urls, string Title, string FollowUp)[] ArtsCultureKnowledgeMap =
    {
        (new[]{"museum","gallery","art gallery","bradford museum","cartwright hall","industrial museum","media museum","national science and media","bradford galleries"},
         new[]{"https://www.bradford.gov.uk/arts-and-culture/museums-and-galleries/museums-and-art-galleries/"},
         "Museums and art galleries in Bradford",
         "Would you like to know about what's on or visiting Bradford?"),

        (new[]{"bradford 2025","city of culture","uk city of culture","bradford2025","culture 2025"},
         new[]{"https://www.bradford.gov.uk/arts-and-culture/whats-on-in-bradford-district/whats-on-in-bradford-district/"},
         "Bradford — what's on",
         "Would you like to know about arts and culture funding or filming in Bradford?"),

        (new[]{"city of film","film bradford","filming bradford","UNESCO city of film","bradford film","film location"},
         new[]{"https://www.bradford.gov.uk/arts-and-culture/bradford-city-of-film/filming-in-bradford/"},
         "Bradford City of Film — filming in Bradford",
         "Would you like to know about what's on in Bradford or arts funding opportunities?"),

        (new[]{"arts grant","culture grant","heritage grant","arts funding","culture funding","arts council","community arts grant","busking grant"},
         new[]{"https://www.bradford.gov.uk/arts-and-culture/funding/arts-culture-and-heritage-grants/",
               "https://www.bradford.gov.uk/arts-and-culture/funding/arts-culture-and-heritage-grant-for-busking-event-or-festival/"},
         "Arts, culture and heritage grants",
         "Would you like to know about capital grants for sports or recreational projects?"),

        (new[]{"city park","centenary square","bradford city park","outdoor event","city centre event"},
         new[]{"https://www.bradford.gov.uk/arts-and-culture/city-park/city-park/"},
         "Bradford City Park",
         "Would you like to know about booking council outdoor spaces for events?"),

        (new[]{"busking","street performance","busker","perform in bradford","play music street"},
         new[]{"https://www.bradford.gov.uk/arts-and-culture/culture/busking-guidance/"},
         "Busking guidance in Bradford",
         "Would you like to know about outdoor event booking or arts grants?"),

        (new[]{"what's on","events bradford","things to do","visit bradford","Bradford events","entertainment bradford"},
         new[]{"https://www.bradford.gov.uk/arts-and-culture/whats-on-in-bradford-district/whats-on-in-bradford-district/",
               "https://www.bradford.gov.uk/arts-and-culture/whats-on-in-bradford-district/visit-bradford/"},
         "What's on in Bradford",
         "Would you like to know about Bradford's museums, City of Film, or arts funding?"),

        (new[]{"arts","culture","heritage","creative","Bradford arts","cultural","Bradford culture"},
         new[]{"https://www.bradford.gov.uk/arts-and-culture/"},
         "Arts and culture in Bradford",
         "What are you looking for? I can help with museums, City of Film, events, arts grants, and City Park."),
    };

    private async Task<string> GetArtsCultureInfoAsync(string query, CancellationToken ct)
        => await ScrapeKnowledgeMapAsync(query, ArtsCultureKnowledgeMap,
            "https://www.bradford.gov.uk/arts-and-culture/",
            "Arts and culture in Bradford",
            "What are you looking for? I can help with museums, City of Film, events, arts grants, and City Park.",
            "BRADFORD ARTS & CULTURE INFORMATION", ct);
}
