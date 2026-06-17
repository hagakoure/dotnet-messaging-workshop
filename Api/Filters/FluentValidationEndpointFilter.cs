using FluentValidation;
using MassTransit;

namespace Api.Filters;

public class ValidationFilter<T>(IEnumerable<IValidator<T>> validators) : IFilter<ConsumeContext<T>>
    where T : class
{
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        var validationContext = new ValidationContext<T>(context.Message);
        
        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(validationContext)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Any())
        {
            var errors = string.Join("; ", failures.Select(f => f.ErrorMessage));
            throw new ValidationException($"Validation failed: {errors}");
        }

        await next.Send(context);
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("validation");
    }
}