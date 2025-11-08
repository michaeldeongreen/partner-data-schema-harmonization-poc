using System.Text.Json;
using SchemaHarmonizer.Models;

namespace SchemaHarmonizer.Services;

public interface IAnnotationService
{
    Task<AnnotationFile?> LoadAnnotationFileAsync(string filePath);
    AnnotationValidationResult ValidateAnnotationFile(AnnotationFile annotationFile, string sourceData);
    string GenerateProcessingInstructions(AnnotationFile annotationFile);
    string GenerateDataAnnotationExamples(AnnotationFile annotationFile, string dataType);
}

public class AnnotationValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public int ProcessedAnnotationsCount { get; set; }
    public int TotalAnnotationsCount { get; set; }
    public double AnnotationCompleteness => TotalAnnotationsCount > 0 ? (double)ProcessedAnnotationsCount / TotalAnnotationsCount * 100 : 0;
}

public class AnnotationService : IAnnotationService
{
    private readonly ILogger<AnnotationService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public AnnotationService(ILogger<AnnotationService> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<AnnotationFile?> LoadAnnotationFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Annotation file not found: {FilePath}", filePath);
                return null;
            }

            var jsonContent = await File.ReadAllTextAsync(filePath);
            var annotationFile = JsonSerializer.Deserialize<AnnotationFile>(jsonContent, _jsonOptions);

            if (annotationFile != null)
            {
                _logger.LogInformation("Successfully loaded annotation file for ID: {Id}", annotationFile.Id);
            }

            return annotationFile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading annotation file: {FilePath}", filePath);
            return null;
        }
    }

    public AnnotationValidationResult ValidateAnnotationFile(AnnotationFile annotationFile, string sourceData)
    {
        var result = new AnnotationValidationResult();

        try
        {
            // Parse source data to get available fields
            var sourceDoc = JsonDocument.Parse(sourceData);
            var sourceFields = ExtractFieldPaths(sourceDoc.RootElement);

            result.TotalAnnotationsCount = annotationFile.Annotations.Count;

            // Validate each annotation
            foreach (var annotation in annotationFile.Annotations)
            {
                if (string.IsNullOrWhiteSpace(annotation.FieldPath))
                {
                    result.Errors.Add("Empty field path found in annotation");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(annotation.DataType))
                {
                    result.Errors.Add($"Empty data type for field: {annotation.FieldPath}");
                    continue;
                }

                if (sourceFields.Contains(annotation.FieldPath))
                {
                    result.ProcessedAnnotationsCount++;
                }
                else
                {
                    result.Warnings.Add($"Annotated field not found in source data: {annotation.FieldPath}");
                }
            }

            result.IsValid = result.Errors.Count == 0;
            _logger.LogInformation("Annotation validation completed. Valid: {IsValid}, Errors: {ErrorCount}, Warnings: {WarningCount}",
                result.IsValid, result.Errors.Count, result.Warnings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during annotation validation");
            result.Errors.Add($"Validation error: {ex.Message}");
            result.IsValid = false;
        }

        return result;
    }

    public string GenerateProcessingInstructions(AnnotationFile annotationFile)
    {
        var instructions = new List<string>
        {
            $"DATA ANNOTATION PROCESSING FOR: {annotationFile.Id.ToUpper()}",
            "Use these data annotations to guide processing:",
            ""
        };

        foreach (var annotation in annotationFile.Annotations.OrderBy(a => a.FieldPath))
        {
            var instruction = $"- {annotation.FieldPath}: {annotation.DataType}";

            if (!string.IsNullOrEmpty(annotation.Format))
                instruction += $" (Format: {annotation.Format})";

            if (annotation.IsRequired)
                instruction += " [REQUIRED]";

            if (!string.IsNullOrEmpty(annotation.Description))
                instruction += $" // {annotation.Description}";

            instructions.Add(instruction);
        }

        if (annotationFile.ProcessingRules.Any())
        {
            instructions.Add("");
            instructions.Add("PROCESSING RULES:");
            foreach (var rule in annotationFile.ProcessingRules)
            {
                instructions.Add($"- {rule.RuleType}: {rule.SourcePattern} → {rule.TargetPattern}");
            }
        }

        instructions.Add("");
        instructions.Add("IMPORTANT: Follow these annotations exactly to ensure proper data processing and validation.");

        return string.Join("\n", instructions);
    }

    public string GenerateDataAnnotationExamples(AnnotationFile annotationFile, string dataType)
    {
        var examples = new List<string>
        {
            $"DATA ANNOTATION EXAMPLES FOR {annotationFile.Id.ToUpper()} ({dataType.ToUpper()} DATA):",
            ""
        };

        // Take first few annotations as examples
        var sampleAnnotations = annotationFile.Annotations.Take(3);

        examples.Add("Expected Data Structure:");
        examples.Add("{");
        foreach (var annotation in sampleAnnotations)
        {
            var sampleValue = GenerateSampleValue(annotation.FieldPath, annotation.DataType);
            examples.Add($"  \"{annotation.FieldPath}\": {sampleValue},");
        }
        examples.Add("}");
        examples.Add("");

        examples.Add("Processing Guidelines:");
        foreach (var annotation in sampleAnnotations)
        {
            examples.Add($"- {annotation.FieldPath}: {annotation.Description}");
        }

        return string.Join("\n", examples);
    }

    private List<string> ExtractFieldPaths(JsonElement element, string prefix = "")
    {
        var fields = new List<string>();

        foreach (var property in element.EnumerateObject())
        {
            var fieldPath = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
            fields.Add(fieldPath);

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                fields.AddRange(ExtractFieldPaths(property.Value, fieldPath));
            }
        }

        return fields;
    }

    private string GenerateSampleValue(string fieldPath, string dataType)
    {
        var lowerField = fieldPath.ToLower();
        var lowerType = dataType.ToLower();

        // Generate contextual sample values based on field name and data type
        if (lowerType.Contains("date")) return "\"2024-11-11\"";
        if (lowerType.Contains("number") || lowerType.Contains("decimal")) return "125.5";
        if (lowerType.Contains("integer") || lowerType.Contains("int")) return "100";
        if (lowerType.Contains("boolean") || lowerType.Contains("bool")) return "true";
        if (lowerField.Contains("volume") || lowerField.Contains("production")) return "1500.0";
        if (lowerField.Contains("rate")) return "125.5";
        if (lowerField.Contains("pressure")) return "2850.0";
        if (lowerField.Contains("temperature")) return "165.0";
        if (lowerField.Contains("name") || lowerField.Contains("company")) return "\"Example Company\"";
        if (lowerField.Contains("api")) return "\"42-123-45678\"";
        if (lowerField.Contains("id")) return "\"ITEM-001\"";
        if (lowerField.Contains("percentage") || lowerField.Contains("percent")) return "85.5";

        return lowerType.Contains("string") ? "\"sample_value\"" : "\"example\"";
    }
}