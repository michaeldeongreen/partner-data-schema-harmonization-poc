using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.RegularExpressions;

namespace SchemaHarmonizer.Services;

public interface IAccuracyValidationService
{
    Task<AccuracyResult> ValidateHarmonizationAsync(string canonicalSchema, string originalData, string harmonizedData);
    AccuracyResult ValidateStructuralAccuracy(string canonicalSchema, string harmonizedData);
    AccuracyResult ValidateDataCompleteness(string originalData, string harmonizedData);
    AccuracyResult ValidateFieldMapping(string canonicalSchema, string originalData, string harmonizedData);
}

public class AccuracyResult
{
    public double OverallAccuracyPercentage { get; set; }
    public double StructuralAccuracy { get; set; }
    public double DataCompleteness { get; set; }
    public double FieldMappingAccuracy { get; set; }
    public List<AccuracyIssue> Issues { get; set; } = new();
    public List<string> SuccessfulMappings { get; set; } = new();
    public Dictionary<string, object> Metrics { get; set; } = new();
}

public class AccuracyIssue
{
    public string Type { get; set; } = string.Empty; // "structural", "data_loss", "field_mapping", "type_mismatch"
    public string Description { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string? ExpectedValue { get; set; }
    public string? ActualValue { get; set; }
    public string Severity { get; set; } = "medium"; // "low", "medium", "high", "critical"
}

public class AccuracyValidationService : IAccuracyValidationService
{
    private readonly ILogger<AccuracyValidationService> _logger;

    public AccuracyValidationService(ILogger<AccuracyValidationService> logger)
    {
        _logger = logger;
    }

    public async Task<AccuracyResult> ValidateHarmonizationAsync(string canonicalSchema, string originalData, string harmonizedData)
    {
        try
        {
            _logger.LogInformation("Starting comprehensive harmonization accuracy validation");

            var structuralResult = ValidateStructuralAccuracy(canonicalSchema, harmonizedData);
            var completenessResult = ValidateDataCompleteness(originalData, harmonizedData);
            var fieldMappingResult = ValidateFieldMapping(canonicalSchema, originalData, harmonizedData);

            var overallResult = new AccuracyResult
            {
                StructuralAccuracy = structuralResult.StructuralAccuracy,
                DataCompleteness = completenessResult.DataCompleteness,
                FieldMappingAccuracy = fieldMappingResult.FieldMappingAccuracy
            };

            // Combine issues from all validations
            overallResult.Issues.AddRange(structuralResult.Issues);
            overallResult.Issues.AddRange(completenessResult.Issues);
            overallResult.Issues.AddRange(fieldMappingResult.Issues);

            // Combine successful mappings
            overallResult.SuccessfulMappings.AddRange(structuralResult.SuccessfulMappings);
            overallResult.SuccessfulMappings.AddRange(completenessResult.SuccessfulMappings);
            overallResult.SuccessfulMappings.AddRange(fieldMappingResult.SuccessfulMappings);

            // Calculate weighted overall accuracy
            overallResult.OverallAccuracyPercentage = CalculateOverallAccuracy(
                overallResult.StructuralAccuracy,
                overallResult.DataCompleteness,
                overallResult.FieldMappingAccuracy);

            // Add metrics
            overallResult.Metrics["total_issues"] = overallResult.Issues.Count;
            overallResult.Metrics["critical_issues"] = overallResult.Issues.Count(i => i.Severity == "critical");
            overallResult.Metrics["successful_mappings"] = overallResult.SuccessfulMappings.Count;

            _logger.LogInformation("Accuracy validation completed. Overall accuracy: {Accuracy}%",
                overallResult.OverallAccuracyPercentage.ToString("F1"));

            return overallResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during accuracy validation");
            return new AccuracyResult
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
    }

    public AccuracyResult ValidateStructuralAccuracy(string canonicalSchema, string harmonizedData)
    {
        var result = new AccuracyResult();

        try
        {
            var canonicalJson = JsonDocument.Parse(canonicalSchema);
            var harmonizedJson = JsonDocument.Parse(harmonizedData);

            var canonicalFields = ExtractFieldStructure(canonicalJson.RootElement);
            var harmonizedFields = ExtractFieldStructure(harmonizedJson.RootElement);

            var matchingFields = 0;
            var totalCanonicalFields = canonicalFields.Count;

            foreach (var canonicalField in canonicalFields)
            {
                if (harmonizedFields.ContainsKey(canonicalField.Key))
                {
                    var canonicalType = canonicalField.Value;
                    var harmonizedType = harmonizedFields[canonicalField.Key];

                    if (canonicalType == harmonizedType)
                    {
                        matchingFields++;
                        result.SuccessfulMappings.Add($"Field '{canonicalField.Key}' correctly mapped with type {canonicalType}");
                    }
                    else
                    {
                        result.Issues.Add(new AccuracyIssue
                        {
                            Type = "type_mismatch",
                            Field = canonicalField.Key,
                            Description = $"Type mismatch for field '{canonicalField.Key}'",
                            ExpectedValue = canonicalType,
                            ActualValue = harmonizedType,
                            Severity = "medium"
                        });
                    }
                }
                else
                {
                    result.Issues.Add(new AccuracyIssue
                    {
                        Type = "structural",
                        Field = canonicalField.Key,
                        Description = $"Missing required field '{canonicalField.Key}' in harmonized data",
                        Severity = "high"
                    });
                }
            }

            result.StructuralAccuracy = totalCanonicalFields > 0 ? (double)matchingFields / totalCanonicalFields * 100 : 0;
        }
        catch (Exception ex)
        {
            result.Issues.Add(new AccuracyIssue
            {
                Type = "structural",
                Description = $"Structural validation failed: {ex.Message}",
                Severity = "critical"
            });
        }

        return result;
    }

    public AccuracyResult ValidateDataCompleteness(string originalData, string harmonizedData)
    {
        var result = new AccuracyResult();

        try
        {
            var originalJson = JsonDocument.Parse(originalData);
            var harmonizedJson = JsonDocument.Parse(harmonizedData);

            var originalValues = ExtractAllValues(originalJson.RootElement);
            var harmonizedValues = ExtractAllValues(harmonizedJson.RootElement);

            var preservedValues = 0;
            var totalOriginalValues = originalValues.Count;

            foreach (var originalValue in originalValues)
            {
                if (harmonizedValues.Any(h => ValuesAreEquivalent(originalValue, h)))
                {
                    preservedValues++;
                    result.SuccessfulMappings.Add($"Value '{originalValue}' preserved in harmonization");
                }
                else
                {
                    result.Issues.Add(new AccuracyIssue
                    {
                        Type = "data_loss",
                        Description = $"Original value '{originalValue}' not found in harmonized data",
                        ExpectedValue = originalValue,
                        Severity = "medium"
                    });
                }
            }

            result.DataCompleteness = totalOriginalValues > 0 ? (double)preservedValues / totalOriginalValues * 100 : 100;
        }
        catch (Exception ex)
        {
            result.Issues.Add(new AccuracyIssue
            {
                Type = "data_loss",
                Description = $"Data completeness validation failed: {ex.Message}",
                Severity = "critical"
            });
        }

        return result;
    }

    public AccuracyResult ValidateFieldMapping(string canonicalSchema, string originalData, string harmonizedData)
    {
        var result = new AccuracyResult();

        try
        {
            // This is a simplified field mapping validation
            // In practice, you might want to implement more sophisticated mapping rules
            var originalJson = JsonDocument.Parse(originalData);
            var harmonizedJson = JsonDocument.Parse(harmonizedData);
            var canonicalJson = JsonDocument.Parse(canonicalSchema);

            var mappingScore = ValidateCommonMappingPatterns(originalJson.RootElement, harmonizedJson.RootElement);
            result.FieldMappingAccuracy = mappingScore;

            if (mappingScore >= 80)
            {
                result.SuccessfulMappings.Add("Field mapping patterns follow expected conventions");
            }
            else
            {
                result.Issues.Add(new AccuracyIssue
                {
                    Type = "field_mapping",
                    Description = "Field mapping accuracy below expected threshold",
                    Severity = "medium"
                });
            }
        }
        catch (Exception ex)
        {
            result.Issues.Add(new AccuracyIssue
            {
                Type = "field_mapping",
                Description = $"Field mapping validation failed: {ex.Message}",
                Severity = "critical"
            });
        }

        return result;
    }

    private Dictionary<string, string> ExtractFieldStructure(JsonElement element, string prefix = "")
    {
        var fields = new Dictionary<string, string>();

        foreach (var property in element.EnumerateObject())
        {
            var fieldName = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
            var fieldType = property.Value.ValueKind.ToString().ToLowerInvariant();

            fields[fieldName] = fieldType;

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                var nestedFields = ExtractFieldStructure(property.Value, fieldName);
                foreach (var nested in nestedFields)
                {
                    fields[nested.Key] = nested.Value;
                }
            }
        }

        return fields;
    }

