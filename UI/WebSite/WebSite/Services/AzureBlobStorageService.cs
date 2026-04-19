using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using WebSite.Enums;
using WebSite.Models.Storage;

namespace WebSite.Services;

public interface IAzureBlobStorageService
{
    Task<string> UploadVerificationDocumentAsync(IFormFile file, VerificationDocumentType documentType, CancellationToken cancellationToken = default);
    Task<string> UploadUserAvatarAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> UploadMediaAsync(
        IEnumerable<IFormFile> files,
        BlobStorageContainerKind containerKind,
        BlobMediaType mediaType,
        CancellationToken cancellationToken = default);
}

public enum BlobStorageContainerKind
{
    BlogMedia = 1,
    ReportMedia = 2
}

public enum BlobMediaType
{
    Image = 1,
    Video = 2
}

public class AzureBlobStorageService : IAzureBlobStorageService
{
    private readonly AzureBlobStorageOptions _options;

    public AzureBlobStorageService(IOptions<AzureBlobStorageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> UploadVerificationDocumentAsync(IFormFile file, VerificationDocumentType documentType, CancellationToken cancellationToken = default)
    {
        ValidateConnectionString();
        ValidateContainerName(_options.VerificationContainerName, "VerificationContainerName");

        var containerClient = await GetContainerClientAsync(_options.VerificationContainerName, cancellationToken);
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var blobName = $"{GetVerificationFolderName(documentType)}/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid()}{fileExtension}";

        return await UploadBlobAsync(containerClient, file, blobName, GetVerificationContentType(file, fileExtension), cancellationToken);
    }

    public async Task<string> UploadUserAvatarAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        ValidateConnectionString();
        ValidateContainerName(_options.UserAvatarContainerName, "UserAvatarContainerName");

        if (file == null || file.Length == 0)
            throw new InvalidOperationException("File avatar khong hop le.");

        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!IsSupportedImage(file.ContentType, fileExtension))
            throw new InvalidOperationException("Chi ho tro upload avatar dinh dang anh hop le.");

        var containerClient = await GetContainerClientAsync(_options.UserAvatarContainerName, cancellationToken);
        var blobName = $"user-avatar/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid()}{fileExtension}";
        return await UploadBlobAsync(containerClient, file, blobName, GetImageContentType(file.ContentType, fileExtension), cancellationToken);
    }

    public async Task<IReadOnlyList<string>> UploadMediaAsync(
        IEnumerable<IFormFile> files,
        BlobStorageContainerKind containerKind,
        BlobMediaType mediaType,
        CancellationToken cancellationToken = default)
    {
        ValidateConnectionString();
        var containerName = GetMediaContainerName(containerKind);
        ValidateContainerName(containerName, $"{containerKind}ContainerName");

        var uploadedUrls = new List<string>();
        var containerClient = await GetContainerClientAsync(containerName, cancellationToken);

        foreach (var file in files.Where(static f => f.Length > 0))
        {
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var isValidType = mediaType == BlobMediaType.Video
                ? IsSupportedVideo(file.ContentType, fileExtension)
                : IsSupportedImage(file.ContentType, fileExtension);
            if (!isValidType)
            {
                var mediaLabel = mediaType == BlobMediaType.Video ? "video" : "anh";
                throw new InvalidOperationException($"File '{file.FileName}' khong phai dinh dang {mediaLabel} hop le.");
            }

            var mediaFolder = mediaType == BlobMediaType.Video ? "video" : "image";
            var blobName = $"{mediaFolder}/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid()}{fileExtension}";
            var contentType = mediaType == BlobMediaType.Video
                ? GetBlogVideoContentType(file.ContentType, fileExtension)
                : GetBlogImageContentType(file.ContentType, fileExtension);
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

    private string GetMediaContainerName(BlobStorageContainerKind containerKind)
        => containerKind switch
        {
            BlobStorageContainerKind.BlogMedia => _options.BlogMediaContainerName,
            BlobStorageContainerKind.ReportMedia => _options.ReportMediaContainerName,
            _ => throw new InvalidOperationException("Loai container media khong hop le.")
        };

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

    private static string GetImageContentType(string? contentType, string fileExtension)
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

    private static string GetBlogImageContentType(string? contentType, string fileExtension) =>
        GetImageContentType(contentType, fileExtension);

    private static bool IsSupportedVideo(string? contentType, string fileExtension)
    {
        if (!string.IsNullOrWhiteSpace(contentType) &&
            contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return fileExtension is ".mp4" or ".webm" or ".ogg" or ".mov";
    }

    private static string GetBlogVideoContentType(string? contentType, string fileExtension)
    {
        if (!string.IsNullOrWhiteSpace(contentType) &&
            contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return contentType;
        }

        return fileExtension switch
        {
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".ogg" => "video/ogg",
            ".mov" => "video/quicktime",
            _ => "application/octet-stream"
        };
    }
}
