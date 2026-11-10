using FacilityManagementService.API.Http.Authentication;
using FacilityManagementService.API.Http.Responses;
using FacilityManagementService.Application.Messaging;
using FacilityManagementService.Application.Users;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FacilityManagementService.API.Users;

internal static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/users").WithTags("Users");

        users.MapGet("/me", GetCurrentUser)
            .WithName(nameof(Application.Users.GetCurrentUser))
            .AllowUnregisteredUser();

        users.MapPut("/me", UpdateCurrentUser)
            .WithName(nameof(Application.Users.UpdateCurrentUser))
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<Ok<CurrentUserResponse>> GetCurrentUser(
        IQueryHandler<Application.Users.GetCurrentUser, CurrentUserProfile> handler,
        CancellationToken cancellationToken)
    {
        var profile = await handler.HandleAsync(new Application.Users.GetCurrentUser(), cancellationToken);
        return TypedResults.Ok(CurrentUserResponse.From(profile));
    }

    private static async Task<Ok<CommandResponse>> UpdateCurrentUser(
        UpdateCurrentUserRequest request,
        ICommandHandler<Application.Users.UpdateCurrentUser, Unit> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(request.ToCommand(), cancellationToken);
        return TypedResults.Ok(CommandResponse.NoWarnings);
    }
}
