using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SchemaHarmonizer.Services;
using SchemaHarmonizer.Models;
using SchemaHarmonizer.Components;
using System.Text.Json;

namespace SchemaHarmonizer.Components.Pages;

public partial class Home : ComponentBase
{
    [Inject] private IFileService FileService { get; set; } = null!;
    [Inject] private IAIService AIService { get; set; } = null!;
    [Inject] private ITokenCountService TokenCountService { get; set; } = null!;

    [Inject] private IAnnotationService AnnotationService { get; set; } = null!;
    [Inject] private ILogger<Home> Logger { get; set; } = null!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

    private string nonStandardSchemaPath = string.Empty;
    private string annotationPath = string.Empty;
    private string jsonOutput = string.Empty;

    private string nonStandardSchemaContent = string.Empty;
    private string canonicalSchemaContent = string.Empty;
    private AnnotationFile? annotation = null;

    private List<string> availableAnnotations = new();
    private bool useAnnotations = false;

    private string processingPrompt = string.Empty; // This will be populated by UpdatePromptWithExamples()

    private bool isProcessing = false;


    private FileBrowser? nonStandardFileBrowser;

    private bool isTestingConnection = false;
    private AIConnectionTestResult? aiTestResult;

    // Token counting properties
    private TokenStats? currentTokenStats;
    private int EstimatedTotalTokens => currentTokenStats?.EstimatedTotalTokens ?? 0;
    private int InputTokens => currentTokenStats?.TotalTokens ?? 0;
    private int EstimatedResponseTokens => currentTokenStats?.EstimatedResponseTokens ?? 0;



    private bool CanProcess => !string.IsNullOrWhiteSpace(nonStandardSchemaContent) &&
                                !string.IsNullOrWhiteSpace(processingPrompt) &&
                                !isProcessing;

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



    private void BrowseNonStandardSchema()
    {
        nonStandardFileBrowser?.ShowModal();
    }



    private async Task OnNonStandardSchemaSelected(string fileName)
    {
        nonStandardSchemaPath = fileName;
        try
        {
            nonStandardSchemaContent = await FileService.ReadFileAsync(fileName) ?? string.Empty;

            // Clear previous results when new schema is selected
            jsonOutput = string.Empty;

            // Update the prompt with the appropriate examples/instructions based on file type
            await UpdatePromptWithExamples();

            StateHasChanged(); // Ensure UI updates immediately

            // Recalculate token statistics
            UpdateTokenStatistics();
        }
        catch (Exception ex)
        {
            nonStandardSchemaContent = $"Error reading file: {ex.Message}";
        }
    }

