using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.Application.Users;

/// <summary>
/// Persistence of <see cref="User"/> aggregates for command handlers (ADR-0014, ADR-0015).
/// Never calls <c>SaveChanges</c>; changes are committed by the transaction decorator's
/// <see cref="Persistence.IUnitOfWork"/>.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetAsync(UserId id, CancellationToken cancellationToken = default);

    Task<User?> FindByExternalIdentityAsync(
        string provider,
        string subject,
        CancellationToken cancellationToken = default);

    void Add(User user);
}
