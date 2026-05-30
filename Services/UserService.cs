using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using RealWorld.Data;
using RealWorld.Models.Entities;
using RealWorld.Services.Interface;
using Microsoft.EntityFrameworkCore;
using RealWorld.Models.DTOs.Auth;
using Mapster;
using RealWorld.Common;

namespace RealWorld.Services;

public class UserService(
    AppDbContext context,
    IJwtService jwtService,
    IFileService fileService
    ) : IUserService
{

    public async Task<ServiceResult<UserResponse?>> LoginAsync(LoginDto dto)
    {
        User? user = await context.Users
            .FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.Password))
            return ServiceResult<UserResponse?>.Unauthorized("Invalid credentials");

        string accessToken = jwtService.GenerateAccessToken(user);
        (string rawRefreshToken, string hashedRefreshToken) = GenerateRefreshToken();

        // Each login = new family (new device/session)
        context.RefreshTokens.Add(new()
        {
            UserId = user.Id,
            Token = hashedRefreshToken,
            Family = Guid.NewGuid().ToString(),
            ExpiryTime = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        UserDto userDto = await UserDtoFactory(user, accessToken, rawRefreshToken);
        return ServiceResult<UserResponse?>.Ok(new(userDto));
    }

    public async Task<ServiceResult<UserResponse>> RegisterAsync(RegisterDto dto)
    {
        User user = new()
        {
            Username = dto.Username,
            Email = dto.Email,
            Password = BCrypt.Net.BCrypt.HashPassword(dto.Password)
        };

        context.Users.Add(user);
        await context.SaveChangesAsync(); // Save first to get user.Id

        string accessToken = jwtService.GenerateAccessToken(user);
        (string rawRefreshToken, string hashedRefreshToken) = GenerateRefreshToken();

        context.RefreshTokens.Add(new()
        {
            UserId = user.Id,
            Token = hashedRefreshToken,
            Family = Guid.NewGuid().ToString(),
            ExpiryTime = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        UserDto userDto = await UserDtoFactory(user, accessToken, rawRefreshToken);
        return ServiceResult<UserResponse>.Ok(new(userDto));
    }

    public async Task<ServiceResult<bool>> LogoutAsync(int userId, string rawRefreshToken)
    {
        string hashed = HashToken(rawRefreshToken);

        RefreshToken? token = await context.RefreshTokens
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == hashed);

        if (token == null)
            return ServiceResult<bool>.Unauthorized();

        // Only revoke this session, not all devices
        token.IsRevoked = true;
        await context.SaveChangesAsync();

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<UserResponse?>> RefreshAsync(TokenRequest request)
    {
        ClaimsPrincipal principal = jwtService.GetPrincipalFromExpiredToken(request.AccessToken);
        string? userIdString = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdString))
            return ServiceResult<UserResponse?>.BadRequest("Invalid access token.");

        string hashed = HashToken(request.RefreshToken);
        RefreshToken? stored = await context.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == hashed);

        if (stored == null)
            return ServiceResult<UserResponse?>.BadRequest("Invalid refresh token.");

        // Reuse detected — token was already rotated, family is compromised
        if (stored.IsRevoked)
        {
            await RevokeFamilyAsync(stored.Family);
            return ServiceResult<UserResponse?>.BadRequest(
                "Token reuse detected. Please log in again."
            );
        }

        if (stored.ExpiryTime <= DateTime.UtcNow)
            return ServiceResult<UserResponse?>.BadRequest("Refresh token expired.");

        // Rotate — revoke old, issue new in same family
        stored.IsRevoked = true;

        (string newRawToken, string newHashedToken) = GenerateRefreshToken();
        context.RefreshTokens.Add(new()
        {
            UserId = stored.UserId,
            Token = newHashedToken,
            Family = stored.Family, // same family, keeps chain trackable
            ExpiryTime = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        string newAccessToken = jwtService.GenerateAccessToken(stored.User);
        UserDto userDto = await UserDtoFactory(stored.User, newAccessToken, newRawToken);
        return ServiceResult<UserResponse?>.Ok(new(userDto));
    }

    public async Task<ServiceResult<UserResponse?>> GetCurrentUserAsync(string currentToken, int userId)
    {
        User? user = await context.Users.FindAsync(userId);
        if (user == null)
            return ServiceResult<UserResponse?>.NotFound();

        UserDto userDto = await UserDtoFactory(user, currentToken);
        return ServiceResult<UserResponse?>.Ok(new(userDto));
    }

    public async Task<ServiceResult<UserResponse?>> UpdateUserAsync(UpdateUserDto dto, int userId)
    {
        User? user = await context.Users.FindAsync(userId);
        if (user == null)
            return ServiceResult<UserResponse?>.NotFound();

        if (!string.IsNullOrEmpty(dto.Email) && dto.Email != user.Email)
        {
            if (await context.Users.AnyAsync(u => u.Email == dto.Email))
                return ServiceResult<UserResponse?>.Fail("Email has already been taken.");
            user.Email = dto.Email;
        }

        if (!string.IsNullOrEmpty(dto.Username) && dto.Username != user.Username)
        {
            if (await context.Users.AnyAsync(u => u.Username == dto.Username))
                return ServiceResult<UserResponse?>.Fail("Username has already been taken.");
            user.Username = dto.Username;
        }

        if (!string.IsNullOrEmpty(dto.Password))
            user.Password = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        if (dto.Bio != null) user.Bio = dto.Bio;
        if (dto.Image != null) user.Image = dto.Image;

        await context.SaveChangesAsync();

        string newAccessToken = jwtService.GenerateAccessToken(user);
        UserDto userDto = await UserDtoFactory(user, newAccessToken);
        return ServiceResult<UserResponse?>.Ok(new(userDto));
    }

    // --- Private helpers ---

    private async Task RevokeFamilyAsync(string family)
    {
        List<RefreshToken> familyTokens = await context.RefreshTokens
            .Where(t => t.Family == family && !t.IsRevoked)
            .ToListAsync();

        foreach (RefreshToken t in familyTokens)
            t.IsRevoked = true;

        await context.SaveChangesAsync();
    }

    private static (string raw, string hashed) GenerateRefreshToken()
    {
        string raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return (raw, HashToken(raw));
    }

    private static string HashToken(string token)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    private async Task<UserDto> UserDtoFactory(
        User user,
        string accessToken,
        string refreshToken = ""
    )
    {
        string? fullImageUrl = fileService.GetAbsoluteFileUrl(user.Image);
        UserDto userDto = user.Adapt<UserDto>();
        userDto.Token = accessToken;
        userDto.RefreshToken = refreshToken == "" ? string.Empty : refreshToken;
        userDto.Image = fullImageUrl;
        return userDto;
    }
}