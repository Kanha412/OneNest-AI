namespace OneNest.Application.DTOs.Contact;

public class ContactSummaryResponse
{
    public int TotalMessages { get; set; }
    public int NewCount { get; set; }
    public int ReadCount { get; set; }
    public int ResolvedCount { get; set; }
    public List<ContactMessageResponse> RecentMessages { get; set; } = new();
}
