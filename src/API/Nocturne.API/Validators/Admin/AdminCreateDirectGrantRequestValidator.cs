using FluentValidation;
using Nocturne.API.Controllers.V4.PlatformAdmin;
using Nocturne.API.Validators.Auth;

namespace Nocturne.API.Validators.Admin;

/// <summary>
/// Validates <see cref="AdminCreateDirectGrantRequest"/> for the platform admin tenant direct
/// grant endpoint.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>SubjectId is required.</description></item>
/// <item><description>Label and scope rules are shared with the self-service endpoint via
/// <see cref="CreateDirectGrantRequestValidator"/>.</description></item>
/// <item><description>ExpiresAt must not be set: direct grants do not expire, and the
/// self-service endpoint's silent-ignore of the field must not carry over to a surface whose
/// callers are SDKs that would trust a requested expiry.</description></item>
/// </list>
/// </remarks>
/// <seealso cref="AdminCreateDirectGrantRequest"/>
/// <seealso cref="TenantDirectGrantController"/>
public class AdminCreateDirectGrantRequestValidator : AbstractValidator<AdminCreateDirectGrantRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AdminCreateDirectGrantRequestValidator"/> class
    /// and configures all validation rules for admin direct grant creation.
    /// </summary>
    public AdminCreateDirectGrantRequestValidator()
    {
        Include(new CreateDirectGrantRequestValidator());
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.ExpiresAt).Null()
            .WithMessage("Direct grants do not expire; ExpiresAt must not be set");
    }
}
