namespace SchemaHarmonizer.Services;

public interface IFileService
{
    Task<string?> BrowseForFileAsync(string filter = "JSON Files|*.json");
    Task<string?> ReadFileAsync(string filePath);
    Task<List<string>> GetAvailableFilesAsync(string directory, string pattern = "*.json");
    Task<List<string>> GetAnnotationFilesAsync();
}

public class FileService : IFileService
{
    private readonly ILogger<FileService> _logger;

    public FileService(ILogger<FileService> logger)
    {
        _logger = logger;
    }

    public async Task<string?> BrowseForFileAsync(string filter = "JSON Files|*.json")
    {
        // Since we're running on a server, we'll need to implement a different approach
        // For now, let's return a sample path from the project's sample-data
        await Task.Delay(100); // Simulate async operation

        // In a real implementation, you might:
        // 1. Show a modal with file explorer
        // 2. Use a file upload component
        // 3. Provide a text input for the user to enter the path

        return null; // Will be implemented with a modal file browser
    }

    public async Task<string?> ReadFileAsync(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                return await File.ReadAllTextAsync(filePath);
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file: {FilePath}", filePath);
            return null;
        }
    }

    public async Task<List<string>> GetAvailableFilesAsync(string directory, string pattern = "*.json")
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                _logger.LogWarning("Directory does not exist: {Directory}", directory);
                return new List<string>();
            }

            var files = Directory.GetFiles(directory, pattern, SearchOption.AllDirectories);
            return await Task.FromResult(files.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting files from directory: {Directory}", directory);
            return new List<string>();
        }
    }

    public async Task<List<string>> GetAnnotationFilesAsync()
    {
        try
        {
            // Look for annotation files in the annotations directory at project root
            var projectRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));
            var annotationDirectory = Path.Combine(projectRoot, "annotations");

            if (!Directory.Exists(annotationDirectory))
            {
                _logger.LogInformation("Annotation directory does not exist: {Directory}", annotationDirectory);
                return new List<string>();
            }

            var annotationFiles = Directory.GetFiles(annotationDirectory, "*.json", SearchOption.AllDirectories);
            return await Task.FromResult(annotationFiles.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading annotation files");
            return new List<string>();
        }
    }
}