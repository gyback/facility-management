using FacilityManagementService.Application.Validation;
using FacilityManagementService.Domain.Facilities;

namespace FacilityManagementService.Application.Facilities;

internal sealed class SetFacilityMemberRoleValidator : IValidator<SetFacilityMemberRole>
{
    public IEnumerable<ValidationError> Validate(SetFacilityMemberRole command)
    {
        if (!Enum.IsDefined(command.Role))
        {
            yield return ValidationError.OutOfRange(
                nameof(SetFacilityMemberRole.Role),
                nameof(FacilityRole.Member),
                nameof(FacilityRole.Administrator));
        }
    }
}
