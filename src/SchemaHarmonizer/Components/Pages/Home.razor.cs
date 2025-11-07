using Microsoft.AspNetCore.Components;
using SchemaHarmonizer.Services;
using SchemaHarmonizer.Models;
using SchemaHarmonizer.Components;

namespace SchemaHarmonizer.Components.Pages;

public partial class Home : ComponentBase
{
    [Inject] private IFileService FileService { get; set; } = null!;
    [Inject] private IAIService AIService { get; set; } = null!;
    [Inject] private ITokenCountService TokenCountService { get; set; } = null!;
    [Inject] private IAccuracyValidationService AccuracyValidationService { get; set; } = null!;
    [Inject] private ILogger<Home> Logger { get; set; } = null!;

    private string standardSchemaPath = string.Empty;
    private string nonStandardSchemaPath = string.Empty;
    private string jsonOutput = string.Empty;

    private string standardSchemaContent = string.Empty;
    private string nonStandardSchemaContent = string.Empty;

    private string harmonizationPrompt = @"You are a data schema harmonization expert. Your task is to transform non-canonical data to match a canonical schema format.

CANONICAL SCHEMA TEMPLATE:
{CANONICAL_SCHEMA}

{FEW_SHOT_EXAMPLES}

NON-CANONICAL DATA TO TRANSFORM:
{NON_CANONICAL_DATA}

Instructions:
1. Analyze the canonical schema structure and field mappings
2. Transform the non-canonical data to match the canonical format exactly  
3. Map corresponding fields from non-canonical to canonical names
4. Preserve all data values while conforming to canonical structure
5. Add any missing required fields with appropriate default values
6. Return ONLY the transformed JSON data, no explanations

Output the harmonized data in valid JSON format that matches the canonical schema:";

    private bool isHarmonizing = false;

    private FileBrowser? standardFileBrowser;
    private FileBrowser? nonStandardFileBrowser;

    private bool isTestingConnection = false;
    private AIConnectionTestResult? aiTestResult;

    // Token counting properties
    private TokenStats? currentTokenStats;
    private int EstimatedTotalTokens => currentTokenStats?.EstimatedTotalTokens ?? 0;
    private int InputTokens => currentTokenStats?.TotalTokens ?? 0;
    private int EstimatedResponseTokens => currentTokenStats?.EstimatedResponseTokens ?? 0;

    // Accuracy validation properties
    private AccuracyResult? accuracyResult;
    private bool isValidatingAccuracy = false;

    private bool CanHarmonize => !string.IsNullOrWhiteSpace(standardSchemaContent) &&
                                !string.IsNullOrWhiteSpace(nonStandardSchemaContent) &&
                                !string.IsNullOrWhiteSpace(harmonizationPrompt) &&
                                !isHarmonizing;

    private async Task TestAIConnection()
    {
        Logger.LogInformation("Starting AI connection test from UI");
        isTestingConnection = true;
        aiTestResult = null;
        StateHasChanged();

        try
        {
            aiTestResult = await AIService.TestConnectionAsync();
            Logger.LogInformation("AI connection test completed. Success: {IsConnected}", aiTestResult.IsConnected);
        }
        catch (Exception ex)
        {
            aiTestResult = new AIConnectionTestResult
            {
                IsConnected = false,
                Message = "Connection test failed",
                ErrorDetails = ex.Message
            };
        }
        finally
        {
            isTestingConnection = false;
            StateHasChanged();
        }
    }

    private void BrowseStandardSchema()
    {
        standardFileBrowser?.ShowModal();
    }

    private void BrowseNonStandardSchema()
    {
        nonStandardFileBrowser?.ShowModal();
    }

    private async Task OnStandardSchemaSelected(string fileName)
    {
        standardSchemaPath = fileName;
        try
        {
            standardSchemaContent = await FileService.ReadFileAsync(fileName);

            // Update the prompt with context-specific examples
            UpdatePromptWithExamples();

            // Recalculate token statistics
            UpdateTokenStatistics();
        }
        catch (Exception ex)
        {
            standardSchemaContent = $"Error reading file: {ex.Message}";
        }
    }

    private async Task OnNonStandardSchemaSelected(string fileName)
    {
        nonStandardSchemaPath = fileName;
        try
        {
            nonStandardSchemaContent = await FileService.ReadFileAsync(fileName);

            // Recalculate token statistics
            UpdateTokenStatistics();
        }
        catch (Exception ex)
        {
            nonStandardSchemaContent = $"Error reading file: {ex.Message}";
        }
    }

