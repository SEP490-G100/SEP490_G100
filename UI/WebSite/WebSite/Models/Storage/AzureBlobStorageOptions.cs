namespace WebSite.Models.Storage;

public class AzureBlobStorageOptions
{
    public const string SectionName = "AzureBlobStorage";

    public string ConnectionString { get; set; } = string.Empty;
    public string VerificationContainerName { get; set; } = "verification-documents";
    public string BlogMediaContainerName { get; set; } = "blog-media";
}
