using FacilityManagementService.Application.Validation;
using FacilityManagementService.Domain.Facilities;

namespace FacilityManagementService.Application.Facilities;

internal sealed class RenameFacilityValidator : IValidator<RenameFacility>
{
    public IEnumerable<ValidationError> Validate(RenameFacility command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            yield return ValidationError.Required(nameof(RenameFacility.Name));
        }
        else if (command.Name.Trim().Length > Facility.MaxNameLength)
        {
            yield return ValidationError.TooLong(nameof(RenameFacility.Name), Facility.MaxNameLength);
        }
    }
}
