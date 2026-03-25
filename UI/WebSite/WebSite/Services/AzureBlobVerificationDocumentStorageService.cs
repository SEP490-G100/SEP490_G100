using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using WebSite.Models.Storage;

namespace WebSite.Services;

public interface IVerificationDocumentStorageService
{
    Task<string> UploadAsync(IFormFile file, int documentType, CancellationToken cancellationToken = default);
}

public class AzureBlobVerificationDocumentStorageService : IVerificationDocumentStorageService
{
    private readonly AzureBlobStorageOptions _options;

    public AzureBlobVerificationDocumentStorageService(IOptions<AzureBlobStorageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> UploadAsync(IFormFile file, int documentType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new InvalidOperationException("Azure Blob Storage chưa được cấu hình ConnectionString.");
        }

        if (string.IsNullOrWhiteSpace(_options.ContainerName))
        {
            throw new InvalidOperationException("Azure Blob Storage chưa được cấu hình ContainerName.");
        }

        var containerClient = new BlobContainerClient(_options.ConnectionString, _options.ContainerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var blobName = $"{GetFolderName(documentType)}/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid()}{fileExtension}";
        var blobClient = containerClient.GetBlobClient(blobName);

        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = GetContentType(file, fileExtension)
            }
        };

        await using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(stream, uploadOptions, cancellationToken);

        return blobClient.Uri.ToString();
    }

    private static string GetFolderName(int documentType) => documentType switch
    {
        1 => "identity-card",
        2 => "degree-certificate",
        4 => "health-certificate",
        _ => "other"
    };

    private static string GetContentType(IFormFile file, string fileExtension)
    {
        if (!string.IsNullOrWhiteSpace(file.ContentType) && file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return file.ContentType;
        }

        return fileExtension switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
