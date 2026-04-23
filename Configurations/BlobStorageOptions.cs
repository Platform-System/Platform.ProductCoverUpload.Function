namespace Platform.ProductCoverUpload.Function.Configurations;

public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public int MaxFileSizeInMb { get; set; } = 10;
}
