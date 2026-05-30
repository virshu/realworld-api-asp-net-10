using RealWorld.Data;
using RealWorld.Models.Entities;
using RealWorld.Services.Interface;
using Microsoft.EntityFrameworkCore;
using RealWorld.Models.DTOs.Profiles;
using Mapster;
using RealWorld.Common;

namespace RealWorld.Services;

public class ProfileService(
    AppDbContext context,
    IFileService fileService
    ) : IProfileService 
{
    public async Task<ServiceResult<ProfileResponse?>> GetProfileByUsernameAsync(string username, int? userId)
    {
        User? user = await context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if(user == null)
        {
            return ServiceResult<ProfileResponse?>.NotFound($"User with ${username} not found.");
        }

        bool isFollowingUser = false;
        if(userId != null)
        {
            User? currentUser = await context.Users
                .Include(u => u.Following)
                .FirstOrDefaultAsync(u => u.Id == userId);
            isFollowingUser = currentUser!.Following.Any(u => u.Username == user.Username);
        }

        ProfileDto profileDto = ProfileDtoFactory(user, isFollowingUser);
        ProfileResponse response = new(profileDto);

        return ServiceResult<ProfileResponse?>.Ok(response);
    }

    public async Task<ServiceResult<ProfileResponse?>> FollowUserAsync(string username, int userId)
    {
        User? currentUser = await context.Users
            .Include(u => u.Following)
            .FirstOrDefaultAsync(u => u.Id == userId);

        User? userToFollow = await context.Users
            .Where(u => u.Username == username)
            .FirstOrDefaultAsync();

        if(userToFollow == null)
        {
            return ServiceResult<ProfileResponse?>.NotFound($"User with ${username} not found.");
        }

        if(currentUser!.Following.All(u => u.Username != userToFollow.Username))
        {
            currentUser.Following.Add(userToFollow);
            await context.SaveChangesAsync();
        }

        ProfileDto profileDto = ProfileDtoFactory(userToFollow, true);
        ProfileResponse response = new(profileDto);

        return ServiceResult<ProfileResponse?>.Ok(response);
    }

    public async Task<ServiceResult<ProfileResponse?>> UnfollowUserAsync(string username, int userId)
    {
        User? currentUser = await context.Users
            .Include(u => u.Following)
            .FirstOrDefaultAsync(u => u.Id == userId);
        User? userToUnfollow = await context.Users
            .Where(u => u.Username == username)
            .FirstOrDefaultAsync();

        if(userToUnfollow == null)
        {
            return ServiceResult<ProfileResponse?>.NotFound($"User with ${username} not found.");
        }

        if(currentUser!.Following.Any(u => u.Username == userToUnfollow.Username))
        {
            currentUser.Following.Remove(userToUnfollow);
            await context.SaveChangesAsync();
        }

        ProfileDto profileDto = ProfileDtoFactory(userToUnfollow, false);
        ProfileResponse response = new(profileDto);

        return ServiceResult<ProfileResponse?>.Ok(response);
    }

    private ProfileDto ProfileDtoFactory(User user, bool isFollowingUser)
    {
        ProfileDto profile = user.Adapt<ProfileDto>();
        string? profileImageUrl = fileService.GetAbsoluteFileUrl(user.Image);

        profile.Image = profileImageUrl;
        profile.Following = isFollowingUser;

        return profile;
    }
}