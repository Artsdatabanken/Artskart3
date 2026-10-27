namespace Artskart3.Core.Application.Import;

/// <summary>
/// De to referansetabellene som har varianter.
/// </summary>
public enum ReferenceValueKind
{
    /// <summary>Funntype — <c>BasisOfRecord</c>, 14 rader.</summary>
    BasisOfRecord,

    /// <summary>Atferd — <c>Behavior</c>, 6 rader.</summary>
    Behavior
}

/// <summary>
/// Oppslag fra kildens verdi til en rad i <c>BasisOfRecord</c> eller <c>Behavior</c>.
///
/// VARIANTER ER HOVEDSAKEN HER. Kildene skriver funntype og atferd på alle slags vis —
/// «preserved_specimen», «specimen», «obs. from boat», «trafikkdrept» — og tabellene
/// bærer en liste over hvilke skrivemåter som betyr hva. Uten den ville de fleste
/// postene fått «ikke gjenkjent».
///
/// LISTEN ER EN STRENG I SQL, OG SKILLETEGNET ER IKKE DET SAMME I DE TO TABELLENE:
/// <c>BasisOfRecord.Variants</c> er kommaseparert, <c>Behavior.Variants</c> er
/// pipeseparert. Det er ikke en tilfeldighet i dataene — det står i Artskart 2s
/// <c>SqlBasisOfRecord</c> og <c>SqlBehavior</c>, som joiner med hvert sitt tegn.
/// </summary>
public interface IReferenceValueRegistry
{
    /// <summary>
    /// Raden som heter dette. Navnet er nøkkelen Artskart 2 slår opp på — det er
    /// RavenDB-dokumentets id uten typeprefikset, og det samme som SQL-kolonnen
    /// <c>Name</c>.
    /// </summary>
    ReferenceValue? ByName(ReferenceValueKind kind, string name);

    /// <summary>
    /// Raden som har denne verdien blant variantene sine.
    ///
    /// SAMMENLIGNINGEN ER BOKSTAVSTØRRELSESFØLSOM, slik Artskart 2s
    /// <c>List&lt;string&gt;.Contains</c> er. Kilden er alt gjort om til små bokstaver
    /// før oppslaget, så varianter som står med store bokstaver i tabellen — og det
    /// finnes noen, som <c>DNA</c> og <c>eDNA Barcoding</c> — kan aldri treffe. De
    /// samme verdiene står også med små bokstaver, og det er de som gjør jobben.
    ///
    /// «unrecognised» og «ukjent» KAN IKKE TREFFE. Variantene på dem er en logg over
    /// hvilke ugyldige verdier kildene har sendt, ikke et oppslag — og å treffe dem
    /// ville betydd at en ukjent verdi ble gjenkjent som ukjent.
    /// </summary>
    ReferenceValue? ByVariant(ReferenceValueKind kind, string variant);

    /// <summary>
    /// Raden ugjenkjennelige verdier havner på. <c>BasisOfRecord</c> har den som Id -1;
    /// <c>Behavior</c> har den ikke i dag, og trenger den ikke — en ukjent atferd gir
    /// ingen atferd på observasjonen i det hele tatt.
    /// </summary>
    ReferenceValue? Unrecognised(ReferenceValueKind kind);
}

/// <summary>
/// Skriver ukjente verdier inn i <c>Variants</c> på «unrecognised»-raden. Port av
/// Artskart 2s <c>AddVariantToBasisOfRecord</c> og <c>StoreBehavior</c>.
///
/// DET ER BOKFØRING, IKKE DATA POSTEN TRENGER. Observasjonen får «unrecognised» uansett
/// om skrivingen skjer eller ikke — listen finnes for at noen skal kunne se hvilke
/// verdier kildene faktisk sender. Derfor kan den slås av (opprett-ikke-modus, plan
/// 4.8) uten at utfallet for posten endres, i motsetning til
/// <see cref="IOrganizationCreator"/>, som skriver fremmednøkkelmål.
///
/// DEN SKRIVER TIL SAMME KOLONNE SOM ARTSKART 2. En egen variant-tabell ville vært
/// ryddigere — <c>Variants</c> er <c>nvarchar(300)</c>, og en variant som inneholder
/// skilletegnet kan ikke representeres — men gjennom overgangen er Artskart 2 den andre
/// skriveren av nettopp denne kolonnen. Skrev de to til hvert sitt sted, ville de
/// divergere uten at noen så det. Egen tabell hører til det punktet Artskart 2 slutter
/// å skrive.
/// </summary>
public interface IReferenceVariantWriter
{
    /// <summary>
    /// Fører verdien opp som variant på «unrecognised»-raden, om den ikke står der
    /// allerede.
    /// </summary>
    void RecordUnrecognisedValue(ReferenceValueKind kind, string value);
}

/// <summary>
/// En rad i en av de to referansetabellene.
/// </summary>
/// <param name="Id">
/// Blir <c>Observation.BasisOfRecordId</c> eller en rad i <c>ObservationBehavior</c>.
/// </param>
/// <param name="Name">Navnet, som også er oppslagsnøkkelen.</param>
public sealed record ReferenceValue(int Id, string Name);
