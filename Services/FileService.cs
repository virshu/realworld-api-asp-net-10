namespace RealWorld.Services.Interface;

class FileService(IHttpContextService httpContextService) : IFileService
{
    public string? GetAbsoluteFileUrl(string? relativeUrl)
    {
        string baseUrl = httpContextService.GetBaseUrl();
        string? fullImageUrl = relativeUrl;
        if (!string.IsNullOrEmpty(relativeUrl) && relativeUrl.StartsWith("/"))
        {
            fullImageUrl = $"{baseUrl}{relativeUrl}";
        }

        return fullImageUrl;
    }

    public async Task<string> UploadAsync(Stream fileStream, string extension)
    {
        string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        if(!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        string uniqueFileName = $"{Guid.NewGuid()}{extension}";
        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

        await using(FileStream stream = new(filePath, FileMode.Create))
        {
            await fileStream.CopyToAsync(stream);
        }

        return $"/uploads/{uniqueFileName}";
    }
}