    private async Task ProcessData()
    {
        if (string.IsNullOrWhiteSpace(nonStandardSchemaContent) ||
            string.IsNullOrWhiteSpace(processingPrompt))
        {
            jsonOutput = "{\n  \"error\": \"Missing required data: customer data or processing prompt\"\n}";
            return;
        }

        isProcessing = true;
        jsonOutput = string.Empty;
        canonicalSchemaContent = string.Empty;
        StateHasChanged();

        try
        {
            Logger.LogInformation("Starting data processing with AI");

            // The prompt already has the data included, but ensure any remaining placeholders are replaced
            var completePrompt = processingPrompt.Contains("{INPUT_DATA}")
                ? processingPrompt.Replace("{INPUT_DATA}", nonStandardSchemaContent)
                : processingPrompt;

            Logger.LogDebug("Using complete prompt for AI processing, length: {Length}", completePrompt.Length);

            var schemaType = DetectSchemaType(nonStandardSchemaPath);

            if (useAnnotations && annotation != null)
            {
                Logger.LogInformation("Using annotations for {Id}", annotation.Id);
            }
            Logger.LogDebug("Detected schema type: {SchemaType}", schemaType);

            Logger.LogDebug("Sending harmonization prompt to AI service");

            // Call the AI service to generate the harmonized schema
            var harmonizedResult = await AIService.GenerateTextAsync(completePrompt);

            if (!string.IsNullOrWhiteSpace(harmonizedResult))
            {
                jsonOutput = harmonizedResult;

                // Load canonical schema for comparison display
                canonicalSchemaContent = await LoadCanonicalSchemaAsync(schemaType);

                Logger.LogInformation("Schema harmonization completed successfully");

                // Automatically validate accuracy

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
            isProcessing = false;
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

    private async Task UpdatePromptWithExamples()
    {
        // Handle case when no file is selected yet
        if (string.IsNullOrEmpty(nonStandardSchemaPath))
        {
            processingPrompt = @"You are a data processing expert. Your task is to transform and standardize customer data.

Please select a customer data file to see the specific processing instructions and examples.

The system will automatically:
1. Detect the data type (drilling, production, seismic, or well data)
2. Load the appropriate canonical schema reference
3. Generate relevant transformation examples
4. Provide detailed processing instructions

Select a file from the file browser to begin.";
            StateHasChanged();
            return;
        }

        var schemaType = DetectSchemaType(nonStandardSchemaPath);

        // Build the base prompt
        var basePrompt = $@"You are a data processing expert. Your task is to transform and standardize customer data.";

        // Add annotation instructions, customer mapping instructions, OR few-shot examples (prioritize annotations)
        if (useAnnotations && annotation != null)
        {
            // Load the raw JSON content of the annotation file and canonical schema
            var annotationFileContent = await FileService.ReadFileAsync(annotationPath);
            var canonicalSchema = await LoadCanonicalSchemaAsync(schemaType);

            // Create the combined annotation + canonical schema prompt
            basePrompt += $@"

CANONICAL SCHEMA REFERENCE:
The target canonical format for {schemaType} data is:
{canonicalSchema}

To help guide you in the task, you have been provided with a data annotation file. Use the information in this annotation file to interpret, map, and transform the customer (non-canonical) data file into the correct canonical format. The annotation file uses @semantic tags to describe the meaning of each field in the customer data.

IMPORTANT: Follow these annotations exactly to ensure proper data processing and validation.

The @semantic tags indicate the canonical field meanings:
- Look for @semantic values to understand what each customer field represents
- Map customer field names to their semantic meanings
- Ensure the output matches the canonical schema structure and requirements
- Apply correct data types and formats based on the semantic meanings

Use these data annotations to guide processing:

{annotationFileContent}";
        }
        else
        {
            // Use canonical schema-based examples when no annotation or customer mapping is active
            var canonicalSchema = await LoadCanonicalSchemaAsync(schemaType);
            var schemaBasedExamples = GenerateSchemaBasedExamples(schemaType, canonicalSchema);
            basePrompt += $@"

{schemaBasedExamples}";
        }

        // Complete the prompt
        // Replace INPUT_DATA immediately if we have content to show in the textarea
        var inputDataSection = string.IsNullOrEmpty(nonStandardSchemaContent)
            ? "{INPUT_DATA}"
            : nonStandardSchemaContent;

        // Different instructions based on whether annotations are being used
        var instructions = "";
        if (useAnnotations && annotation != null)
        {
            instructions = @"CRITICAL INSTRUCTIONS:
1. Study the canonical schema structure shown above - this is your TARGET FORMAT
2. Use the @semantic tags in the annotation file to understand what each field represents
3. Transform the non-canonical data to match the canonical format EXACTLY
4. Map field names from non-canonical to canonical field names precisely
5. Follow the canonical schema's nested object structure (e.g., {{""value"": X, ""unit"": ""Y""}})
6. Preserve all data values while strictly conforming to the canonical structure
7. If a field exists in non-canonical data but not in canonical schema, include it using the closest canonical pattern
8. For missing required fields in the canonical schema, provide appropriate default values
9. Maintain proper data types as shown in the canonical schema examples
10. Return ONLY valid JSON that strictly follows the canonical schema format
11. NO explanations, comments, or additional text - JSON output only

Transform the input data using the semantic annotations provided:";
        }
        else
        {
            instructions = @"CRITICAL INSTRUCTIONS:
1. Study the canonical schema structure shown above - this is your TARGET FORMAT
2. Transform the non-canonical data to match the canonical format EXACTLY
3. Map field names from non-canonical to canonical field names precisely
4. Follow the canonical schema's nested object structure (e.g., {{""value"": X, ""unit"": ""Y""}})
5. Preserve all data values while strictly conforming to the canonical structure
6. If a field exists in non-canonical data but not in canonical schema, include it using the closest canonical pattern
7. For missing required fields in the canonical schema, provide appropriate default values
8. Maintain proper data types as shown in the canonical schema examples
9. Return ONLY valid JSON that strictly follows the canonical schema format
10. NO explanations, comments, or additional text - JSON output only

Transform the input data to match the canonical schema format exactly:";
        }

        processingPrompt = $@"{basePrompt}

NON-CANONICAL DATA TO TRANSFORM:
{inputDataSection}

{instructions}";

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

    private async Task<string> LoadCanonicalSchemaAsync(string schemaType)
    {
        try
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));
            var schemaPath = Path.Combine(projectRoot, "sample-data", "annotations", "canonical", $"{schemaType}-data", "partner_canonical_schema.json");

            if (File.Exists(schemaPath))
            {
                var schemaContent = await File.ReadAllTextAsync(schemaPath);
                Logger.LogInformation("Loaded canonical schema for {SchemaType}", schemaType);
                return schemaContent;
            }
            else
            {
                Logger.LogWarning("Canonical schema not found for {SchemaType} at {Path}", schemaType, schemaPath);
                return string.Empty;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading canonical schema for {SchemaType}", schemaType);
            return string.Empty;
        }
    }

    private string GenerateSchemaBasedExamples(string schemaType, string canonicalSchema)
    {
        if (string.IsNullOrWhiteSpace(canonicalSchema))
        {
            // Fallback to original examples if canonical schema is not available
            return GenerateFewShotExamples(schemaType);
        }

        var examples = $@"CANONICAL SCHEMA REFERENCE:
The target canonical format for {schemaType} data is:
{canonicalSchema}

TRANSFORMATION EXAMPLES BASED ON CANONICAL SCHEMA:

Example 1 - Basic Field Mapping:";

        switch (schemaType.ToLowerInvariant())
        {
            case "drilling":
                examples += @"
Non-canonical input: {""WellName"": ""Alpha-1"", ""Depth_ft"": 8500, ""Date"": ""2025-11-06""}
Canonical output: {""wellId"": ""Alpha-1"", ""measuredDepth"": {""value"": 8500, ""unit"": ""ft""}, ""timestamp"": ""2025-11-06""}

Example 2 - Complex Field Transformation:
Non-canonical input: {""MudWeight_ppg"": 12.5, ""ROP_ft_hr"": 45, ""Formation"": ""Sandstone""}
Canonical output: {""mudWeight"": {""value"": 12.5, ""unit"": ""ppg""}, ""rateOfPenetration"": {""value"": 45, ""unit"": ""ft/hr""}, ""formationType"": ""Sandstone""}

Example 3 - Unit Structure Normalization:
Non-canonical input: {""total_depth"": ""8500 feet"", ""mud_weight"": ""12.5 pounds per gallon""}
Canonical output: {""measuredDepth"": {""value"": 8500, ""unit"": ""ft""}, ""mudWeight"": {""value"": 12.5, ""unit"": ""ppg""}}";
                break;

            case "production":
                examples += @"
Non-canonical input: {""well_id"": ""Alpha-1"", ""oil_bbl"": 150, ""gas_mcf"": 850, ""date"": ""2024-01-15""}
Canonical output: {""wellId"": ""Alpha-1"", ""oilProduction"": {""value"": 150, ""unit"": ""bbl""}, ""gasProduction"": {""value"": 850, ""unit"": ""mcf""}, ""productionDate"": ""2024-01-15""}

Example 2 - Water Production:
Non-canonical input: {""water_production"": 25, ""water_cut_percent"": 15}
Canonical output: {""waterProduction"": {""value"": 25, ""unit"": ""bbl""}, ""waterCutPercentage"": 15}";
                break;

            case "seismic":
                examples += @"
Non-canonical input: {""survey_name"": ""Eagle Ford 3D"", ""shot_date"": ""2024-01-15"", ""x_coord"": 123456.78}
Canonical output: {""surveyName"": ""Eagle Ford 3D"", ""acquisitionDate"": ""2024-01-15"", ""coordinates"": {""x"": 123456.78}}";
                break;

            default:
                examples += @"
Non-canonical input: {""id"": ""sample-001"", ""create_date"": ""2024-01-15""}
Canonical output: {""identifier"": ""sample-001"", ""createdDate"": ""2024-01-15""}";
                break;
        }

        return examples;
    }

    private void UpdateTokenStatistics()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(nonStandardSchemaContent))
            {
                // Generate the complete prompt (examples already included in processingPrompt)
                var completePrompt = processingPrompt
                    .Replace("{INPUT_DATA}", nonStandardSchemaContent);

                currentTokenStats = TokenCountService.GetTokenStats(
                    "",
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



    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await InitializeTooltips();
        }
    }

    private async Task InitializeTooltips()
    {
        try
        {
            // More robust tooltip initialization that checks for Bootstrap availability
            await JSRuntime.InvokeVoidAsync("eval", @"
                if (typeof bootstrap !== 'undefined' && bootstrap.Tooltip) {
                    // Dispose existing tooltips first to avoid duplicates
                    document.querySelectorAll('[data-bs-toggle=""tooltip""]').forEach(function(element) {
                        var tooltip = bootstrap.Tooltip.getInstance(element);
                        if (tooltip) {
                            tooltip.dispose();
                        }
                    });

                    // Initialize new tooltips
                    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle=""tooltip""]'));
                    var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
                        return new bootstrap.Tooltip(tooltipTriggerEl);
                    });
                } else {
                    console.warn('Bootstrap is not loaded, tooltips will not be initialized');
                }
            ");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error initializing tooltips - this is non-critical");
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await LoadAvailableAnnotations();