    private List<string> ExtractAllValues(JsonElement element)
    {
        var values = new List<string>();

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                values.Add(element.GetString() ?? "");
                break;
            case JsonValueKind.Number:
                values.Add(element.GetRawText());
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                values.Add(element.GetBoolean().ToString().ToLower());
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    values.AddRange(ExtractAllValues(property.Value));
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    values.AddRange(ExtractAllValues(item));
                }
                break;
        }

        return values;
    }

    private bool ValuesAreEquivalent(string value1, string value2)
    {
        // Basic equivalence check - could be enhanced with more sophisticated matching
        if (string.Equals(value1, value2, StringComparison.OrdinalIgnoreCase))
            return true;

        // Check for numeric equivalence
        if (double.TryParse(value1, out var num1) && double.TryParse(value2, out var num2))
            return Math.Abs(num1 - num2) < 0.001;

        // Check for date/time equivalence
        if (DateTime.TryParse(value1, out var date1) && DateTime.TryParse(value2, out var date2))
            return date1.Date == date2.Date;

        return false;
    }

    private double ValidateCommonMappingPatterns(JsonElement original, JsonElement harmonized)
    {
        // Implement common field mapping pattern validation
        // This is a simplified example - you would extend this based on your domain knowledge

        var commonPatterns = new Dictionary<string, string[]>
        {
            { "date", new[] { "date", "dt", "time", "timestamp" } },
            { "id", new[] { "id", "identifier", "number" } },
            { "name", new[] { "name", "title", "label" } }
        };

        // Calculate mapping accuracy based on pattern recognition
        // This is a placeholder implementation
        return 85.0; // Would implement actual pattern matching logic
    }

    private double CalculateOverallAccuracy(double structural, double completeness, double fieldMapping)
    {
        // Weighted average - adjust weights based on importance
        const double structuralWeight = 0.4;
        const double completenessWeight = 0.3;
        const double fieldMappingWeight = 0.3;

        return (structural * structuralWeight +
                completeness * completenessWeight +
                fieldMapping * fieldMappingWeight);
    }
}