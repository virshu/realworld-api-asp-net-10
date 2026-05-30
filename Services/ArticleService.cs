using RealWorld.Data;
using RealWorld.Models.Entities;
using RealWorld.Services.Interface;
using Microsoft.EntityFrameworkCore;
using RealWorld.Models.DTOs.Articles;
using Mapster;
using RealWorld.Common;

namespace RealWorld.Services;


public class ArticleService(
    AppDbContext context,
    IFileService fileService   
    ) : IArticleService {

    public async Task<ServiceResult<ArticleListResponse>> GetArticlesAsync(
        ArticleQueryParameters query, 
        bool isFeed = false,
        int? userId = null
    )
    {
        IQueryable<Article> articlesQuery = context.Articles
            .Include(a => a.Author)
                .ThenInclude(a => a.Followers)
            .Include(a => a.TagList)
            .Include(a => a.FavoritedBy)
            .AsQueryable();

        if(!string.IsNullOrEmpty(query.Author)) 
        {
            articlesQuery = articlesQuery.Where(a => a.Author.Username == query.Author);
        }
        if(!string.IsNullOrEmpty(query.Tag))
        {
            articlesQuery = articlesQuery.Where(a => a.TagList.Any(t => t.TagText == query.Tag));
        }
        if(!string.IsNullOrWhiteSpace(query.Favorited))
        {
            articlesQuery = articlesQuery.Where(a => a.FavoritedBy.Any(f => f.Username == query.Favorited));
        }
        if(isFeed)
        {
            if(userId != null)
            {
                articlesQuery = articlesQuery.Where(
                    a => a.Author.Followers.Any(f => f.Id == userId)
                );
            }
            
        }

        int articleCount = await articlesQuery.CountAsync();
        List<Article> articles = await articlesQuery
            .OrderByDescending(a => a.CreatedAt)
            .Skip(query.Offset)
            .Take(query.Limit)
            .ToListAsync();

        IEnumerable<ArticleDto> returnArticles = articles.Select(
            a =>
            {
                bool isFavorited = false;
                bool isFollowing = false;
                if(userId != null)
                {
                    isFavorited = a.FavoritedBy.FirstOrDefault(u => u.Id == userId) != null;
                    isFollowing = a.Author.Followers.FirstOrDefault(u => u.Id == userId) != null;
                }                
                
                return ArticleDtoFactory(a, isFavorited, isFollowing);
            }
        );

        ArticleListResponse response = new(
            returnArticles, 
            articleCount
        );
        return ServiceResult<ArticleListResponse>.Ok(response);
    }

    public async Task<ServiceResult<ArticleResponse?>> GetArticleBySlugAsync(string slug, int? userId)
    {
        Article? article = await context.Articles
            .Include(a => a.Author).ThenInclude(user => user.Followers)
            .Include(a => a.FavoritedBy)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Slug == slug);
        
        if(article == null)
        {
            return ServiceResult<ArticleResponse?>.NotFound($"Article with slug '{slug}' was not found.");
        }

        bool isFavorited = false;
        bool isFollowing = false;
        if(userId != null)
        {
            isFollowing = article.Author.Followers.FirstOrDefault(u => u.Id == userId) != null;
            isFavorited = article.FavoritedBy.FirstOrDefault(u => u.Id == userId) != null;
        }

        ArticleDto dto = ArticleDtoFactory(article, isFavorited, isFollowing);
        ArticleResponse response = new(dto);

        return ServiceResult<ArticleResponse?>.Ok(response);
    }

    public async Task<ServiceResult<ArticleResponse>> CreateAsync(CreateArticleDto dto, int userId)
    {
        User? currentUser = await context.Users.FindAsync(userId);
        
        string slug = await SlugifyAsync(dto.Title);

        Article article = new()
        {
            Title = dto.Title,
            Description = dto.Description,
            Body = dto.Body,
            Slug = slug, 
            AuthorId = userId
        };
        
        if(dto.TagList.Any())
        {
            List<string> incomingTags = dto.TagList.Select(t => t.ToLower()).Distinct().ToList();
            List<Tag> existingTags = await context.Tags
                .Where(t => incomingTags.Contains(t.TagText))
                .ToListAsync();

            List<string> existingTagNames = existingTags.Select(t => t.TagText).ToList();
            IEnumerable<string> newTagNames = dto.TagList.Except(existingTagNames);

            List<Tag> newTags = newTagNames.Select(name => new Tag {TagText = name}).ToList();
            article.TagList = existingTags.Concat(newTags).ToList();
        }

        // The user automatically favorites their own article
        article.FavoritedBy.Add(currentUser!);

        context.Articles.Add(article);
        await context.SaveChangesAsync();

        await context.Entry(article).Reference(a => a.Author).LoadAsync();

        ArticleDto data = ArticleDtoFactory(article, true, false);
        ArticleResponse response = new(data);

        return ServiceResult<ArticleResponse>.Ok(response);
    }

    public async Task<ServiceResult<ArticleResponse?>> UpdateAsync(
        string slug, 
        UpdateArticleDto dto,
        int userId
    )
    {
        Article? article = await context.Articles
            .Include(a => a.Author)
            .Include(a => a.FavoritedBy)
            .Where(a => a.Slug == slug)
            .FirstOrDefaultAsync();
        
        if(article == null)
        {
            return ServiceResult<ArticleResponse?>.NotFound($"Article with slug '{slug}' was not found.");
        }
        if (article.AuthorId != userId)
        {
            return ServiceResult<ArticleResponse?>.Unauthorized("You do not have permission to edit this article.");
        }

        // Apply partial updates
        if(!string.IsNullOrWhiteSpace(dto.Title))
        {
            article.Title = dto.Title;
            article.Slug = await SlugifyAsync(dto.Title);
        }
        if(!string.IsNullOrWhiteSpace(dto.Description))
        {
            article.Description = dto.Description;
        }
        if(!string.IsNullOrWhiteSpace(dto.Body))
        {
            article.Body = dto.Body;
        }

        await context.SaveChangesAsync();

        bool isFavorited = article.FavoritedBy.FirstOrDefault(u => u.Id == userId) != null;

        ArticleDto articleDto = ArticleDtoFactory(article, isFavorited, false);
        ArticleResponse response = new(articleDto);

        return ServiceResult<ArticleResponse?>.Ok(response);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(string slug, int userId)
    {
        int? authorId = await context.Articles
            .Where(a => a.Slug == slug)
            .Select(a => (int?)a.AuthorId)
            .FirstOrDefaultAsync();
        if (authorId == null)
        {
            return ServiceResult<bool>.NotFound($"Article with slug '{slug}' was not found.");
        }
        if (authorId != userId)
        {
            return ServiceResult<bool>.Unauthorized("You do not have permission to delete this article");
        }

        await context.Articles
            .Where(a => a.Slug == slug)
            .ExecuteDeleteAsync();

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<ArticleResponse?>> FavoriteArticleAsync(string slug, int userId)
    {
        Article? article = await context.Articles
            .Include(a => a.Author).ThenInclude(user => user.Followers)
            .Include(a => a.TagList)
            .Include(a => a.FavoritedBy)
            .FirstOrDefaultAsync(a => a.Slug == slug);

        if (article == null)
        {
            return ServiceResult<ArticleResponse?>.NotFound($"Article with slug '{slug}' was not found.");
        }

        if(article.FavoritedBy.All(u => u.Id != userId))
        {
            User? user = await context.Users.FindAsync(userId);
            article.FavoritedBy.Add(user!);
            await context.SaveChangesAsync();
        }
        bool isFollowing = article.Author.Followers.FirstOrDefault(u => u.Id == userId) != null;

        ArticleDto dto = ArticleDtoFactory(article, true, isFollowing);
        ArticleResponse response = new(dto);

        return ServiceResult<ArticleResponse?>.Ok(response);
    }

    public async Task<ServiceResult<ArticleResponse?>> UnfavoriteArticleAsync(string slug, int userId)
    {
        Article? article = await context.Articles
            .Include(a => a.Author).ThenInclude(user => user.Followers)
            .Include(a => a.TagList)
            .Include(a => a.FavoritedBy)
            .FirstOrDefaultAsync(a => a.Slug == slug);

        if (article == null)
        {
            ServiceResult<ArticleDto?>.NotFound($"Article with slug '{slug}' was not found.");
        }

        if(article!.FavoritedBy.Any(u => u.Id == userId))
        {
            User? user = await context.Users.FindAsync(userId);
            article.FavoritedBy.Remove(user!);
            await context.SaveChangesAsync();
        }
        bool isFollowing = article.Author.Followers.FirstOrDefault(u => u.Id == userId) != null;

        ArticleDto dto = ArticleDtoFactory(article, false, isFollowing);
        ArticleResponse response = new(dto);

        return ServiceResult<ArticleResponse?>.Ok(response);
    }

    private ArticleDto ArticleDtoFactory(Article article, bool isFavorited, bool isFollowing)
    {
        ArticleDto a = article.Adapt<ArticleDto>();
        a.Favorited = isFavorited;
        a.Author.Following = isFollowing;

        // Manufacture the profile image URL
        string? absoluteFileUrl = fileService.GetAbsoluteFileUrl(article.Author.Image);
        a.Author.Image = absoluteFileUrl;

        return a;
    }

    private async Task<string> SlugifyAsync(string text)
    {
        string slug = text.Replace(' ', '-').ToLower();

        bool slugExists = await context.Articles.AnyAsync(a => a.Slug == slug);

        if(slugExists)
        {
            string randomSuffix = Guid.NewGuid().ToString().Substring(0, 6);
            slug = $"{slug}-{randomSuffix}";
        }
        return slug;
    }
}