using System.Text.RegularExpressions;
using Artskart3.Core.Application.Import.Helpers;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Application.Import.Steps;

/// <summary>
/// Kopierer og tolker de ~40 enkle feltene. Port av Artskart 2s
/// <c>ImportSimpleProperties</c>.
///
/// DET TUNGSTE STEGET Å PORTERE, fordi nesten hver linje er en tolkning noen har tatt
/// et valg om. Trunkeringsgrensene er kolonnebreddene i <c>ObservationDetail</c>, og de
/// er verifisert mot <c>ArtskartDbContext</c> — <c>Habitat</c> 2000, <c>Collector</c>
/// 250, <c>Notes</c> 3500 og så videre.
///
/// LESER record.Notes OG SKRIVER DEN TIL OBSERVASJONEN. Stegene før dette —
/// <c>ImportBasisOfRecord</c>, <c>ImportBehavior</c>, <c>ImportGeometry</c> — skriver
/// sine forklaringer inn i <c>record.Notes</c>, og det er her de havner i
/// observasjonen. Derfor er rekkefølgen ikke valgfri.
///
/// SEKS STEDER DER OPPFØRSELEN ER RAR, OG BEVART:
///
/// 1. <c>organismQuantity</c> overskriver <c>individualCount</c> — og skrives i tillegg
///    som dynamisk egenskap.
/// 2. En ugyldig innsamlingsdato avviser posten, men en ugyldig bestemmelsesdato gir
///    bare advarsel — med varselkoden <c>DateTimeCollectedError</c>, altså feil kode.
/// 3. <c>introducedAssistedColonisation</c> i lista over verdier som gir
///    <c>Tag.Unspontaneus</c> kan aldri treffe: verdien lowercases før
///    sammenligningen, mens lista har stor forbokstav inne i ordet.
/// 4. <c>EstablishmentMeans</c> skrives som dynamisk egenskap med mindre verdien er
///    «unspontaneus» — med én s mindre enn «unspontaneous» i taggregelen over. Verdien
///    «unspontaneous» gir derfor både tagg og dynamisk egenskap.
/// 5. For node 40 og 211 TILORDNER Artskart 2 <c>AssociatedReferences</c> GBIF-URL-en
///    når kilden ikke har referanser, og kaster dermed det <c>ImportMediaStep</c> alt
///    har lagt inn. Det er det ene stedet dette steget avviker bevisst: her unioneres
///    det. Se kommentaren ved kallet.
/// 6. Dynamiske egenskaper legges inn med <c>Add</c>. Finnes nøkkelen fra før, kastes
///    det, og hele blokken ender i «NonValid dynamicProperties» på <c>Notes</c> — med
///    de parene som alt var lagt inn stående igjen.
/// </summary>
public class ImportSimplePropertiesStep : IImportStep
{
    /// <summary>
    /// Verdier av <c>establishmentMeans</c> som gir <c>Tag.Unspontaneus</c>. Lista er
    /// Artskart 2s, med stor C i midten — se punkt 3 i klassekommentaren.
    /// </summary>
    private static readonly string[] UspontanEtablering =
        ["unspontaneous", "introducedAssistedColonisation", "vagrant"];

    /// <summary>
    /// Historisk har både 1 og 0 blitt brukt i <c>identificationQualifier</c>, og
    /// «true»/«false» likeså. De sier ingenting og skal ikke lagres.
    /// </summary>
    private static readonly string[] UbrukeligeKvalifikatorer = ["0", "true", "false"];

    private static readonly Regex FlereMellomrom = new(@"\s+", RegexOptions.Compiled);

    public ImportStepType StepType => ImportStepType.Post;

    public ImportRecordResult ValidateAndImport(
        CacheRecord record,
        ImportedObservation observation,
        ImportContext context)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(context);

        var notificationCode = RecordNotificationCode.Ok;
        var validationCode = RecordValidationCode.Ok;

        SkrivFritekst(record, observation);

        var datoResultat = SkrivDatoer(record, observation, ref validationCode, ref notificationCode);
        if (datoResultat is not null) return datoResultat;

