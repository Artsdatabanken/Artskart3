namespace Artskart3.Api.Configuration;

/// <summary>
/// Hvilken header som bærer det opprinnelige vertsnavnet inn fra leddet foran appen.
/// Standarden er <c>X-Original-Host</c>, som er det Application Gateway sender i dag.
/// Byttes leddet foran — eller treffer trafikken en ingress direkte — heter headeren
/// noe annet, typisk <c>X-Forwarded-Host</c>. Får appen feil vertsnavn, bygger
/// innloggingen feil returadresse, og brukeren havner på en intern URL etter pålogging
/// eller avvises av identitetsleverandøren.
/// </summary>
public class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    public string OriginalHostHeaderName { get; set; } = "X-Original-Host";
}
