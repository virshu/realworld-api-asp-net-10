using FluentValidation;
using RealWorld.Models.DTOs.Articles;

namespace RealWorld.Models.Validators;

public class CreateArticleValidator : AbstractValidator<CreateArticleRequest>
{
    public CreateArticleValidator()
    {
        RuleFor(x => x.article.Title)
            .NotEmpty().WithMessage("can't be blank");
        
        RuleFor(x => x.article.Description)
            .NotEmpty().WithMessage("can't be blank");

        RuleFor(x => x.article.Body)
            .NotEmpty().WithMessage("can't be blank");
    }
}