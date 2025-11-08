namespace SchemaHarmonizer.Models;

public class AnnotationFile
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty; // drilling, production, seismic, well
    public string Version { get; set; } = "1.0";
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public List<DataAnnotation> Annotations { get; set; } = new();
    public List<ProcessingRule> ProcessingRules { get; set; } = new();
}

public class DataAnnotation
{
    public string FieldPath { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string? DefaultValue { get; set; }
    public List<string> AllowedValues { get; set; } = new();
    public string? ValidationPattern { get; set; }
}

public class ProcessingRule
{
    public string RuleType { get; set; } = string.Empty; // transform, validate, normalize
    public string SourcePattern { get; set; } = string.Empty;
    public string TargetPattern { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}