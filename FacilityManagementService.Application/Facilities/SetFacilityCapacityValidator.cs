using FacilityManagementService.Application.Validation;
using FacilityManagementService.Domain.Facilities;

namespace FacilityManagementService.Application.Facilities;

internal sealed class SetFacilityCapacityValidator : IValidator<SetFacilityCapacity>
{
    public IEnumerable<ValidationError> Validate(SetFacilityCapacity command)
    {
        if (command.Capacity is < Facility.MinCapacity or > Facility.MaxCapacity)
        {
            yield return ValidationError.OutOfRange(
                nameof(SetFacilityCapacity.Capacity),
                Facility.MinCapacity,
                Facility.MaxCapacity);
        }
    }
}
