namespace Artskart3.Core.Application.Import.Writing;

/// <summary>
/// Konfigurasjonen for importkjøringen, bundet fra <c>Import</c>-seksjonen.
/// </summary>
public class ImportOptions
{
    public const string SectionName = "Import";

    /// <summary>
    /// Hvor mange poster som samles opp før en runde skrives. 250 000 er planens
    /// forslag: nok til at columnstore komprimerer radgruppene (minst 102 400), og
    /// lite nok til at en avbrutt kjøring ikke mister mye.
    /// </summary>
    public int BatchSize { get; set; } = 250_000;

    /// <summary>
    /// Nodene som skal kjøres. Tom liste betyr alle som ikke er merket slettet i
    /// <c>DataSource</c>.
    /// </summary>
    public int[] Nodes { get; set; } = [];

    /// <summary>
    /// Om importen får opprette datasett og prosjekter den ikke finner.
    ///
    /// SETT DEN FALSE UNDER PARALLELLVERIFISERING (plan 4.8). Da avviser importen
    /// posten med <see cref="RecordValidationCode.OrganizationNotCreated"/> i stedet
    /// for å opprette raden, og registrene kan ikke divergere fra Artskart 2s.
    /// </summary>
    public bool CreateMissingOrganizations { get; set; } = true;

    /// <summary>
    /// Om ukjente funntyper og atferder føres opp som varianter på
    /// «unrecognised»-raden. Rent bokholderi — utfallet for posten er det samme.
    /// </summary>
    public bool RecordUnrecognisedVariants { get; set; } = true;

    /// <summary>
    /// MASKERINGEN AV SENSITIVE ARTER FINNES IKKE ENNÅ (fase 8), og uten den skriver
    /// importen eksakte koordinater for arter som skal maskeres. Planen gjør derfor
    /// fase 8 til en sperre for produksjonssetting, og denne flaggen er den sperren:
    /// uten den nekter importen å kjøre.
    ///
    /// SETT DEN BARE FOR TESTDATA. Den finnes for at node-for-node-verifisering mot en
    /// testbase skal være mulig før fase 8 er ferdig, og skal fjernes sammen med
    /// sperren når maskeringen er på plass.
    ///
    /// EN NODE MERKET <c>IsSensitive</c> KJØRER IKKE UANSETT. For den er hele
    /// datasettet sensitivt, og flaggen hjelper ikke.
    /// </summary>
    public bool AllowImportWithoutMasking { get; set; }
}
