using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RealWorld.Models.Validators.Filters;

public class AutoValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (object? argument in context.ActionArguments.Values.Where(v => v != null))
        {
            Type argumentType = argument!.GetType();

            Type validatorType = typeof(IValidator<>).MakeGenericType(argumentType);
            IValidator? validator = context.HttpContext.RequestServices.GetService(validatorType) as IValidator;

            if (validator == null) continue;
            ValidationContext<object> validationContext = new(argument);
            ValidationResult validationResult = await validator.ValidateAsync(validationContext);

            if (validationResult.IsValid) continue;
            context.Result = new UnprocessableEntityObjectResult(
                new { errors = validationResult.ToDictionary() }
            );
                    
            return;
        }

        await next();
    }
}