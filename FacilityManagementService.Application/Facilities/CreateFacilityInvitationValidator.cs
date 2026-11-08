using FacilityManagementService.Application.Validation;
using FacilityManagementService.Domain.Facilities;

namespace FacilityManagementService.Application.Facilities;

internal sealed class CreateFacilityInvitationValidator : IValidator<CreateFacilityInvitation>
{
    public IEnumerable<ValidationError> Validate(CreateFacilityInvitation command)
    {
        if (!Enum.IsDefined(command.Role))
        {
            yield return ValidationError.OutOfRange(
                nameof(CreateFacilityInvitation.Role),
                nameof(FacilityRole.Member),
                nameof(FacilityRole.Administrator));
        }
    }
}
