using System.Security.Claims;

namespace RealWorld.Extensions;

public static class ClaimsPrincipalExtensions
{
    extension(ClaimsPrincipal user)
    {
        // For [Authorize] endpoints (Throws if missing)
        public int GetRequiredUserId()
        {
            int? id = user.GetOptionalUserId();
            return id ?? throw new UnauthorizedAccessException("User ID not found.");
        }

        // For [AllowAnonymous] endpoints (Returns null if missing)
        public int? GetOptionalUserId()
        {
            // If the user isn't logged in, ClaimsPrincipal is empty.
            Claim? claim = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("id");
        
            if (claim != null && int.TryParse(claim.Value, out int userId))
            {
                return userId;
            }

            return null;
        }
    }
}