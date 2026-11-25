namespace Artskart3.Core.Domain.Entities;

/// <summary>
/// Totalen per filtermaske, uten noen bøttedimensjon. 32 rader.
///
/// Finnes for bool-fasettene («med bilde», «sensitiv», …). De grupperes ikke på noen
/// dimensjon — de er én sum med én bit satt — så uten andre aktive filtre er det ingen
/// bøttedimensjon å lese summen fra.
///
/// Summen KUNNE hentes fra <see cref="FacetCountCacheLevel1"/> ved å summere over alle
/// bøttene i en vilkårlig dimensjon, men bare hvis nettopp den dimensjonen har en
/// NULL-bøtte som fanger radene uten verdi. Geografi har ingen, og kategori har
/// 2 282 814 rader i sin. 32 eksplisitte rader koster ingenting og fjerner koblingen.
///
/// Summen over alle 32 maskene er totalen uten bool-filter.
/// </summary>
public class FacetCountCacheLevel0 : IHarFilterMaske
{
    public byte Mask { get; set; }

    public int ObservationCount { get; set; }
}

/// <summary>
/// Fasett-antall for ett enkelt filter: én rad per (dimensjon, bøtte, maske).
///
/// Dette er <see cref="AreaCountCacheLevel1"/> uten utdatacellen. Bøttene, maskene og
/// dimensjons-id-ene er de samme — se <c>FacetCountCacheDimensions</c> for hvorfor
/// registeret ikke dupliseres.
///
/// Brukes på to måter:
///   - dimensjonsfasett uten andre aktive filtre: grupper på <see cref="BucketId"/>
///   - bool-fasett med ett annet aktivt filter: summer over filterets bøtter
///
/// Målt radkost på produksjonslik data: 68 834 rader, mot 2 262 224 i områdebufferens
/// ettnivå. Ingen komprimering — tabellen er for liten til at den lønner seg.
/// </summary>
public class FacetCountCacheLevel1 : IHarFilterMaske
{
    public byte DimensionId { get; set; }

    public int BucketId { get; set; }

    /// <summary>
    /// Bitmaske over de fem bool-filtrene. Se <c>FilterMask</c>.
    ///
    /// Ligger SIST i nøkkelen, som i områdebufferen: oppslaget uten bool-filter må
    /// summere over maskene for samme bøtte, og de ligger da sammenhengende. Et
    /// bool-filter er <c>(Mask &amp; Care) == Want</c>, altså et restledd uansett hvor
    /// kolonnen står — en tidligere plassering ville bare hjulpet når alle fem bitene
    /// er satt samtidig.
    /// </summary>
    public byte Mask { get; set; }

    public int ObservationCount { get; set; }
}

/// <summary>
/// Fasett-antall for to filtre samtidig: én rad per (pargruppe, bøtteA, bøtteB, maske).
///
/// <see cref="BucketA"/> hører til dimensjonen med lavest id i paret, som i
/// <see cref="AreaCountCacheLevel2"/>. Pargruppe-id-en tildeles av
/// <c>AreaCountCacheDimensions.GetPairId</c> og gjenbrukes her — to buffere som
/// nummererer par ulikt svarer riktige tall for feil par.
///
/// Brukes på to måter:
///   - dimensjonsfasett med ett annet aktivt filter: grupper på fasettens side av paret
///   - bool-fasett med to andre aktive filtre: summer over begge filtrenes bøtter
///
/// Målt radkost: 4 388 868 rader, mot 43 710 551 i områdebufferens toernivå. Fordelt på
/// 66 par er det i snitt ~66 000 rader per par, så et oppslag som må skanne et helt par
/// er billig — derfor er klyngenøkkelen den samme uansett hvilken side fasetten ligger
/// på, framfor to indekser for de to retningene.
/// </summary>
public class FacetCountCacheLevel2 : IHarFilterMaske
{
    public byte DimensionPairId { get; set; }

    public int BucketA { get; set; }

    public int BucketB { get; set; }

    /// <summary>Se <see cref="FacetCountCacheLevel1.Mask"/>.</summary>
    public byte Mask { get; set; }

    public int ObservationCount { get; set; }
}

/// <summary>
/// Tilstanden til fasett-bufferen. Én rad, <see cref="Id"/> = 1.
///
/// EGEN RAD, IKKE <see cref="AreaCountCacheState"/>
/// Områdebufferens tilstandsrad står allerede på <c>Ready</c> med en
/// <see cref="SchemaVersion"/> som matcher koden. Tok fasettoppslaget den som sitt
/// klarsignal, ville det lest tomme fasett-tabeller som «null treff» i vinduet mellom
/// deploy og første bygg — altså vist 0 ved hvert valg i hele filterpanelet. Med egen
/// rad faller fasett-tallene tilbake til aggregering til bygget er kjørt, og de to
/// bufferne kan bygges uavhengig.
///
/// <see cref="SchemaVersion"/> kommer fra <c>FacetCountCacheDimensions.SchemaVersion</c>,
/// som selv er avledet av områderegisterets versjon: endres bøttene eller
/// pargruppe-id-ene der, må fasett-bufferen bygges om selv om fasett-registeret er urørt.
/// </summary>
public class FacetCountCacheState
{
    public byte Id { get; set; }

    public int SchemaVersion { get; set; }

    /// <summary>Building, Ready eller Failed. Kun Ready leses av oppslaget.</summary>
    public string Status { get; set; } = "Building";

    public DateTime? BuiltAt { get; set; }

    /// <summary>Antall rader i ObservationEntityIndex da bufferen ble bygget.</summary>
    public long? SourceRows { get; set; }

    public int? DurationSeconds { get; set; }
}
