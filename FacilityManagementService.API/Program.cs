using FacilityManagementService.API;
using FacilityManagementService.API.Facilities;
using FacilityManagementService.API.Users;
using FacilityManagementService.Application;
using FacilityManagementService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddApi();

var app = builder.Build();

app.UseApi();

app.MapUserEndpoints();
app.MapFacilityEndpoints();

app.Run();

