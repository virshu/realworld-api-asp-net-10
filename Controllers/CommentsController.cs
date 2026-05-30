using RealWorld.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealWorld.Models.DTOs.Comments;
using RealWorld.Extensions;
using RealWorld.Common;

namespace RealWorld.Controllers;

[Route("api/articles")]
[ApiController]
[Authorize]
public class CommentsController(
    ICommentService commentService
    ) : ApiControllerBase
{
    private readonly ICommentService _commentService = commentService;

    /// <summary>
    /// Returns all comments for a given article
    /// </summary>
    /// <param name="slug">Article slug</param>
    [AllowAnonymous]
    [HttpGet("{slug}/comments")]
    public async Task<ActionResult> GetComments(string slug)
    {
        ServiceResult<CommentListResponse?> result = await _commentService.GetCommentsForArticleAsync(slug, User.GetOptionalUserId());
        return HandleResult(result);
    }

    /// <summary>
    /// Creates a new comment for a given article
    /// </summary>
    /// <param name="slug">Article slug</param>
    [HttpPost("{slug}/comments")]
    public async Task<ActionResult> CreateComment(CreateCommentRequest request, string slug)
    {
        ServiceResult<CommentResponse?> result = await _commentService.CreateAsync(request.comment, slug, User.GetRequiredUserId());
        return HandleResult(result);
    }

    /// <summary>
    /// Deletes a comment for a given article by its Id
    /// </summary>
    /// <param name="slug">Article slug</param>
    /// <param name="id">Comment id</param>
    [HttpDelete("{slug}/comments/{id}")]
    public async Task<ActionResult> DeleteComment(string slug, int id)
    {
        ServiceResult<bool> result = await _commentService.DeleteAsync(id, User.GetRequiredUserId());
        return HandleResult(result);
    }
}
