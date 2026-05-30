using RealWorld.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealWorld.Models.DTOs.Articles;
using RealWorld.Extensions;
using RealWorld.Common;

namespace RealWorld.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ArticlesController(
    IArticleService articleService
    ) : ApiControllerBase
{

    /// <summary>
    /// Returns a paginated list of articles, optionally filtered by the author, tags or its favorite status for the user
    /// </summary>
    /// <param name="query"></param>
    [AllowAnonymous]
    [HttpGet("")]
    public async Task<ActionResult> List([FromQuery] ArticleQueryParameters query)
    {
        ServiceResult<ArticleListResponse> result = await articleService.GetArticlesAsync(query, userId: User.GetOptionalUserId());
        return HandleResult(result);
    }

    /// <summary>
    /// Returns a paginated list of articles from authors
    /// the user is following, optionally filtered by the author, tags or its favorite status for the user
    /// </summary>
    /// <param name="query"></param>
    [HttpGet("feed")]
    public async Task<ActionResult> Feed([FromQuery] ArticleQueryParameters query)
    {
        ServiceResult<ArticleListResponse> result = await articleService.GetArticlesAsync(query, isFeed: true, userId: User.GetRequiredUserId());
        return HandleResult(result);
    }

    /// <summary>
    /// Returns an article based on the slug
    /// </summary>
    /// <param name="slug">Article slug</param>
    [AllowAnonymous]
    [HttpGet("{slug}")]
    public async Task<ActionResult> GetArticle(string slug)
    {
        ServiceResult<ArticleResponse?> result = await articleService.GetArticleBySlugAsync(slug, User.GetRequiredUserId());
        return HandleResult(result);
    }

    /// <summary>
    /// Creates a new article
    /// </summary>
    [HttpPost("")]
    public async Task<ActionResult> CreateArticle(CreateArticleRequest request)
    {
        ServiceResult<ArticleResponse> result = await articleService.CreateAsync(request.article, User.GetRequiredUserId());
        return HandleResult(result);
    }

    /// <summary>
    /// Updates an existing article
    /// </summary>
    [HttpPut("{slug}")]
    public async Task<ActionResult> UpdateArticle(string slug, UpdateArticleRequest request)
    {
        ServiceResult<ArticleResponse?> result = await articleService.UpdateAsync(slug, request.article, User.GetRequiredUserId());
        return HandleResult(result);
    }

    /// <summary>
    /// Deletes an article
    /// </summary>
    [HttpDelete("{slug}")]
    public async Task<ActionResult> DeleteArticle(string slug)
    {
        ServiceResult<bool> result = await articleService.DeleteAsync(slug, User.GetRequiredUserId());
        return HandleResult(result);
    }

    /// <summary>
    /// Favorites an article
    /// </summary>
    [HttpPost("{slug}/favorite")]
    public async Task<ActionResult> FavoriteArticle(string slug)
    {
        ServiceResult<ArticleResponse?> result = await articleService.FavoriteArticleAsync(slug, User.GetRequiredUserId());
        return HandleResult(result);
    }

    /// <summary>
    /// Unfavorites an article
    /// </summary>
    [HttpDelete("{slug}/favorite")]
    public async Task<ActionResult> UnfavoriteArticle(string slug)
    {
        ServiceResult<ArticleResponse?> result = await articleService.UnfavoriteArticleAsync(slug, User.GetRequiredUserId());
        return HandleResult(result);
    }
}