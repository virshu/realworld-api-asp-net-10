using RealWorld.Data;
using RealWorld.Models.Entities;
using RealWorld.Services.Interface;
using Microsoft.EntityFrameworkCore;
using RealWorld.Models.DTOs.Comments;
using Mapster;
using RealWorld.Common;

namespace RealWorld.Services;

public class CommentService(AppDbContext context) : ICommentService
{

    public async Task<ServiceResult<CommentListResponse?>> GetCommentsForArticleAsync(string slug, int? userId)
    {
        Article? article = await context.Articles
            .Include(a => a.Comments)
                .ThenInclude(c => c.Author)
            .FirstOrDefaultAsync(a => a.Slug == slug);

        if(article == null)
        {
            return ServiceResult<CommentListResponse?>.NotFound($"Article with slug '{slug}' was not found.");
        }

        List<int> commenterIds = article.Comments.Select(c => c.Author.Id).Distinct().ToList();

        if(userId.HasValue)
        {
            List<int> followedList = await context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .SelectMany(u => u.Following)
                .Where(f => commenterIds.Contains(f.Id))
                .Select(f => f.Id)
                .ToListAsync();
            HashSet<int> followedAuthorIds = [.. followedList];

            IEnumerable<CommentDto> commentListWithFollowing = article.Comments.Select(c =>
                CommentDtoFactory(c, followedAuthorIds.Contains(c.Author.Id))
            );
            CommentListResponse responseFollowing = new(commentListWithFollowing);

            return ServiceResult<CommentListResponse?>.Ok(responseFollowing);
        }

        IEnumerable<CommentDto> commentList = article.Comments.Select(c => CommentDtoFactory(c, false));
        CommentListResponse response = new(commentList);

        return ServiceResult<CommentListResponse?>.Ok(response);
    }

    public async Task<ServiceResult<CommentResponse?>> CreateAsync(CreateCommentDto dto, string slug, int userId)
    {
        Article? article = await context.Articles.FirstOrDefaultAsync(a => a.Slug == slug);
        if(article == null)
        {
            return ServiceResult<CommentResponse?>.NotFound("Comment not found.");
        }

        Comment newComment = new()
        {
            Body = dto.Body,
            AuthorId = userId,
            ArticleId = article.Id
        };
        context.Comments.Add(newComment);

        await context.SaveChangesAsync();
        await context.Entry(newComment).Reference(c => c.Author).LoadAsync();

        CommentDto commentDto = CommentDtoFactory(newComment, false);
        CommentResponse response = new(commentDto);

        return ServiceResult<CommentResponse?>.Ok(response);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id, int userId)
    {
        Comment? comment = await context.Comments
            .FirstOrDefaultAsync(c => c.Id == id);
        if (comment == null)
        {
            return ServiceResult<bool>.NotFound("Comment not found.");
        }
        if (comment.AuthorId != userId)
        {
            return ServiceResult<bool>.Unauthorized("You do not have permission to delete this article.");
        }

        await context.Comments
            .Where(a => a.Id == id)
            .ExecuteDeleteAsync();

        return ServiceResult<bool>.Ok(true);
    }

    private CommentDto CommentDtoFactory(Comment comment, bool isFollowing)
    {
        CommentDto c = comment.Adapt<CommentDto>();
        c.Author.Following = isFollowing;

        return c;
    }
}