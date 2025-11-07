# Debugging Guide for Schema Harmonizer

## Quick Start Debugging

### 1. **VS Code Debugging Setup**

1. Open the project in VS Code
2. Install recommended extensions (VS Code will prompt you)
3. Press `F5` to start debugging

### 2. **Available Debug Configurations**

#### **Launch SchemaHarmonizer (Debug)** - Recommended
- Full debugging with breakpoints
- Automatic browser opening
- Clean build before launch

#### **Launch SchemaHarmonizer (Watch)**
- Hot reload on file changes
- Great for UI development
- Automatic restart on code changes

#### **Attach to SchemaHarmonizer**
- Attach to running process
- Useful for debugging production-like scenarios

### 3. **Setting Breakpoints**

#### **C# Code Breakpoints**
- Services: `src/SchemaHarmonizer/Services/AIService.cs`
- Models: `src/SchemaHarmonizer/Models/AzureAIOptions.cs`
- Program: `src/SchemaHarmonizer/Program.cs`

#### **Razor Page Breakpoints**
- Home page: `src/SchemaHarmonizer/Components/Pages/Home.razor`
- File browser: `src/SchemaHarmonizer/Components/FileBrowser.razor`

#### **Common Debug Points**
```csharp
// AIService.cs - Line ~27
_logger.LogInformation("Testing connection to Azure AI endpoint: {Endpoint}", _options.Endpoint);

// Home.razor - Line ~120  
Logger.LogInformation("Starting AI connection test from UI");

// Program.cs - Line ~32
app.MapGet("/api/ai/test-connection", async (IAIService aiService) =>
```

### 4. **Debug Console Commands**

When debugging, you can use these in the Debug Console:

```csharp
// Check configuration values
_options.Endpoint
_options.DeploymentName
_options.Model

// Check service state
aiTestResult?.IsConnected
standardSchemaPath
nonStandardSchemaPath

// Logger state
Logger.IsEnabled(LogLevel.Debug)
```

### 5. **Logging Levels**

Configure logging in `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "SchemaHarmonizer": "Debug"  // Enable debug logs for our code
    }
  }
}
```

### 6. **Testing Scenarios**

#### **AI Connection Testing**
1. Set breakpoint in `AIService.TestConnectionAsync()`
2. Click "Test AI Connection" button
3. Step through Azure AI client creation and API call

#### **File Selection Testing**
1. Set breakpoint in `Home.OnStandardSchemaSelected()`
2. Click Browse button
3. Select a file from the modal
4. Watch file path assignment

#### **Schema Harmonization Flow**
1. Set breakpoints in `Home.HarmonizeSchemas()`
2. Select both schema files
3. Click Harmonize button
4. Step through file reading and AI processing

### 7. **Common Issues & Solutions**

#### **Breakpoints Not Hitting**
- Ensure you're using the Debug configuration
- Rebuild the project (`Ctrl+Shift+P` → "Tasks: Run Build Task")
- Check that source maps are enabled

#### **AI Connection Failures**
- **RBAC Authentication Issues**:
  - Ensure you're logged into Azure CLI: `az account show`
  - Check if you have the "Cognitive Services OpenAI User" role
  - Run: `az role assignment list --assignee $(az ad signed-in-user show --query id -o tsv) --all`
- **Configuration Issues**:
  - Verify user secrets: `dotnet user-secrets list`  
  - Check endpoint URL format
  - Ensure UseRBAC is set to true in configuration

#### **File Access Issues**
- Ensure sample data files exist in `sample-data/` directories
- Check file permissions
- Verify paths are absolute

### 8. **Performance Debugging**

Enable detailed timing logs:
```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.Hosting.Lifetime": "Debug",
      "Microsoft.AspNetCore.Hosting": "Debug"
    }
  }
}
```

### 9. **Network Debugging**

Monitor HTTP requests to Azure AI:
- Use browser DevTools Network tab
- Check API endpoint: `http://localhost:5161/api/ai/test-connection`
- Verify CORS settings if needed

### 10. **Hot Reload Tips**

When using Watch mode:
- Razor page changes auto-reload
- C# changes trigger rebuild
- CSS/JS changes are instant
- Configuration changes require restart

## Troubleshooting

### **Build Errors**
```bash
# Clean and rebuild
dotnet clean && dotnet build

# Restore packages
dotnet restore
```

### **User Secrets Issues**
```bash
# List current secrets
dotnet user-secrets list

# Re-add if missing
dotnet user-secrets set "AzureAI:ApiKey" "your-key-here"
```

### **Port Conflicts**
If port 5161 is busy, update `launchSettings.json` or set environment variable:
```bash
export ASPNETCORE_URLS="http://localhost:5162"
```