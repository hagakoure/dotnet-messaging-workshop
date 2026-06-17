using FluentValidation;
using Shared.Contracts;

namespace Api.Validators;

public class EmailRequestedValidator : AbstractValidator<EmailRequested>
{
    public EmailRequestedValidator()
    {
        RuleFor(x => x.To)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("Invalid email format.");

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required.")
            .MaximumLength(200);

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Email body cannot be empty.");
    }
}