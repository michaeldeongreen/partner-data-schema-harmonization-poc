namespace SchemaHarmonizer.Models;

public class AzureAIOptions
{
    public const string SectionName = "AzureAI";

    public string Endpoint { get; set; } = string.Empty;
    public string? ApiKey { get; set; } // Optional - only needed for key-based auth
    public string DeploymentName { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int MaxTokens { get; set; } = 1000;
    public double Temperature { get; set; } = 0.3;
    public bool UseRBAC { get; set; } = true; // Default to RBAC authentication
}

public class AIConnectionTestResult
{
    public bool IsConnected { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorDetails { get; set; }
    public DateTime TestTime { get; set; } = DateTime.UtcNow;
}