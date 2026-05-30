using RealWorld.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using RealWorld.Common;
using RealWorld.Models.DTOs.Tags;

namespace RealWorld.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TagsController(
    ITagService tagService
    ) : ApiControllerBase
{
    private readonly ITagService _tagService = tagService;

    /// <summary>
    /// Returns all tags, sorted alphabetically
    /// </summary>
    [HttpGet("")]
    public async Task<IActionResult> List()
    {
        ServiceResult<TagListResponse> result = await _tagService.GetTagsAsync();
        return HandleResult(result);
    }
}