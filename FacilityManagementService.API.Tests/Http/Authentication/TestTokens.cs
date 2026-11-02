using System.Security.Cryptography;
using FacilityManagementService.Domain.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace FacilityManagementService.API.Tests.Http.Authentication;

/// <summary>
/// Mints real signed JWTs in-process and wires <see cref="JwtBearerOptions"/> to trust the test
/// signing key, so tests exercise the production JWT bearer handler without network calls
/// (ADR-0015).
/// </summary>
internal static class TestTokens
{
    public const string Authority = "https://auth.example.test";
    public const string Audience = "facility-management-api";
    public const string Provider = "test";
    public const string SubjectClaim = "sub";

    public const string RegisteredSubject = "registered-subject";
    public const string UnregisteredSubject = "unregistered-subject";
    public const string DisplayName = "Alice";

    public static readonly UserId RegisteredUserId =
        UserId.From(Guid.Parse("01933b4a-8c10-7000-8000-000000000001"));

    public static readonly SecurityKey SigningKey = CreateKey("test-key");
    public static readonly SecurityKey UntrustedSigningKey = CreateKey("untrusted-key");

    private static readonly JsonWebTokenHandler TokenHandler = new();

    public static readonly string RegisteredUserToken = Issue();

    public static string Issue(
        string? subject = RegisteredSubject,
        string? displayName = DisplayName,
        string issuer = Authority,
        string audience = Audience,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? expires = null,
        SecurityKey? signingKey = null,
        string subjectClaim = SubjectClaim)
    {
        var now = DateTimeOffset.UtcNow;
        var claims = new Dictionary<string, object>();

        if (subject is not null)
        {
            claims[subjectClaim] = subject;
        }

        if (displayName is not null)
        {
            claims["name"] = displayName;
        }

        return TokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Claims = claims,
            NotBefore = (notBefore ?? now.AddMinutes(-1)).UtcDateTime,
            Expires = (expires ?? now.AddMinutes(30)).UtcDateTime,
            SigningCredentials = new SigningCredentials(
                signingKey ?? SigningKey,
                SecurityAlgorithms.EcdsaSha256),
        });
    }

    public static void ConfigureJwtBearer(JwtBearerOptions options)
    {
        var configuration = new OpenIdConnectConfiguration { Issuer = options.Authority };
        configuration.SigningKeys.Add(SigningKey);
        options.Configuration = configuration;
        options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
    }

    private static ECDsaSecurityKey CreateKey(string keyId) =>
        new(ECDsa.Create(ECCurve.NamedCurves.nistP256)) { KeyId = keyId };
}

