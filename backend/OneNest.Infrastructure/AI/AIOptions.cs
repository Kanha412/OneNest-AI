namespace OneNest.Infrastructure.AI;

public class AIOptions
{
    public string Provider { get; set; } = "Gemini";
    public string Model { get; set; } = "gemini-2.5-flash";
    public string ApiKey { get; set; } = string.Empty;
}
