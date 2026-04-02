using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using WebSite.Enums;
using WebSite.Models.Storage;

namespace WebSite.Services;

public interface IVerificationDocumentStorageService
{
    Task<string> UploadAsync(IFormFile file, VerificationDocumentType documentType, CancellationToken cancellationToken = default);
}

public interface IBlogImageStorageService
{
    Task<IReadOnlyList<string>> UploadAsync(IEnumerable<IFormFile> files, CancellationToken cancellationToken = default);
}

public class AzureBlobStorageService : IVerificationDocumentStorageService, IBlogImageStorageService
{
    private readonly AzureBlobStorageOptions _options;

    public AzureBlobStorageService(IOptions<AzureBlobStorageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> UploadAsync(IFormFile file, VerificationDocumentType documentType, CancellationToken cancellationToken = default)
    {
        ValidateConnectionString();
        ValidateContainerName(_options.VerificationContainerName, "VerificationContainerName");

        var containerClient = await GetContainerClientAsync(_options.VerificationContainerName, cancellationToken);
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var blobName = $"{GetVerificationFolderName(documentType)}/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid()}{fileExtension}";

        return await UploadBlobAsync(containerClient, file, blobName, GetVerificationContentType(file, fileExtension), cancellationToken);
    }

    public async Task<IReadOnlyList<string>> UploadAsync(IEnumerable<IFormFile> files, CancellationToken cancellationToken = default)
    {
        ValidateConnectionString();
        ValidateContainerName(_options.BlogImageContainerName, "BlogImageContainerName");

        var uploadedUrls = new List<string>();
        var containerClient = await GetContainerClientAsync(_options.BlogImageContainerName, cancellationToken);

        foreach (var file in files.Where(static f => f.Length > 0))
        {
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!IsSupportedImage(file.ContentType, fileExtension))
            {
                throw new InvalidOperationException($"File '{file.FileName}' khong phai dinh dang anh hop le.");
            }

            var blobName = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid()}{fileExtension}";
            var contentType = GetBlogImageContentType(file.ContentType, fileExtension);
            var uploadedUrl = await UploadBlobAsync(containerClient, file, blobName, contentType, cancellationToken);
            uploadedUrls.Add(uploadedUrl);
        }

        return uploadedUrls;
    }

    private void ValidateConnectionString()
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new InvalidOperationException("Azure Blob Storage chua duoc cau hinh ConnectionString.");
        }
    }

    private static void ValidateContainerName(string? containerName, string optionName)
    {
        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new InvalidOperationException($"Azure Blob Storage chua duoc cau hinh {optionName}.");
        }
    }

    private async Task<BlobContainerClient> GetContainerClientAsync(string containerName, CancellationToken cancellationToken)
    {
        var containerClient = new BlobContainerClient(_options.ConnectionString, containerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
        return containerClient;
    }

    private static async Task<string> UploadBlobAsync(
        BlobContainerClient containerClient,
        IFormFile file,
        string blobName,
        string contentType,
        CancellationToken cancellationToken)
    {
        var blobClient = containerClient.GetBlobClient(blobName);
        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType
            }
        };

        await using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(stream, uploadOptions, cancellationToken);
        return blobClient.Uri.ToString();
    }

    private static string GetVerificationFolderName(VerificationDocumentType documentType) => documentType switch
    {
        VerificationDocumentType.IdentityCard => "identity-card",
        VerificationDocumentType.DegreeCertificate => "degree-certificate",
        VerificationDocumentType.HealthCertificate => "health-certificate",
        _ => "other"
    };

    private static string GetVerificationContentType(IFormFile file, string fileExtension)
    {
        if (!string.IsNullOrWhiteSpace(file.ContentType) &&
            file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return file.ContentType;
        }

        return fileExtension switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }

    private static bool IsSupportedImage(string? contentType, string fileExtension)
    {
        if (!string.IsNullOrWhiteSpace(contentType) &&
            contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return fileExtension is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif";
    }

    private static string GetBlogImageContentType(string? contentType, string fileExtension)
    {
        if (!string.IsNullOrWhiteSpace(contentType) &&
            contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return contentType;
        }

        return fileExtension switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };
    }
}