    private async Task HarmonizeSchemas()
    {
        if (string.IsNullOrWhiteSpace(standardSchemaContent) ||
            string.IsNullOrWhiteSpace(nonStandardSchemaContent) ||
            string.IsNullOrWhiteSpace(harmonizationPrompt))
        {
            jsonOutput = "{\n  \"error\": \"Missing required data: canonical schema, non-canonical data, or harmonization prompt\"\n}";
            return;
        }

        isHarmonizing = true;
        jsonOutput = string.Empty;
        StateHasChanged();

        try
        {
            Logger.LogInformation("Starting schema harmonization with AI");

            // Build the complete prompt by replacing placeholders (examples already included)
            var completePrompt = harmonizationPrompt
                .Replace("{CANONICAL_SCHEMA}", standardSchemaContent)
                .Replace("{NON_CANONICAL_DATA}", nonStandardSchemaContent);

            var schemaType = DetectSchemaType(standardSchemaPath);
            Logger.LogDebug("Detected schema type: {SchemaType}", schemaType);

            Logger.LogDebug("Sending harmonization prompt to AI service");

            // Call the AI service to generate the harmonized schema
            var harmonizedResult = await AIService.GenerateTextAsync(completePrompt);

            if (!string.IsNullOrWhiteSpace(harmonizedResult))
            {
                jsonOutput = harmonizedResult;
                Logger.LogInformation("Schema harmonization completed successfully");

                // Automatically validate accuracy
                await ValidateAccuracy();
            }
            else
            {
                jsonOutput = "{\n  \"error\": \"AI service returned empty response\"\n}";
                Logger.LogWarning("AI service returned empty response for schema harmonization");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during schema harmonization");
            jsonOutput = $"{{\n  \"error\": \"Schema harmonization failed: {ex.Message}\",\n  \"timestamp\": \"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}\"\n}}";
        }
        finally
        {
            isHarmonizing = false;
            StateHasChanged();
        }
    }

    private string DetectSchemaType(string schemaPath)
    {
        if (string.IsNullOrEmpty(schemaPath))
            return "unknown";

        var lowerPath = schemaPath.ToLowerInvariant();

        if (lowerPath.Contains("drilling"))
            return "drilling";
        else if (lowerPath.Contains("production"))
            return "production";
        else if (lowerPath.Contains("seismic"))
            return "seismic";
        else if (lowerPath.Contains("well"))
            return "well";

        return "unknown";
    }

    private void UpdatePromptWithExamples()
    {
        var schemaType = DetectSchemaType(standardSchemaPath);
        var fewShotExamples = GenerateFewShotExamples(schemaType);

        // Build the complete prompt with actual examples included
        harmonizationPrompt = $@"You are a data schema harmonization expert. Your task is to transform non-canonical data to match a canonical schema format.

CANONICAL SCHEMA TEMPLATE:
{{CANONICAL_SCHEMA}}

{fewShotExamples}

NON-CANONICAL DATA TO TRANSFORM:
{{NON_CANONICAL_DATA}}

Instructions:
1. Analyze the canonical schema structure and field mappings
2. Transform the non-canonical data to match the canonical format exactly  
3. Map corresponding fields from non-canonical to canonical names
4. Preserve all data values while conforming to canonical structure
5. Add any missing required fields with appropriate default values
6. Return ONLY the transformed JSON data, no explanations

Output the harmonized data in valid JSON format that matches the canonical schema:";

        StateHasChanged();
    }

    private string GenerateFewShotExamples(string schemaType)
    {
        return schemaType.ToLowerInvariant() switch
        {
            "drilling" => @"
EXAMPLE TRANSFORMATIONS FOR DRILLING DATA:

Example 1 - Field Mapping:
Non-canonical input: {""drill_date"": ""2024-01-15"", ""depth_ft"": 8500}
Canonical output: {""drillingDate"": ""2024-01-15"", ""totalDepthFeet"": 8500}

Example 2 - Unit Conversion:  
Non-canonical input: {""mud_weight"": ""12.5 ppg"", ""rop"": ""45 ft/hr""}
Canonical output: {""mudWeight"": 12.5, ""mudWeightUnit"": ""ppg"", ""rateOfPenetration"": 45, ""ropUnit"": ""ft/hr""}

Example 3 - Structure Normalization:
Non-canonical input: {""location"": ""Section 12, Township 5N, Range 3W""}  
Canonical output: {""location"": {""section"": 12, ""township"": ""5N"", ""range"": ""3W""}}",

            "production" => @"
EXAMPLE TRANSFORMATIONS FOR PRODUCTION DATA:

Example 1 - Field Mapping:
Non-canonical input: {""prod_date"": ""2024-01-15"", ""oil_bbl"": 150, ""gas_mcf"": 850}
Canonical output: {""productionDate"": ""2024-01-15"", ""oilProductionBarrels"": 150, ""gasProductionMcf"": 850}

Example 2 - Unit Standardization:
Non-canonical input: {""water_cut"": ""15%"", ""gor"": ""567 scf/bbl""}
Canonical output: {""waterCutPercent"": 15, ""gasOilRatio"": 567, ""gorUnit"": ""scf/bbl""}

Example 3 - Nested Structure:
Non-canonical input: {""well_id"": ""ABC-123"", ""operator"": ""ExampleCorp""}
Canonical output: {""wellIdentifier"": ""ABC-123"", ""operator"": {""name"": ""ExampleCorp""}}",

            "seismic" => @"
EXAMPLE TRANSFORMATIONS FOR SEISMIC DATA:

Example 1 - Survey Information:
Non-canonical input: {""survey_name"": ""Eagle Ford 3D"", ""shot_date"": ""2024-01-15""}
Canonical output: {""surveyName"": ""Eagle Ford 3D"", ""acquisitionDate"": ""2024-01-15""}

Example 2 - Coordinate System:
Non-canonical input: {""x_coord"": 123456.78, ""y_coord"": 987654.32, ""datum"": ""NAD83""}
Canonical output: {""coordinates"": {""x"": 123456.78, ""y"": 987654.32, ""coordinateSystem"": ""NAD83""}}

Example 3 - Processing Parameters:
Non-canonical input: {""fold"": 60, ""bin_size"": ""82.5x82.5""}
Canonical output: {""foldCoverage"": 60, ""binSize"": {""x"": 82.5, ""y"": 82.5, ""unit"": ""feet""}}",

            "well" => @"
EXAMPLE TRANSFORMATIONS FOR WELL DATA:

Example 1 - Well Header:
Non-canonical input: {""api_number"": ""42-123-45678"", ""well_name"": ""Smith #1""}
Canonical output: {""apiNumber"": ""42-123-45678"", ""wellName"": ""Smith #1""}

Example 2 - Location Data:
Non-canonical input: {""latitude"": 32.7767, ""longitude"": -96.7970, ""county"": ""Dallas""}
Canonical output: {""location"": {""latitude"": 32.7767, ""longitude"": -96.7970, ""county"": ""Dallas"", ""state"": null}}

Example 3 - Well Status:
Non-canonical input: {""status"": ""PRODUCING"", ""spud_date"": ""2023-06-15""}
Canonical output: {""wellStatus"": ""PRODUCING"", ""spudDate"": ""2023-06-15"", ""statusDate"": null}",

            _ => @"
GENERAL TRANSFORMATION EXAMPLES:

Example 1 - Field Naming:
Non-canonical input: {""create_dt"": ""2024-01-15T10:30:00Z""}
Canonical output: {""createdDate"": ""2024-01-15T10:30:00Z""}

Example 2 - Data Type Conversion:
Non-canonical input: {""active"": ""true"", ""count"": ""42""}  
Canonical output: {""isActive"": true, ""itemCount"": 42}

Example 3 - Structure Flattening:
Non-canonical input: {""metadata_version"": ""1.0"", ""metadata_source"": ""system""}
Canonical output: {""metadata"": {""version"": ""1.0"", ""source"": ""system""}}"
        };
    }

    private void UpdateTokenStatistics()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(standardSchemaContent) &&
                !string.IsNullOrWhiteSpace(nonStandardSchemaContent))
            {
                // Generate the complete prompt (examples already included in harmonizationPrompt)
                var completePrompt = harmonizationPrompt
                    .Replace("{CANONICAL_SCHEMA}", standardSchemaContent)
                    .Replace("{NON_CANONICAL_DATA}", nonStandardSchemaContent);

                currentTokenStats = TokenCountService.GetTokenStats(
                    standardSchemaContent,
                    nonStandardSchemaContent,
                    completePrompt);

                Logger.LogDebug("Token statistics updated: Input={InputTokens}, Estimated Total={EstimatedTotal}",
                    currentTokenStats.TotalTokens, currentTokenStats.EstimatedTotalTokens);
            }
            else
            {
                currentTokenStats = null;
            }

            // Trigger UI update
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error calculating token statistics");
            currentTokenStats = null;
        }
    }

    private async Task ValidateAccuracy()
    {
        if (string.IsNullOrWhiteSpace(standardSchemaContent) ||
            string.IsNullOrWhiteSpace(nonStandardSchemaContent) ||
            string.IsNullOrWhiteSpace(jsonOutput) ||
            jsonOutput.Contains("error"))
        {
            accuracyResult = null;
            return;
        }

        isValidatingAccuracy = true;
        StateHasChanged();

        try
        {
            Logger.LogInformation("Starting accuracy validation");

            accuracyResult = await AccuracyValidationService.ValidateHarmonizationAsync(
                standardSchemaContent,
                nonStandardSchemaContent,
                jsonOutput);

            Logger.LogInformation("Accuracy validation completed. Overall accuracy: {Accuracy}%",
                accuracyResult.OverallAccuracyPercentage.ToString("F1"));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during accuracy validation");
            accuracyResult = new AccuracyResult
            {
                Issues = new List<AccuracyIssue>
                {
                    new AccuracyIssue
                    {
                        Type = "validation_error",
                        Description = $"Accuracy validation failed: {ex.Message}",
                        Severity = "critical"
                    }
                }
            };
        }
        finally
        {
            isValidatingAccuracy = false;
            StateHasChanged();
        }
    }
}