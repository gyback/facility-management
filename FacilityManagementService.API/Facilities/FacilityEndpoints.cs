using FacilityManagementService.API.Http.Authentication;
using FacilityManagementService.API.Http.Responses;
using FacilityManagementService.Application.Facilities;
using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Domain.Facilities;
using FacilityManagementService.Domain.Users;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FacilityManagementService.API.Facilities;

internal static class FacilityEndpoints
{
    public static IEndpointRouteBuilder MapFacilityEndpoints(this IEndpointRouteBuilder app)
    {
        var facilities = app.MapGroup("/facilities").WithTags("Facilities");

        facilities.MapPost("", CreateFacility)
            .WithName(nameof(Application.Facilities.CreateFacility));

        facilities.MapGet("", GetFacilities)
            .WithName(nameof(Application.Facilities.GetFacilities));

        var facility = facilities.MapGroup("/{facilityId:guid}");

        facility.MapGet("", GetFacility)
            .WithName(nameof(Application.Facilities.GetFacility))
            .ProducesProblem(StatusCodes.Status404NotFound);

        facility.MapPut("/name", RenameFacility)
            .WithName(nameof(Application.Facilities.RenameFacility))
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        facility.MapPut("/capacity", SetFacilityCapacity)
            .WithName(nameof(Application.Facilities.SetFacilityCapacity))
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        facility.MapPost("/invitations", CreateFacilityInvitation)
            .WithName(nameof(Application.Facilities.CreateFacilityInvitation))
            .ProducesProblem(StatusCodes.Status404NotFound);

        facility.MapGet("/invitations", GetFacilityInvitations)
            .WithName(nameof(Application.Facilities.GetFacilityInvitations))
            .ProducesProblem(StatusCodes.Status404NotFound);

        facility.MapDelete("/invitations/{invitationId:guid}", RevokeFacilityInvitation)
            .WithName(nameof(Application.Facilities.RevokeFacilityInvitation))
            .ProducesProblem(StatusCodes.Status404NotFound);

        facility.MapPut("/members/{userId:guid}", SetFacilityMemberRole)
            .WithName(nameof(Application.Facilities.SetFacilityMemberRole))
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        facility.MapDelete("/members/{userId:guid}", RemoveFacilityMember)
            .WithName(nameof(Application.Facilities.RemoveFacilityMember))
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPost("/invitations/accept", AcceptInvitation)
            .WithName(nameof(Application.Facilities.AcceptInvitation))
            .WithTags("Facilities")
            .AllowUnregisteredUser()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<Created<CreatedResponse>> CreateFacility(
        CreateFacilityRequest request,
        ICommandHandler<Application.Facilities.CreateFacility, FacilityId> handler,
        CancellationToken cancellationToken)
    {
        var id = await handler.HandleAsync(request.ToCommand(), cancellationToken);
        return TypedResults.Created($"/facilities/{id.Value}", CreatedResponse.From(id));
    }

    private static async Task<Ok<IReadOnlyList<FacilitySummaryResponse>>> GetFacilities(
        IQueryHandler<Application.Facilities.GetFacilities, IReadOnlyList<FacilitySummary>> handler,
        CancellationToken cancellationToken)
    {
        var facilities = await handler.HandleAsync(new Application.Facilities.GetFacilities(), cancellationToken);
        return TypedResults.Ok<IReadOnlyList<FacilitySummaryResponse>>(
            facilities.Select(FacilitySummaryResponse.From).ToArray());
    }

    private static async Task<Ok<FacilityResponse>> GetFacility(
        Guid facilityId,
        IQueryHandler<Application.Facilities.GetFacility, FacilityDetails> handler,
        CancellationToken cancellationToken)
    {
        var facility = await handler.HandleAsync(
            new Application.Facilities.GetFacility(FacilityId.From(facilityId)),
            cancellationToken);
        return TypedResults.Ok(FacilityResponse.From(facility));
    }

    private static async Task<Ok<CommandResponse>> RenameFacility(
        Guid facilityId,
        RenameFacilityRequest request,
        ICommandHandler<Application.Facilities.RenameFacility, Unit> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(request.ToCommand(facilityId), cancellationToken);
        return TypedResults.Ok(CommandResponse.NoWarnings);
    }

    private static async Task<Ok<CommandResponse>> SetFacilityCapacity(
        Guid facilityId,
        SetFacilityCapacityRequest request,
        ICommandHandler<Application.Facilities.SetFacilityCapacity, Unit> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(request.ToCommand(facilityId), cancellationToken);
        return TypedResults.Ok(CommandResponse.NoWarnings);
    }

    private static async Task<Created<CreateFacilityInvitationResponse>> CreateFacilityInvitation(
        Guid facilityId,
        CreateFacilityInvitationRequest request,
        ICommandHandler<Application.Facilities.CreateFacilityInvitation, CreatedFacilityInvitation> handler,
        CancellationToken cancellationToken)
    {
        var invitation = await handler.HandleAsync(request.ToCommand(facilityId), cancellationToken);
        return TypedResults.Created(
            $"/facilities/{facilityId}/invitations/{invitation.Id.Value}",
            CreateFacilityInvitationResponse.From(invitation));
    }

    private static async Task<Ok<IReadOnlyList<FacilityInvitationResponse>>> GetFacilityInvitations(
        Guid facilityId,
        IQueryHandler<Application.Facilities.GetFacilityInvitations, IReadOnlyList<PendingFacilityInvitation>> handler,
        CancellationToken cancellationToken)
    {
        var invitations = await handler.HandleAsync(
            new Application.Facilities.GetFacilityInvitations(FacilityId.From(facilityId)),
            cancellationToken);
        return TypedResults.Ok<IReadOnlyList<FacilityInvitationResponse>>(
            invitations.Select(FacilityInvitationResponse.From).ToArray());
    }

    private static async Task<Ok<CommandResponse>> RevokeFacilityInvitation(
        Guid facilityId,
        Guid invitationId,
        ICommandHandler<Application.Facilities.RevokeFacilityInvitation, Unit> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(
            new Application.Facilities.RevokeFacilityInvitation(
                FacilityId.From(facilityId),
                FacilityInvitationId.From(invitationId)),
            cancellationToken);
        return TypedResults.Ok(CommandResponse.NoWarnings);
    }

    private static async Task<Ok<CommandResponse>> SetFacilityMemberRole(
        Guid facilityId,
        Guid userId,
        SetFacilityMemberRoleRequest request,
        ICommandHandler<Application.Facilities.SetFacilityMemberRole, Unit> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(request.ToCommand(facilityId, userId), cancellationToken);
        return TypedResults.Ok(CommandResponse.NoWarnings);
    }

    private static async Task<Ok<CommandResponse>> RemoveFacilityMember(
        Guid facilityId,
        Guid userId,
        ICommandHandler<Application.Facilities.RemoveFacilityMember, Unit> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(
            new Application.Facilities.RemoveFacilityMember(
                FacilityId.From(facilityId),
                UserId.From(userId)),
            cancellationToken);
        return TypedResults.Ok(CommandResponse.NoWarnings);
    }

    private static async Task<Ok<AcceptInvitationResponse>> AcceptInvitation(
        AcceptInvitationRequest request,
        ICommandHandler<Application.Facilities.AcceptInvitation, AcceptedInvitation> handler,
        CancellationToken cancellationToken)
    {
        var accepted = await handler.HandleAsync(request.ToCommand(), cancellationToken);
        return TypedResults.Ok(AcceptInvitationResponse.From(accepted));
    }
}
