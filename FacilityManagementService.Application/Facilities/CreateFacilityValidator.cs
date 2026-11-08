using FacilityManagementService.Application.Validation;
using FacilityManagementService.Domain.Facilities;

namespace FacilityManagementService.Application.Facilities;

internal sealed class CreateFacilityValidator : IValidator<CreateFacility>
{
    public IEnumerable<ValidationError> Validate(CreateFacility command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            yield return ValidationError.Required(nameof(CreateFacility.Name));
        }
        else if (command.Name.Trim().Length > Facility.MaxNameLength)
        {
            yield return ValidationError.TooLong(nameof(CreateFacility.Name), Facility.MaxNameLength);
        }

        if (command.Capacity is < Facility.MinCapacity or > Facility.MaxCapacity)
        {
            yield return ValidationError.OutOfRange(
                nameof(CreateFacility.Capacity),
                Facility.MinCapacity,
                Facility.MaxCapacity);
        }
    }
}