        // Initialize the prompt with default content
        await UpdatePromptWithExamples();
    }

    private async Task LoadAvailableAnnotations()
    {
        try
        {
            availableAnnotations = await FileService.GetAnnotationFilesAsync();
            Logger.LogInformation("Loaded {Count} annotation files", availableAnnotations.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load annotation files");
            availableAnnotations = new List<string>();
        }
    }

    private async Task OnAnnotationSelected(ChangeEventArgs e)
    {
        annotationPath = e.Value?.ToString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(annotationPath))
        {
            annotation = null;
            useAnnotations = false;
            return;
        }

        try
        {
            annotation = await AnnotationService.LoadAnnotationFileAsync(annotationPath);
            if (annotation != null)
            {
                useAnnotations = true;
                Logger.LogInformation("Loaded annotation file for {Id}", annotation.Id);

                // Clear previous results when annotation is selected
                jsonOutput = string.Empty;

                // Update the prompt with annotation instructions
                await UpdatePromptWithExamples();

                // Update token statistics with new annotation data
                UpdateTokenStatistics();
            }
            else
            {
                Logger.LogError("Failed to load annotation from {Path}", annotationPath);
                useAnnotations = false;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading annotation: {Path}", annotationPath);
            annotation = null;
            useAnnotations = false;
        }

        StateHasChanged();
    }

    private async Task ToggleAnnotations(ChangeEventArgs e)
    {
        useAnnotations = bool.Parse(e.Value?.ToString() ?? "false");
        if (!useAnnotations)
        {
            annotation = null;
            annotationPath = string.Empty;
        }

        // Clear previous results when toggling annotations
        jsonOutput = string.Empty;

        // Update the prompt to reflect the annotation change
        await UpdatePromptWithExamples();

        // Update token statistics
        UpdateTokenStatistics();
        StateHasChanged();
    }

    private string DetermineSchemaType(string schemaPath)
    {
        if (schemaPath.Contains("drilling", StringComparison.OrdinalIgnoreCase))
            return "drilling";
        if (schemaPath.Contains("production", StringComparison.OrdinalIgnoreCase))
            return "production";
        if (schemaPath.Contains("seismic", StringComparison.OrdinalIgnoreCase))
            return "seismic";
        if (schemaPath.Contains("well", StringComparison.OrdinalIgnoreCase))
            return "well";

        return "generic";
    }







}