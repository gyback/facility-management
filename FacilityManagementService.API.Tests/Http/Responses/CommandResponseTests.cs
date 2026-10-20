using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using FacilityManagementService.API.Http.Responses;
using FacilityManagementService.Application.Messaging;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FacilityManagementService.API.Tests.Http.Responses;

public sealed class CommandResponseTests
{
    private static readonly JsonSerializerOptions WireOptions = new ServiceCollection()
        .AddApi()
        .BuildServiceProvider()
        .GetRequiredService<IOptions<JsonOptions>>()
        .Value.SerializerOptions;

    [Fact]
    public void A_created_response_has_the_id_as_a_string_and_empty_warnings()
    {
        var id = Guid.Parse("6f1c9d0e-4b9a-4f57-9c1e-2a4b8f3d7e10");

        var json = JsonSerializer.Serialize(CreatedResponse.From(new Outcome<Guid>(id)), WireOptions);

        Assert.Equal("""{"id":"6f1c9d0e-4b9a-4f57-9c1e-2a4b8f3d7e10","warnings":[]}""", json);
    }

    [Fact]
    public void A_command_response_without_warnings_still_has_the_array() =>
        Assert.Equal("""{"warnings":[]}""", JsonSerializer.Serialize(CommandResponse.NoWarnings, WireOptions));

    [Fact]
    public void Every_warning_has_a_response_contract()
    {
        var warnings = typeof(Warning).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(Warning)) && !type.IsAbstract);

        Assert.All(warnings, type => WarningResponse.From((Warning)RuntimeHelpers.GetUninitializedObject(type)));
    }

    [Fact]
    public void Warning_contracts_are_polymorphic_on_code_once_there_are_warnings()
    {
        var hasWarnings = typeof(Warning).Assembly.GetTypes().Any(type => type.IsSubclassOf(typeof(Warning)));
        var polymorphic = typeof(WarningResponse).GetCustomAttribute<JsonPolymorphicAttribute>();

        if (hasWarnings)
        {
            Assert.Equal("code", polymorphic?.TypeDiscriminatorPropertyName);
        }
        else
        {
            // Without derived types System.Text.Json refuses the attribute.
            Assert.Null(polymorphic);
        }
    }

    [Fact]
    public void Every_warning_contract_is_registered_with_a_known_code()
    {
        var discriminators = typeof(WarningResponse).GetCustomAttributes<JsonDerivedTypeAttribute>()
            .Select(attribute => attribute.TypeDiscriminator);

        Assert.All(discriminators, code => Assert.Contains(Assert.IsType<string>(code), WarningCodes.All));
    }
}
