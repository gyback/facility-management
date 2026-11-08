using FacilityManagementService.API.Http.Responses;
using FacilityManagementService.Application.Facilities;
using FacilityManagementService.Domain.Facilities;
using FacilityManagementService.Domain.Users;

namespace FacilityManagementService.API.Facilities;

internal sealed record CreateFacilityRequest(string Name, int Capacity)
{
    public CreateFacility ToCommand() => new(Name, Capacity);
}

internal sealed record RenameFacilityRequest(string Name)
{
    public RenameFacility ToCommand(Guid facilityId) =>
        new(FacilityId.From(facilityId), Name);
}

internal sealed record SetFacilityCapacityRequest(int Capacity)
{
    public SetFacilityCapacity ToCommand(Guid facilityId) =>
        new(FacilityId.From(facilityId), Capacity);
}

internal sealed record CreateFacilityInvitationRequest(FacilityRole Role)
{
    public CreateFacilityInvitation ToCommand(Guid facilityId) =>
        new(FacilityId.From(facilityId), Role);
}

internal sealed record CreateFacilityInvitationResponse(
    Guid Id,
    string Token,
    DateTime ExpiresAt,
    IReadOnlyList<WarningResponse> Warnings)
{
    public static CreateFacilityInvitationResponse From(CreatedFacilityInvitation invitation) => new(
        invitation.Id.Value,
        invitation.Token,
        invitation.ExpiresAt.UtcDateTime,
        []);
}

internal sealed record AcceptInvitationRequest(string Token, string? DisplayName = null)
{
    public AcceptInvitation ToCommand() => new(Token, DisplayName);
}

internal sealed record AcceptInvitationResponse(
    Guid FacilityId,
    Guid UserId,
    FacilityRole Role,
    IReadOnlyList<WarningResponse> Warnings)
{
    public static AcceptInvitationResponse From(AcceptedInvitation accepted) => new(
        accepted.FacilityId.Value,
        accepted.UserId.Value,
        accepted.Role,
        []);
}

internal sealed record SetFacilityMemberRoleRequest(FacilityRole Role)
{
    public SetFacilityMemberRole ToCommand(Guid facilityId, Guid userId) =>
        new(FacilityId.From(facilityId), UserId.From(userId), Role);
}

internal sealed record FacilitySummaryResponse(
    Guid Id,
    string Name,
    int Capacity,
    FacilityRole Role)
{
    public static FacilitySummaryResponse From(FacilitySummary summary) => new(
        summary.Id.Value,
        summary.Name,
        summary.Capacity,
        summary.Role);
}

internal sealed record FacilityResponse(
    Guid Id,
    string Name,
    int Capacity,
    IReadOnlyList<FacilityMemberResponse> Members)
{
    public static FacilityResponse From(FacilityDetails facility) => new(
        facility.Id.Value,
        facility.Name,
        facility.Capacity,
        facility.Members.Select(FacilityMemberResponse.From).ToArray());
}

internal sealed record FacilityMemberResponse(
    Guid UserId,
    string DisplayName,
    FacilityRole Role)
{
    public static FacilityMemberResponse From(FacilityMemberDetails member) => new(
        member.UserId.Value,
        member.DisplayName,
        member.Role);
}

internal sealed record FacilityInvitationResponse(
    Guid Id,
    FacilityRole Role,
    DateTime CreatedAt,
    DateTime ExpiresAt)
{
    public static FacilityInvitationResponse From(PendingFacilityInvitation invitation) => new(
        invitation.Id.Value,
        invitation.Role,
        invitation.CreatedAt.UtcDateTime,
        invitation.ExpiresAt.UtcDateTime);
}
