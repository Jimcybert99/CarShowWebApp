using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CarShowJudging.Core.Interfaces;

namespace CarShowJudging.Infrastructure.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _client;
    private const string Container = "vehicle-photos";

    public BlobStorageService(BlobServiceClient client) => _client = client;

    public async Task<string> UploadAsync(Stream stream, string fileName, string contentType)
    {
        var container = _client.GetBlobContainerClient(Container);
        await container.CreateIfNotExistsAsync(PublicAccessType.Blob);

        var blob = container.GetBlobClient(fileName);
        await blob.UploadAsync(stream, new BlobHttpHeaders { ContentType = contentType });

        return blob.Uri.ToString();
    }

    public async Task DeleteAsync(string blobUrl)
    {
        var uri = new Uri(blobUrl);
        var blobName = string.Join("/", uri.Segments[2..]);
        var container = _client.GetBlobContainerClient(Container);
        await container.GetBlobClient(blobName).DeleteIfExistsAsync();
    }
}
