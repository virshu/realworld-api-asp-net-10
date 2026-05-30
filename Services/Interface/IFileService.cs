namespace RealWorld.Services.Interface;

public interface IFileService
{
    Task<string> UploadAsync(Stream fileStream, string extension);
    public string? GetAbsoluteFileUrl(string? relativeUrl);
}