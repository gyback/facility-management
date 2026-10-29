using FacilityManagementService.Domain.Entities;

namespace FacilityManagementService.Domain.Users;

/// <summary>
/// The identifier of a user (ADR-0015, ADR-0018). Stays, comments and memberships reference
/// <see cref="UserId"/>, never an identity provider's subject.
/// </summary>
public readonly record struct UserId(Guid Value) : IEntityId<UserId>
{
    public static UserId New() => new(Guid.CreateVersion7());
    public static UserId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
