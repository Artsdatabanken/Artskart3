using Artskart3.Core.Application.Import.Models;

namespace Artskart3.Core.Application.Import.Steps;

/// <summary>
/// Setter rødlistekategorien eller fremmedartsrisikoen som gjelder der observasjonen
/// ligger. Port av Artskart 2s <c>ImportStatusForRegion</c>.
///
/// AVHENGER AV TO TIDLIGERE STEG, og det er grunnen til at det er et PostTask:
/// taksonegenskapene kommer fra <c>ImportTaxonomyStep</c> via konteksten, og om
/// posten ligger på Svalbard leses av lokalitetens områder, satt av
/// <c>ImportGeometryStep</c>.
///
/// OG DET SER DEN MASKERTE POSISJONEN. <c>ImportCheckSensitiveSpecies</c> kjører før
/// dette steget og bytter ut lokaliteten med en maskert rutelokasjon (fase 8).
/// Svalbard-vurderingen gjøres altså på den maskerte posisjonen — med 16 km ruter kan
/// det i prinsippet flytte en observasjon over en regiongrense. Dagens oppførsel.
///
/// NORGE OG SVALBARD VURDERES HVER FOR SEG. Et takson kan ha én rødlistekategori for
/// fastlandet (<c>Context == "N"</c>) og en annen for Svalbard (<c>"S"</c>). Finnes
/// bare vurderingen for den andre regionen, blir kategorien <c>NE</c> — «ikke
/// vurdert» — og ikke den fra feil region.
///
/// NYESTE VURDERING VINNER. Prefikset bærer årstallet: <c>RL2015</c>, <c>FA2012</c>.
/// Sorteringen leser de fire siste tegnene som tall.
/// </summary>
public class ImportStatusForRegionStep : IImportStep
{
    /// <summary>Områdekoden for Svalbard i <c>Area.Fid</c>.</summary>
    private const string SvalbardFid = "21";

    /// <summary>Koden for «ikke vurdert». Id 1 i <c>Category</c>.</summary>
    private const string IkkeVurdert = "NE";

    private readonly ICategoryLookup _kategorier;

    public ImportStatusForRegionStep(ICategoryLookup kategorier)
    {
        _kategorier = kategorier;
    }

    public ImportStepType StepType => ImportStepType.Post;

    public ImportRecordResult ValidateAndImport(
        CacheRecord record,
        ImportedObservation observation,
        ImportContext context)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(context);

        if (context.TaxonProperties.Count == 0) return ImportRecordResult.Ok();

        var påSvalbard = observation.Location?.Areas.Any(x => x.Fid == SvalbardFid) ?? false;

        observation.CategoryId = FinnKategori(context.TaxonProperties, påSvalbard)?.Id;

        return ImportRecordResult.Ok();
    }

    /// <summary>
    /// Velger kategorien som gjelder for regionen posten ligger i.
    /// </summary>
    private CategoryRef? FinnKategori(IReadOnlyList<TaxonPropertyInfo> egenskaper, bool påSvalbard)
    {
        // Nyeste vurdering først. Årstallet er de fire siste tegnene i prefikset.
        var sortert = egenskaper
            .OrderByDescending(Årstall)
            .ToArray();

        var svalbard = VelgBlantKonflikter(sortert.Where(x => x.Context == "S").ToArray());
        var norge = VelgBlantKonflikter(sortert.Where(x => x.Context == "N").ToArray());

        if (svalbard is not null && norge is not null)
        {
            return HentKategori(påSvalbard ? svalbard.Property : norge.Property);
        }

        // Finnes bare vurderingen for den andre regionen, er taksonet ikke vurdert her.
        if (svalbard is not null)
        {
            return påSvalbard ? HentKategori(svalbard.Property) : HentKategori(IkkeVurdert);
        }

        if (norge is not null)
        {
            return påSvalbard ? HentKategori(IkkeVurdert) : HentKategori(norge.Property);
        }

        return null;
    }

    /// <summary>
    /// Et takson kan ha både en fremmedartsvurdering (<c>FA</c>) og en rødlistestatus
    /// (<c>RL</c>) for samme region. Denne velger hvilken som gjelder.
    ///
    /// Regelen er at en reell fremmedartsvurdering slår en rødlistestatus som bare sier
    /// «ikke egnet» eller «ikke vurdert» — en fremmed art er ikke rødlistet, og
    /// <c>NA</c>/<c>NE</c> er da den mindre opplysende av de to.
    ///
    /// ARTSKART 2 HAR FIRE TILFELLER HER, MEN BARE TO AV DEM KAN TREFFE: tilfelle 3 er
    /// identisk med tilfelle 1, og tilfelle 4 er en innsnevring av tilfelle 2, som alt
    /// har svart. De to siste er død kode. Rekkefølgen og utfallet er likevel det
    /// samme, så porten har de to som virker.
    /// </summary>
    private static TaxonPropertyInfo? VelgBlantKonflikter(TaxonPropertyInfo[] egenskaper)
    {
        if (egenskaper.Length == 0) return null;

        var harUegnetRødliste = egenskaper.Any(x =>
            Starter(x.Prefix, "RL") && (Starter(x.Property, "NA") || Starter(x.Property, "NE")));

        // «Ikke risikovurdert» som fremmed art slår «ikke egnet»/«ikke vurdert» i rødlista.
        var ikkeRisikovurdert = egenskaper.FirstOrDefault(x =>
            Starter(x.Prefix, "FA") && Starter(x.Property, "NR") && harUegnetRødliste);

        if (ikkeRisikovurdert is not null) return ikkeRisikovurdert;

        // En reell fremmedartsvurdering slår alt annet.
        var fremmedart = egenskaper.FirstOrDefault(x => Starter(x.Prefix, "FA") && !Starter(x.Property, "NR"));

        if (fremmedart is not null) return fremmedart;

        // Ellers den nyeste.
        return egenskaper[0];
    }

    /// <summary>
    /// Kategorien for en kode. Koden kuttes til to tegn, og <c>NR</c> — «ikke
    /// risikovurdert» — erstattes av <c>NE</c>: den sier at noen har sett på taksonet og
    /// konkludert med ingenting, og da er «ikke vurdert» det riktige å vise.
    /// </summary>
    private CategoryRef? HentKategori(string? kode)
    {
        if (string.IsNullOrWhiteSpace(kode)) return _kategorier.ByCode(IkkeVurdert);

        var kortKode = kode.Length > 2 ? kode[..2] : kode;

        var kategori = _kategorier.ByCode(kortKode);

        return kategori is not null && kategori.Code != "NR" ? kategori : _kategorier.ByCode(IkkeVurdert);
    }

    /// <summary>
    /// Årstallet i prefikset. Artskart 2 bruker <c>int.Parse</c> på de fire siste
    /// tegnene og kaster for et prefiks som ikke ender på fire sifre — et unntak som
    /// feller hele nodeimporten. Her sorteres slike bakerst i stedet.
    /// </summary>
    private static int Årstall(TaxonPropertyInfo egenskap)
    {
        var prefiks = egenskap.Prefix;

        if (prefiks is null || prefiks.Length < 4) return 0;

        return int.TryParse(prefiks[^4..], out var år) ? år : 0;
    }

    private static bool Starter(string? verdi, string prefiks) =>
        verdi is not null && verdi.StartsWith(prefiks, StringComparison.Ordinal);
}
