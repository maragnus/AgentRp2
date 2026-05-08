namespace AgentRp.Services;

public static class ImageContentTypeRules
{
    public static string FileExtensionFor(string contentType, string fileName = "") => contentType.ToLowerInvariant() switch
    {
        "image/avif" => ".avif",
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/webp" => ".webp",
        _ => SafeExtensionFromFileName(fileName)
    };

    static string SafeExtensionFromFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return string.IsNullOrWhiteSpace(extension) ? ".bin" : extension.ToLowerInvariant();
    }
}
