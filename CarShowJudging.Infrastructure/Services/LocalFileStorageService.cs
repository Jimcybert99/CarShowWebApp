using CarShowJudging.Core.Interfaces;

namespace CarShowJudging.Infrastructure.Services;

public class LocalFileStorageService : IBlobStorageService
{
    private readonly string _basePath;
    private readonly string _baseUrl;

    public LocalFileStorageService(string basePath, string baseUrl)
    {
        _basePath = basePath;
        _baseUrl = baseUrl;
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> UploadAsync(Stream stream, string fileName, string contentType)
    {
        var ext = Path.GetExtension(fileName);
        var name = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(_basePath, name);
        using var file = File.Create(filePath);
        await stream.CopyToAsync(file);
        return $"{_baseUrl}/{name}";
    }

    public Task DeleteAsync(string url)
    {
        var name = Path.GetFileName(url);
        var filePath = Path.Combine(_basePath, name);
        if (File.Exists(filePath))
            File.Delete(filePath);
        return Task.CompletedTask;
    }
}
