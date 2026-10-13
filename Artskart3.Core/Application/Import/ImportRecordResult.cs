using Artskart3.Core.Application.Import.Helpers;

namespace Artskart3.Core.Application.Import;

/// <summary>
/// Hva ett steg kom fram til for én post. Port av Artskart 2s
/// <c>ProcessRecordResult</c>, som også er raden avviste poster lagres som.
///
/// <see cref="ErrorContext"/> er trunkert til 400 tegn med <c>DotLeft</c> fordi
/// kolonnen er <c>NVARCHAR(400) NOT NULL</c>. Trunkeringen skjer her og ikke ved
/// skriving, slik at det som står i loggen er det som havner i basen.
/// </summary>
public class ImportRecordResult
{
    private ImportRecordResult(
        RecordValidationCode validationCode,
        RecordNotificationCode notificationCode,
        string errorContext)
    {
        ValidationCode = validationCode;
        NotificationCode = notificationCode;
        ErrorContext = ImportValueHelper.DotLeft(errorContext, 400) ?? string.Empty;
    }

    public RecordValidationCode ValidationCode { get; }

    public RecordNotificationCode NotificationCode { get; }

    public string ErrorContext { get; }

    /// <summary>
    /// Kodene som lar posten fortsette. <see cref="RecordValidationCode.Warn"/> gir en
    /// advarsel på siden, og <see cref="RecordValidationCode.SensitiveSpecies"/> betyr
    /// at maskeringen har gjort jobben sin — ingen av dem avviser.
    /// </summary>
    public bool IsAccepted => ValidationCode is RecordValidationCode.Ok
        or RecordValidationCode.Warn
        or RecordValidationCode.SensitiveSpecies;

    public bool IsWarning => ValidationCode == RecordValidationCode.Warn;

    /// <summary>Alt gikk bra, ingenting å melde.</summary>
    public static ImportRecordResult Ok() =>
        new(RecordValidationCode.Ok, RecordNotificationCode.Ok, string.Empty);

    /// <summary>Gikk bra, men med en merknad verdt å spore (f.eks. at en samling ble opprettet).</summary>
    public static ImportRecordResult Ok(RecordNotificationCode notificationCode, string errorContext = "") =>
        new(RecordValidationCode.Ok, notificationCode, errorContext);

    /// <summary>
    /// Posten importeres, men noe er verdt å advare om. Advarselen havner i
    /// <c>WarningRecord</c>-sporet, ikke på observasjonen.
    /// </summary>
    public static ImportRecordResult Warn(RecordNotificationCode notificationCode, string errorContext) =>
        new(RecordValidationCode.Warn, notificationCode, errorContext);

    /// <summary>Posten avvises. Kjeden stopper for denne posten.</summary>
    public static ImportRecordResult Rejected(
        RecordValidationCode validationCode,
        RecordNotificationCode notificationCode,
        string errorContext) =>
        new(validationCode, notificationCode, errorContext);

    /// <summary>
    /// Sensitiv art. Posten fortsetter — koden finnes for at avgjørelsen skal være
    /// sporbar i resultatet, ikke for å stoppe noe.
    /// </summary>
    public static ImportRecordResult Sensitive(RecordNotificationCode notificationCode, string errorContext = "") =>
        new(RecordValidationCode.SensitiveSpecies, notificationCode, errorContext);

    public override string ToString() =>
        ErrorContext.Length == 0
            ? $"{ValidationCode}/{NotificationCode}"
            : $"{ValidationCode}/{NotificationCode}: {ErrorContext}";
}
