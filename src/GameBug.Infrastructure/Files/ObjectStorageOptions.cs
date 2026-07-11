namespace GameBug.Infrastructure.Files;

public class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    public string Endpoint { get; set; } = null!;
    public string AccessKey { get; set; } = null!;
    public string SecretKey { get; set; } = null!;
    public string BucketName { get; set; } = "gamebug-attachments";
    public bool UseSsl { get; set; } = false;
    public int TimeoutSeconds { get; set; } = 30;
}
