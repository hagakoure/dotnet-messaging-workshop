using FluentValidation;

namespace Api.Filters;

public class FluentValidationEndpointFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, 
        EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
        {
            return await next(context);
        }

        var validator = context.HttpContext.RequestServices
            .GetService<IValidator<T>>();
        
        if (validator is null)
        {
            return await next(context);
        }

        var validationResult = await validator.ValidateAsync(argument);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());
            
            return Results.ValidationProblem(errors);
        }

        return await next(context);
    }
}