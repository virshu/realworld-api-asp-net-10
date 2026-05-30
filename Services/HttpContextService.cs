using System.Security.Claims;
using RealWorld.Services.Interface;

namespace RealWorld.Services;

class HttpContextService(
    IHttpContextAccessor httpContextAccessor
    ) : IHttpContextService
{

    public string GetBaseUrl()
    {
        HttpRequest request = httpContextAccessor.HttpContext!.Request;
        string baseUrl = $"{request.Scheme}://{request.Host}";

        return baseUrl;
    }

    public int? GetCurrentUserId()
    {
        string? userIdString = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if(userIdString == null)
        {
            return null;
        }
        int currentUserId = int.Parse(userIdString);

        return currentUserId;
    }
}