using FacilityManagementService.API;
using FacilityManagementService.Application;
using FacilityManagementService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddApi();

var app = builder.Build();

app.UseApi();

// Each feature maps its endpoints here, e.g. app.MapFacilityEndpoints(); see the README.

app.Run();
