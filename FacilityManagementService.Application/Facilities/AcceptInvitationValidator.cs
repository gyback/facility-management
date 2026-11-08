using FacilityManagementService.Application.Validation;
using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Application.Facilities;

internal sealed class AcceptInvitationValidator : IValidator<AcceptInvitation>
{
    public IEnumerable<ValidationError> Validate(AcceptInvitation command)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
        {
            yield return ValidationError.Required(nameof(AcceptInvitation.Token));
        }

        if (command.DisplayName is not null)
        {
            if (string.IsNullOrWhiteSpace(command.DisplayName))
            {
                yield return ValidationError.Required(nameof(AcceptInvitation.DisplayName));
            }
            else if (command.DisplayName.Trim().Length > User.MaxDisplayNameLength)
            {
                yield return ValidationError.TooLong(
                    nameof(AcceptInvitation.DisplayName),
                    User.MaxDisplayNameLength);
            }
        }
    }
}
