using FacilityManagementService.Application.Validation;
using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Application.Users;

internal sealed class UpdateCurrentUserValidator : IValidator<UpdateCurrentUser>
{
    public IEnumerable<ValidationError> Validate(UpdateCurrentUser command)
    {
        if (string.IsNullOrWhiteSpace(command.DisplayName))
        {
            yield return ValidationError.Required(nameof(UpdateCurrentUser.DisplayName));
        }
        else if (command.DisplayName.Trim().Length > User.MaxDisplayNameLength)
        {
            yield return ValidationError.TooLong(nameof(UpdateCurrentUser.DisplayName), User.MaxDisplayNameLength);
        }
    }
}
