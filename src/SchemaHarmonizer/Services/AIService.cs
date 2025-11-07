using Azure.AI.OpenAI;
using OpenAI.Chat;
using SchemaHarmonizer.Models;

namespace SchemaHarmonizer.Services;

public interface IAIService
{
    Task<AIConnectionTestResult> TestConnectionAsync();
    Task<string> GenerateTextAsync(string prompt, CancellationToken cancellationToken = default);
}

public class AIService : IAIService
{
    private readonly AzureOpenAIClient _client;
    private readonly AzureAIOptions _options;
    private readonly ILogger<AIService> _logger;

    public AIService(AzureOpenAIClient client, AzureAIOptions options, ILogger<AIService> logger)
    {
        _client = client;
        _options = options;
        _logger = logger;
    }

    public async Task<AIConnectionTestResult> TestConnectionAsync()
    {
        try
        {
            _logger.LogInformation("Testing connection to Azure AI endpoint: {Endpoint}", _options.Endpoint);
            _logger.LogInformation("Using RBAC authentication: {UseRBAC}", _options.UseRBAC);

            // Debug: Log configuration details (without sensitive data)
            _logger.LogDebug("Using deployment: {DeploymentName}, Model: {Model}",
                _options.DeploymentName, _options.Model);            // Simple test: Generate a very basic response
            var chatClient = _client.GetChatClient(_options.DeploymentName);

            var chatCompletion = await chatClient.CompleteChatAsync(
                [
                    ChatMessage.CreateSystemMessage("You are a helpful assistant. Respond with only 'Connection successful' if you receive this message."),
                    ChatMessage.CreateUserMessage("Test connection")
                ]
            );

            var response = chatCompletion.Value.Content[0].Text;

            _logger.LogInformation("Successfully connected to Azure AI. Response: {Response}", response);

            return new AIConnectionTestResult
            {
                IsConnected = true,
                Message = $"Successfully connected to Azure AI. Response: {response}",
                TestTime = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Azure AI endpoint");

            var errorMessage = "Failed to connect to Azure AI";
            var errorDetails = ex.Message;

            // Provide specific guidance for common RBAC issues
            if (ex.Message.Contains("403") || ex.Message.Contains("Forbidden"))
            {
                errorMessage = "Access denied - RBAC permissions required";
                errorDetails = "You need 'Cognitive Services OpenAI User' role on the resource. " +
                              "Run: az role assignment create --assignee-object-id $(az ad signed-in-user show --query id -o tsv) " +
                              "--role 'Cognitive Services OpenAI User' --scope '/subscriptions/{subscription-id}/resourceGroups/{rg}/providers/Microsoft.CognitiveServices/accounts/{resource-name}'";
            }
            else if (ex.Message.Contains("AuthenticationTypeDisabled"))
            {
                errorMessage = "Key-based authentication disabled - using RBAC";
                errorDetails = "Resource requires RBAC authentication. Ensure you're logged into Azure CLI and have proper role assignments.";
            }

            return new AIConnectionTestResult
            {
                IsConnected = false,
                Message = errorMessage,
                ErrorDetails = errorDetails,
                TestTime = DateTime.UtcNow
            };
        }
    }

    public async Task<string> GenerateTextAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            var chatClient = _client.GetChatClient(_options.DeploymentName);

            var chatCompletion = await chatClient.CompleteChatAsync(
                [
                    ChatMessage.CreateSystemMessage("You are a helpful assistant specialized in data schema harmonization."),
                    ChatMessage.CreateUserMessage(prompt)
                ],
                cancellationToken: cancellationToken
            );

            return chatCompletion.Value.Content[0].Text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating text with prompt: {Prompt}", prompt);
            throw;
        }
    }
}