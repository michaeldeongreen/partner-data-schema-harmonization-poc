using Azure.AI.OpenAI;
using Azure.Identity;
using SchemaHarmonizer.Components;
using SchemaHarmonizer.Services;
using SchemaHarmonizer.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure Azure AI options
var azureAIOptions = builder.Configuration.GetSection(AzureAIOptions.SectionName).Get<AzureAIOptions>()
    ?? throw new InvalidOperationException("AzureAI configuration is missing");

// Register Azure AI client with RBAC authentication
builder.Services.AddSingleton(provider =>
{
    // Use DefaultAzureCredential for RBAC authentication
    // This will try multiple credential types in order:
    // 1. Environment variables (for production)
    // 2. Azure CLI (for local development)
    // 3. Visual Studio Code (for local development)
    // 4. Managed Identity (for Azure hosting)
    var credential = new DefaultAzureCredential();

    return new AzureOpenAIClient(
        new Uri(azureAIOptions.Endpoint),
        credential);
});

builder.Services.AddSingleton(azureAIOptions);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register custom services
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IAIService, AIService>();
builder.Services.AddScoped<ITokenCountService, TokenCountService>();
builder.Services.AddScoped<IAccuracyValidationService, AccuracyValidationService>();

var app = builder.Build();

// Add API endpoints for testing
app.MapGet("/api/ai/test-connection", async (IAIService aiService) =>
{
    var result = await aiService.TestConnectionAsync();
    return Results.Ok(result);
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
