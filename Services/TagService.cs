using RealWorld.Data;
using RealWorld.Services.Interface;
using Microsoft.EntityFrameworkCore;
using RealWorld.Common;
using RealWorld.Models.DTOs.Tags;

namespace RealWorld.Services;

public class TagService(AppDbContext context) : ITagService
{

    public async Task<ServiceResult<TagListResponse>> GetTagsAsync()
    {
        List<string> tagList = await context.Tags
            .Select(t => t.TagText)
            .ToListAsync();
        TagListResponse response = new(tagList);

        return ServiceResult<TagListResponse>.Ok(response);
    }
}