        SkrivReferanser(record, observation);
        SkrivTagger(record, observation);
        SkrivDynamiskeEgenskaper(record, observation, context);
        SkrivKortefelt(record, observation);
        SkrivDybdeOgHoyde(record, observation, context);
        SkrivKoblinger(record, observation);

        observation.HashCode = record.HashCode;

        return validationCode == RecordValidationCode.Ok
            ? ImportRecordResult.Ok(notificationCode)
            : ImportRecordResult.Warn(notificationCode, string.Empty);
    }

    private static void SkrivFritekst(CacheRecord record, ImportedObservation observation)
    {
        // Habitat, substrat og økologi er tre felt i kilden og én kolonne her, satt
        // sammen i denne rekkefølgen.
        if (!string.IsNullOrWhiteSpace(record.Habitat)
            || !string.IsNullOrWhiteSpace(record.Substrat)
            || !string.IsNullOrWhiteSpace(record.Okologi))
        {
            var deler = new List<string>();

            if (!string.IsNullOrWhiteSpace(record.Habitat)) deler.Add(record.Habitat.Trim());
            if (!string.IsNullOrWhiteSpace(record.Substrat)) deler.Add(record.Substrat.Trim());
            if (!string.IsNullOrWhiteSpace(record.Okologi)) deler.Add(record.Okologi.Trim());

            observation.Habitat = ImportValueHelper.DotLeft(string.Join(", ", deler), 2000);
        }

        if (!string.IsNullOrWhiteSpace(record.Collector))
            observation.Collector = ImportValueHelper.DotLeft(record.Collector.Trim(), 250);

        if (!string.IsNullOrWhiteSpace(record.IndividualCount))
            observation.IndividualCount = ImportValueHelper.DotLeft(record.IndividualCount.Trim(), 100);

        // Punkt 1: overskriver individualCount.
        if (!string.IsNullOrWhiteSpace(record.OrganismQuantity))
            observation.IndividualCount = ImportValueHelper.DotLeft(record.OrganismQuantity.Trim(), 100);

        // Her kommer meldingene de tidligere stegene skrev inn i record.Notes.
        if (!string.IsNullOrWhiteSpace(record.Notes))
            observation.Notes = ImportValueHelper.DotLeft(record.Notes.Trim(), 3500);

        if (!string.IsNullOrWhiteSpace(record.Sex))
            observation.Sex = ImportValueHelper.DotLeft(record.Sex.Trim(), 50);

        if (!string.IsNullOrWhiteSpace(record.IdentifiedBy))
            observation.IdentifiedBy = ImportValueHelper.DotLeft(record.IdentifiedBy.Trim(), 100);
    }

    /// <summary>
    /// Bygger de to datoene av år/måned/dag. Returnerer et avvisningsresultat hvis
    /// innsamlingsdatoen ikke kan bygges — den er med i søk og indekser, og en post uten
    /// gyldig dato er ikke brukbar.
    /// </summary>
    private static ImportRecordResult? SkrivDatoer(
        CacheRecord record,
        ImportedObservation observation,
        ref RecordValidationCode validationCode,
        ref RecordNotificationCode notificationCode)
    {
        // Darwin Core har både YearCollected og Year. Den første vinner når den er satt.
        int? år = record.YearCollected > 0 ? record.YearCollected : record.Year;
        var måned = record.MonthCollected > 0
            ? record.MonthCollected
            : record.Month is > 0 ? record.Month.Value : 0;
        var dag = record.DayCollected > 0
            ? record.DayCollected
            : record.Day is > 0 ? record.Day.Value : 0;

        // Kildens tall slik de kom, før de ble en dato. Mangler året, begynner strengen
        // med et mellomrom — det er dagens verdi i kolonnen.
        observation.DateTimeCollectedStr = FlereMellomrom.Replace($"{år} {måned} {dag}", " ");

        if (år is null or 0)
        {
            observation.DateTimeCollected = null;
        }
        else
        {
            try
            {
                observation.DateTimeCollected = new DateTime(år.Value, måned > 0 ? måned : 1, dag > 0 ? dag : 1);
            }
            catch (ArgumentOutOfRangeException e)
            {
                return ImportRecordResult.Rejected(
                    RecordValidationCode.Error,
                    RecordNotificationCode.DateTimeCollectedError,
                    e.Message);
            }
        }

        if (record.YearIdentified == 0 && record.MonthIdentified == 0 && record.DayIdentified == 0)
        {
            observation.DatetimeIdentified = null;
        }
        else
        {
            try
            {
                observation.DatetimeIdentified = new DateTime(
                    record.YearIdentified,
                    record.MonthIdentified > 0 ? record.MonthIdentified : 1,
                    record.DayIdentified > 0 ? record.DayIdentified : 1);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Punkt 2: advarsel, og med innsamlingsdatoens varselkode.
                observation.DatetimeIdentified = null;
                validationCode = RecordValidationCode.Warn;
                notificationCode = RecordNotificationCode.DateTimeCollectedError;
            }
        }

        return null;
    }

    private static void SkrivReferanser(CacheRecord record, ImportedObservation observation)
    {
        if (!string.IsNullOrWhiteSpace(record.AssociatedReferences))
        {
            foreach (var referanse in SplittPåSkilletegn(record.AssociatedReferences))
            {
                observation.AddAssociatedReference(referanse);
            }
        }
        else if (record.NodeDatabaseID is 40 or 211)
        {
            // BEVISST AVVIK FRA ARTSKART 2 (punkt 5 i klassekommentaren).
            //
            // Artskart 2 tilordner her i stedet for å unionere, og kaster dermed det
            // ImportMedia alt har lagt inn — media er et Parallel-steg og kjører før
            // dette. Målt på 20 000 poster fra node 40: 47 poster (0,24 %) har media
            // som verken er bilde eller lyd og ingen referanser fra kilden, og mister
            // altså referansen sin. Node 40 har 4 355 946 rader i cache-kopien.
            //
            // Rettingen er rent additiv: ingen rad mister noe, noen får én referanse
            // mer. Avviket føres i paritetsriggen.
            observation.AddAssociatedReference(
                "https://www.gbif.org/occurrence/" + record.CatalogNumber?.Trim());
        }

        if (record.UnknownFields is not null
            && record.UnknownFields.TryGetValue("references", out var ukjenteReferanser))
        {
            foreach (var referanse in SplittPåSkilletegn(ukjenteReferanser))
            {
                observation.AddAssociatedReference(referanse);
            }
        }
    }

    private static void SkrivTagger(CacheRecord record, ImportedObservation observation)
    {
        if (record.Antropokor
            || (!string.IsNullOrWhiteSpace(record.EstablishmentMeans)
                && UspontanEtablering.Contains(record.EstablishmentMeans.ToLowerInvariant())))
        {
            observation.AddTag(Tag.Unspontaneus);
        }

        if (string.Equals(record.IdentificationVerificationStatus?.Trim(), "validated",
                StringComparison.OrdinalIgnoreCase))
        {
            observation.AddTag(Tag.Validated);
        }

        if (string.Equals(record.OccurrenceStatus?.Trim(), "absent", StringComparison.OrdinalIgnoreCase))
        {
            observation.AddTag(Tag.Absent);
        }

        if (record.IdentificationPrecision
            || (!string.IsNullOrWhiteSpace(record.IdentificationQualifier) && record.IdentificationQualifier != "0"))
        {
            observation.AddTag(Tag.UnsureIdentification);
        }
    }

    private static void SkrivDynamiskeEgenskaper(
        CacheRecord record,
        ImportedObservation observation,
        ImportContext context)
    {
        TolkDynamicProperties(record, observation, context);

        // Målinger legges i dynamiske egenskaper med måletypen som nøkkel. «Verdi
        // enhet» trimmet, så en måling uten enhet ikke får et mellomrom bakerst.
        foreach (var måling in record.MeasurementOrFacts ?? [])
        {
            context.AppendDynamicProperty(
                måling.MeasurementType ?? string.Empty,
                $"{måling.MeasurementValue} {måling.MeasurementUnit}".Trim());
        }

        foreach (var måling in record.ExtendedMeasurementOrFacts ?? [])
        {
            context.AppendDynamicProperty(
                måling.MeasurementType ?? string.Empty,
                $"{måling.MeasurementValue} {måling.MeasurementUnit}".Trim());
        }

        // Punkt 4: «unspontaneus» med én s mindre enn i taggregelen.
        if (!string.IsNullOrWhiteSpace(record.EstablishmentMeans)
            && record.EstablishmentMeans.ToLowerInvariant() != "unspontaneus")
        {
            context.AppendDynamicProperty("EstablishmentMeans", record.EstablishmentMeans);
        }

        LeggTilHvisUtfylt(context, "EventID", record.EventID);
        LeggTilHvisUtfylt(context, "ParentEventID", record.ParentEventID);
        LeggTilHvisUtfylt(context, "FieldNotes", record.FieldNotes);
        LeggTilHvisUtfylt(context, "EventDate", record.EventDate);
        LeggTilHvisUtfylt(context, "EventTime", record.EventTime);
        LeggTilHvisUtfylt(context, "EventRemarks", record.EventRemarks);
        LeggTilHvisUtfylt(context, "VerbatimEventDate", record.VerbatimEventDate);
        LeggTilHvisUtfylt(context, "SamplingProtocol", record.SamplingProtocol);
        LeggTilHvisUtfylt(context, "SamplingEffort", record.SamplingEffort);
        LeggTilHvisUtfylt(context, "SampleSizeValue", record.SampleSizeValue);
        LeggTilHvisUtfylt(context, "SampleSizeUnit", record.SampleSizeUnit);

        if (record.Day.HasValue) context.AppendDynamicProperty("Day", record.Day.Value.ToString());
        if (record.Month.HasValue) context.AppendDynamicProperty("Month", record.Month.Value.ToString());
        if (record.Year.HasValue) context.AppendDynamicProperty("Year", record.Year.Value.ToString());
        if (record.StartDayOfYear.HasValue)
            context.AppendDynamicProperty("StartDayOfYear", record.StartDayOfYear.Value.ToString());
        if (record.EndDayOfYear.HasValue)
            context.AppendDynamicProperty("EndDayOfYear", record.EndDayOfYear.Value.ToString());

        if (!string.IsNullOrWhiteSpace(record.ContinentOcean))
            context.AppendDynamicProperty("ContinentOcean", record.ContinentOcean.Trim());
        if (!string.IsNullOrWhiteSpace(record.OrganismQuantity))
            context.AppendDynamicProperty("OrganismQuantity", record.OrganismQuantity.Trim());
        if (!string.IsNullOrWhiteSpace(record.OrganismQuantityType))
            context.AppendDynamicProperty("OrganismQuantityType", record.OrganismQuantityType.Trim());

        if (!string.IsNullOrWhiteSpace(record.IdentificationQualifier)
            && !UbrukeligeKvalifikatorer.Contains(record.IdentificationQualifier))
        {
            context.AppendDynamicProperty("IdentificationQualifier", record.IdentificationQualifier.Trim());
        }

        if (!string.IsNullOrWhiteSpace(record.IdentificationRemarks))
            context.AppendDynamicProperty("IdentificationRemarks", record.IdentificationRemarks.Trim());

        // Alt harvest ikke kjente igjen blir med som det er. Det er her feltene
        // importen leser særskilt — municipality, species, references — også ligger.
        foreach (var felt in record.UnknownFields ?? [])
        {
            context.AppendDynamicProperty(felt.Key, felt.Value);
        }
    }

    /// <summary>
    /// <c>dynamicProperties</c> kommer i tre former: JSON, «nøkkel:verdi;nøkkel:verdi»,
    /// og ugyldig. Artskart 2 skiller på om strengen inneholder <c>{</c> eller <c>"</c>.
    /// </summary>
    private static void TolkDynamicProperties(
        CacheRecord record,
        ImportedObservation observation,
        ImportContext context)
    {
        if (string.IsNullOrWhiteSpace(record.DynamicProperties)) return;

        var tekst = ImportValueHelper.JsonSanityFixes(record.DynamicProperties);
        if (string.IsNullOrWhiteSpace(tekst)) return;

        if (!tekst.Contains('{') && !tekst.Contains('"'))
        {
            // «Life Stage Remarks: none recorded;Mode of Acquisition:Purchased» — og
            // varianten som bruker = i stedet for :
            var elementer = tekst.Replace("=", ":").Split(';', StringSplitOptions.RemoveEmptyEntries);

            foreach (var element in elementer)
            {
                var par = element.Split(':', StringSplitOptions.RemoveEmptyEntries);

                if (par.Length == 2) context.AppendDynamicProperty(par[0], par[1]);
            }

            return;
        }

        // gbifissue er en array og ikke en streng, og velter deserialiseringen.
        // Artskart 2 hopper over hele feltet da.
        if (tekst.Contains("gbifissue")) return;

        try
        {
            var lest = ImportValueHelper.ReadJsonAsTextPairs(tekst, "dynamicProperties");

            foreach (var par in lest)
            {
                // Punkt 6: Add og ikke tilordning — kolliderer nøkkelen, kastes det.
                context.DynamicProperties.Add(par.Key, par.Value);
            }

            if (lest.TryGetValue("absenceState", out var fravær)
                && string.Equals(fravær?.Trim(), "notrecovered", StringComparison.OrdinalIgnoreCase))
            {
                observation.AddTag(Tag.NotRecovered);

                // Trenger ikke begge.
                observation.RemoveTag(Tag.Absent);
            }
        }
        catch (Exception)
        {
            observation.AppendNotes(" NonValid dynamicProperties: " + tekst);
        }
    }

    private static void SkrivKortefelt(CacheRecord record, ImportedObservation observation)
    {
        if (!string.IsNullOrWhiteSpace(record.CollectingMethod))
            observation.CollectingMethod = ImportValueHelper.DotLeft(record.CollectingMethod.Trim(), 100);

        if (!string.IsNullOrWhiteSpace(record.CollectorNumber))
            observation.RecordNumber = ImportValueHelper.DotLeft(record.CollectorNumber.Trim(), 50);

        // Prefikset er en del av verdien, og grensen er derfor 89 og ikke 100.
        if (!string.IsNullOrWhiteSpace(record.ElevationKilde))
            observation.MeasurementMethod = "Elevation: " + ImportValueHelper.DotLeft(record.ElevationKilde.Trim(), 89);

        if (!string.IsNullOrWhiteSpace(record.FieldNumber))
            observation.FieldNumber = ImportValueHelper.DotLeft(record.FieldNumber.Trim(), 100);

        if (!string.IsNullOrWhiteSpace(record.KoordinatKilde))
            observation.GeoreferenceRemarks = ImportValueHelper.DotLeft(record.KoordinatKilde.Trim(), 50);

        if (!string.IsNullOrWhiteSpace(record.PreparationType))
            observation.Preparations = ImportValueHelper.DotLeft(record.PreparationType.Trim(), 200);

        if (!string.IsNullOrWhiteSpace(record.PreviousCatalogNumber))
            observation.OtherCatalogNumbers = ImportValueHelper.DotLeft(record.PreviousCatalogNumber.Trim(), 200);

        if (!string.IsNullOrWhiteSpace(record.RelatedCatalogItem))
            observation.RelatedResourceId = ImportValueHelper.DotLeft(record.RelatedCatalogItem.Trim(), 50);

        if (!string.IsNullOrWhiteSpace(record.RelationshipType))
            observation.RelationshipOfResource = ImportValueHelper.DotLeft(record.RelationshipType.Trim(), 50);

        if (!string.IsNullOrWhiteSpace(record.TypeStatus))
            observation.TypeStatus = ImportValueHelper.DotLeft(record.TypeStatus.Trim(), 50);

        if (!string.IsNullOrWhiteSpace(record.TimeOfDay))
            observation.EventTime = ImportValueHelper.DotLeft(record.TimeOfDay.Trim(), 100);
    }

    private static void SkrivDybdeOgHoyde(
        CacheRecord record,
        ImportedObservation observation,
        ImportContext context)
    {
        // Dybde lagres som tekst med prefiks, fordi kilden kan oppgi intervaller og
        // omtrentlige verdier som ikke blir tall.
        if (!string.IsNullOrWhiteSpace(record.MaximumDepth))
            observation.VerbatimDepth = "max: " + ImportValueHelper.DotLeft(record.MaximumDepth.Trim(), 95);

        if (!string.IsNullOrWhiteSpace(record.MinimumDepthInMeters))
        {
            observation.VerbatimDepth = string.IsNullOrWhiteSpace(observation.VerbatimDepth)
                ? "min: " + record.MinimumDepthInMeters.Trim()
                : observation.VerbatimDepth + ", min: " + record.MinimumDepthInMeters.Trim();

            observation.VerbatimDepth = ImportValueHelper.DotLeft(observation.VerbatimDepth, 100);
        }

        observation.MaximumElevationInMeters = ImportValueHelper.ExtractIntegerIfPossible(
            record.MaximumElevationInMeters, "MaximumElevationInMeters", context.RecordIssues);

        observation.MinimumElevationInMeters = ImportValueHelper.ExtractIntegerIfPossible(
            record.MinimumElevationInMeters, "MinimumElevationInMeters", context.RecordIssues);
    }

    /// <summary>
    /// <c>associatedOccurrences</c> blir koblinger til andre observasjoner. Feltet er
    /// fritekst i praksis, så Artskart 2 prøver tre tolkninger og lagrer råteksten som
    /// merknad hvis ingen av dem går.
    ///
    /// Koblingen får bare <c>Remarks</c>. Den andre observasjonen slås ikke opp — den
    /// finnes ikke nødvendigvis ennå.
    /// </summary>
    private static void SkrivKoblinger(CacheRecord record, ImportedObservation observation)
    {
        if (string.IsNullOrWhiteSpace(record.AssociatedOccurrences)) return;

        var tekst = ImportValueHelper.JsonSanityFixes(record.AssociatedOccurrences);
        if (string.IsNullOrWhiteSpace(tekst)) return;

        string[] kjenteRelasjoner = ["herbariumSpecimenDuplicate", "subsampleOf", "originatingSampleOf"];

        try
        {
            if (kjenteRelasjoner.Any(x => tekst.Contains(x, StringComparison.Ordinal)))
            {
                if (tekst.Contains('|')) tekst = tekst.Replace("|", ", ");

                foreach (var linje in tekst.Split(','))
                {
                    var deler = linje.Split(':');

                    if (kjenteRelasjoner.Contains(deler[0].Trim(), StringComparer.Ordinal))
                    {
                        observation.Links.Add(new ImportedObservationLink
                        {
                            Remarks = string.Join(":", deler.Skip(1)).Trim()
                        });
                    }
                }

                return;
            }

            if (!tekst.Contains('{')) tekst = "{" + tekst + "}";

            var lest = ImportValueHelper.ReadJsonAsTextPairs(tekst, "dynamicProperties");

            if (lest.TryGetValue("same occurrence as", out var samme))
            {
                observation.Links.Add(new ImportedObservationLink { Remarks = samme });
            }
        }
        catch (Exception)
        {
            // Råteksten er bedre enn ingenting: noen må kunne se hva kilden sendte.
            observation.Links.Add(new ImportedObservationLink { Remarks = tekst });
        }
    }

    private static void LeggTilHvisUtfylt(ImportContext context, string nøkkel, string? verdi)
    {
        if (!string.IsNullOrWhiteSpace(verdi)) context.AppendDynamicProperty(nøkkel, verdi);
    }

    /// <summary>Deler på <c>;</c> og <c>|</c> og trimmer, som <c>ConcanatedStringToArray</c>.</summary>
    private static string[] SplittPåSkilletegn(string? value) =>
        string.IsNullOrEmpty(value)
            ? []
            : value.Split([';', '|'], StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .ToArray();
}
