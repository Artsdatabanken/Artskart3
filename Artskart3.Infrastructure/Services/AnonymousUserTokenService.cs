using System.Security.Cryptography;
using Artskart3.Core.Application.Services.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace Artskart3.Infrastructure.Services;

/// <summary>
/// Signerer anonyme eksport-ID-er med ASP.NET Core Data Protection.
/// Nøkkelen ligger i DataProtection-nøkkelringen, så token overlever omstart
/// og fungerer på tvers av instanser som deler nøkkellager.
/// </summary>
public class AnonymousUserTokenService : IAnonymousUserTokenService
{
    private const string Purpose = "AnonymousExportUser.v1";

    private readonly IDataProtector _protector;

    public AnonymousUserTokenService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string CreateToken()
    {
        return _protector.Protect(Guid.NewGuid().ToString());
    }

    public bool TryGetUserId(string? token, out Guid userId)
    {
        userId = Guid.Empty;
        if (string.IsNullOrEmpty(token))
            return false;

        string payload;
        try
        {
            payload = _protector.Unprotect(token);
        }
        catch (CryptographicException)
        {
            // Feil signatur, tukla innhold eller ukjent nøkkel — ikke vår ID.
            return false;
        }

        return Guid.TryParse(payload, out userId) && userId != Guid.Empty;
    }
}
