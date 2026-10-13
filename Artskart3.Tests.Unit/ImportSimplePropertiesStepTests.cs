using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Application.Import.Steps;
using Artskart3.Core.Domain.Enums;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class ImportSimplePropertiesStepTests
{
    private readonly ImportSimplePropertiesStep _steg = new();
    private readonly ImportedObservation _observasjon = new();
    private readonly ImportContext _context = new();

    private ImportRecordResult Kjør(CacheRecord record) =>
        _steg.ValidateAndImport(record, _observasjon, _context);

    [Fact]
    public void Er_et_post_steg()
    {
        _steg.StepType.Should().Be(ImportStepType.Post);
    }

    // --- Fritekst -----------------------------------------------------------

    /// <summary>Tre felt i kilden, én kolonne her, i rekkefølgen habitat, substrat, økologi.</summary>
    [Fact]
    public void Setter_sammen_habitat_substrat_og_okologi()
    {
        Kjør(new CacheRecord { Habitat = " skog ", Substrat = "død ved", Okologi = "fuktig" });

        _observasjon.Habitat.Should().Be("skog, død ved, fuktig");
    }

    [Fact]
    public void Lar_habitat_vaere_null_naar_alle_tre_mangler()
    {
        Kjør(new CacheRecord());

        _observasjon.Habitat.Should().BeNull();
    }

    [Fact]
    public void Kopierer_notes_fra_posten()
    {
        // Her kommer meldingene de tidligere stegene skrev inn i record.Notes.
        Kjør(new CacheRecord { Notes = "  Ukjent verdi for BasisOfRecord.  " });

        _observasjon.Notes.Should().Be("Ukjent verdi for BasisOfRecord.");
    }

    [Fact]
    public void Trunkerer_de_korte_tekstfeltene()
    {
        var lang = new string('x', 400);

        Kjør(new CacheRecord { Collector = lang, Sex = lang, IdentifiedBy = lang });

        _observasjon.Collector.Should().HaveLength(250);
        _observasjon.Sex.Should().HaveLength(50);
        _observasjon.IdentifiedBy.Should().HaveLength(100);
        _observasjon.Collector.Should().EndWith("...");
    }

    /// <summary>
    /// <c>organismQuantity</c> overskriver <c>individualCount</c> — og skrives i tillegg
    /// som dynamisk egenskap.
    /// </summary>
    [Fact]
    public void OrganismQuantity_overskriver_individualCount()
    {
        Kjør(new CacheRecord { IndividualCount = "3", OrganismQuantity = "12" });

        _observasjon.IndividualCount.Should().Be("12");
        _context.DynamicProperties["OrganismQuantity"].Should().Be("12");
    }

    // --- Datoer -------------------------------------------------------------

    [Fact]
    public void Bygger_innsamlingsdato_av_ar_maned_og_dag()
    {
        Kjør(new CacheRecord { YearCollected = 2024, MonthCollected = 5, DayCollected = 17 });

        _observasjon.DateTimeCollected.Should().Be(new DateTime(2024, 5, 17));
        _observasjon.DateTimeCollectedStr.Should().Be("2024 5 17");
    }

    [Fact]
    public void Bruker_Year_Month_Day_naar_Collected_feltene_mangler()
    {
        Kjør(new CacheRecord { Year = 2024, Month = 5, Day = 17 });

        _observasjon.DateTimeCollected.Should().Be(new DateTime(2024, 5, 17));
    }

    [Fact]
    public void Lar_Collected_feltene_vinne_over_Year_Month_Day()
    {
        Kjør(new CacheRecord
        {
            YearCollected = 2024, MonthCollected = 5, DayCollected = 17,
            Year = 1999, Month = 1, Day = 1
        });

        _observasjon.DateTimeCollected.Should().Be(new DateTime(2024, 5, 17));
    }

    [Fact]
    public void Setter_forste_i_maneden_naar_dagen_mangler()
    {
        Kjør(new CacheRecord { YearCollected = 2024, MonthCollected = 5 });

        _observasjon.DateTimeCollected.Should().Be(new DateTime(2024, 5, 1));
        _observasjon.DateTimeCollectedStr.Should().Be("2024 5 0", "strengen bærer kildens tall, ikke datoens");
    }

    [Fact]
    public void Gir_ingen_innsamlingsdato_uten_ar()
    {
        Kjør(new CacheRecord { MonthCollected = 5, DayCollected = 17 });

        _observasjon.DateTimeCollected.Should().BeNull();
    }

    /// <summary>Mangler året, begynner strengen med et mellomrom. Det er dagens verdi i kolonnen.</summary>
    [Fact]
    public void DateTimeCollectedStr_begynner_med_mellomrom_uten_ar()
    {
        Kjør(new CacheRecord());

        _observasjon.DateTimeCollectedStr.Should().Be(" 0 0");
    }

    /// <summary>
    /// Datoen er med i søk og indekser, så en post med ugyldig dato er ikke brukbar.
    /// </summary>
    [Fact]
    public void Avviser_posten_naar_innsamlingsdatoen_er_ugyldig()
    {
        var resultat = Kjør(new CacheRecord { YearCollected = 2024, MonthCollected = 13, DayCollected = 1 });

        resultat.IsAccepted.Should().BeFalse();
        resultat.ValidationCode.Should().Be(RecordValidationCode.Error);
        resultat.NotificationCode.Should().Be(RecordNotificationCode.DateTimeCollectedError);
    }

    [Fact]
    public void Bygger_bestemmelsesdato()
    {
        Kjør(new CacheRecord { YearIdentified = 2025, MonthIdentified = 3, DayIdentified = 2 });

        _observasjon.DatetimeIdentified.Should().Be(new DateTime(2025, 3, 2));
    }

    [Fact]
    public void Gir_ingen_bestemmelsesdato_naar_alle_tre_er_null()
    {
        Kjør(new CacheRecord());

        _observasjon.DatetimeIdentified.Should().BeNull();
    }

    /// <summary>
    /// Ugyldig bestemmelsesdato gir bare advarsel — og med innsamlingsdatoens varselkode,
    /// som er feil kode. Bevart fordi den ligger i avvisningsrapportene.
    /// </summary>
    [Fact]
    public void Advarer_men_avviser_ikke_naar_bestemmelsesdatoen_er_ugyldig()
    {
        var resultat = Kjør(new CacheRecord { MonthIdentified = 5, DayIdentified = 17 });

        resultat.ValidationCode.Should().Be(RecordValidationCode.Warn);
        resultat.NotificationCode.Should().Be(RecordNotificationCode.DateTimeCollectedError);
        _observasjon.DatetimeIdentified.Should().BeNull();
    }

    // --- Referanser ---------------------------------------------------------

    [Fact]
    public void Legger_til_referanser_fra_kilden()
    {
        Kjør(new CacheRecord { AssociatedReferences = "https://a.example; https://b.example" });

        _observasjon.AssociatedReferences.Should().Equal(["https://a.example", "https://b.example"]);
    }

    [Fact]
    public void Legger_til_referanser_fra_ukjente_felt()
    {
        Kjør(new CacheRecord
        {
            AssociatedReferences = "https://a.example",
            UnknownFields = new Dictionary<string, string> { ["references"] = "https://b.example" }
        });

        _observasjon.AssociatedReferences.Should().Equal(["https://a.example", "https://b.example"]);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(211)]
    public void Setter_gbif_url_som_referanse_for_gbif_nodene(int node)
    {
        Kjør(new CacheRecord { NodeDatabaseID = node, CatalogNumber = " 12345 " });

        _observasjon.AssociatedReferences.Should().Equal(["https://www.gbif.org/occurrence/12345"]);
    }

    /// <summary>
    /// BEVISST AVVIK FRA ARTSKART 2. Der tilordnes GBIF-URL-en, og det kaster
    /// mediereferansene <c>ImportMediaStep</c> alt har lagt inn — media er et
    /// Parallel-steg og kjører før dette. Her unioneres det, og ingen rad mister noe.
    /// Målt: 47 av 20 000 poster i node 40 rammes av tapet i Artskart 2.
    /// </summary>
    [Fact]
    public void Gbif_url_beholder_referanser_media_alt_har_lagt_inn()
    {
        _observasjon.AddAssociatedReference("https://example.org/artikkel.pdf");

        Kjør(new CacheRecord { NodeDatabaseID = 40, CatalogNumber = "12345" });

        _observasjon.AssociatedReferences.Should().Equal([
            "https://example.org/artikkel.pdf",
            "https://www.gbif.org/occurrence/12345"
        ]);
    }

    [Fact]
    public void Rorer_ikke_mediereferanser_for_andre_noder()
    {
        _observasjon.AddAssociatedReference("https://example.org/artikkel.pdf");

        Kjør(new CacheRecord { NodeDatabaseID = 43, CatalogNumber = "12345" });

        _observasjon.AssociatedReferences.Should().Equal(["https://example.org/artikkel.pdf"]);
    }

    // --- Tagger -------------------------------------------------------------

    [Fact]
    public void Setter_uspontan_tagg_av_antropokor()
    {
        Kjør(new CacheRecord { Antropokor = true });

        _observasjon.HasTag(Tag.Unspontaneus).Should().BeTrue();
    }

    [Theory]
    [InlineData("unspontaneous")]
    [InlineData("UNSPONTANEOUS")]
    [InlineData("vagrant")]
    public void Setter_uspontan_tagg_av_etableringsmate(string verdi)
    {
        Kjør(new CacheRecord { EstablishmentMeans = verdi });

        _observasjon.HasTag(Tag.Unspontaneus).Should().BeTrue();
    }

    /// <summary>
    /// Verdien lowercases før sammenligningen, mens lista i Artskart 2 har stor C inne i
    /// ordet. Regelen kan derfor aldri treffe — dokumentert med en test slik at en
    /// framtidig retting blir et valg.
    /// </summary>
    [Fact]
    public void Treffer_aldri_paa_introducedAssistedColonisation()
    {
        Kjør(new CacheRecord { EstablishmentMeans = "introducedAssistedColonisation" });

        _observasjon.HasTag(Tag.Unspontaneus).Should().BeFalse();
    }

    /// <summary>
    /// Unntaket for den dynamiske egenskapen staves «unspontaneus», med én s mindre enn
    /// taggregelens «unspontaneous». Verdien «unspontaneous» gir derfor begge.
    /// </summary>
    [Fact]
    public void Etableringsmate_unspontaneous_gir_bade_tagg_og_dynamisk_egenskap()
    {
        Kjør(new CacheRecord { EstablishmentMeans = "unspontaneous" });

        _observasjon.HasTag(Tag.Unspontaneus).Should().BeTrue();
        _context.DynamicProperties.Should().ContainKey("EstablishmentMeans");
    }

    [Fact]
    public void Etableringsmate_unspontaneus_gir_ingen_dynamisk_egenskap()
    {
        Kjør(new CacheRecord { EstablishmentMeans = "unspontaneus" });

        _context.DynamicProperties.Should().NotContainKey("EstablishmentMeans");
    }

    [Theory]
    [InlineData("validated", true)]
    [InlineData(" Validated ", true)]
    [InlineData("unvalidated", false)]
    public void Setter_validert_tagg(string verdi, bool forventet)
    {
        Kjør(new CacheRecord { IdentificationVerificationStatus = verdi });

        _observasjon.HasTag(Tag.Validated).Should().Be(forventet);
    }

    [Theory]
    [InlineData("absent", true)]
    [InlineData(" ABSENT ", true)]
    [InlineData("present", false)]
    public void Setter_fravaerstagg(string verdi, bool forventet)
    {
        Kjør(new CacheRecord { OccurrenceStatus = verdi });

        _observasjon.HasTag(Tag.Absent).Should().Be(forventet);
    }

    [Fact]
    public void Setter_usikker_bestemmelse_av_flagget_taksonomisteget_satte()
    {
        Kjør(new CacheRecord { IdentificationPrecision = true });

        _observasjon.HasTag(Tag.UnsureIdentification).Should().BeTrue();
    }

    [Theory]
    [InlineData("cf", true)]
    [InlineData("0", false)]
    public void Setter_usikker_bestemmelse_av_kvalifikator(string verdi, bool forventet)
    {
        Kjør(new CacheRecord { IdentificationQualifier = verdi });

        _observasjon.HasTag(Tag.UnsureIdentification).Should().Be(forventet);
    }

    // --- Dynamiske egenskaper ----------------------------------------------

    [Fact]
    public void Leser_dynamicProperties_som_json()
    {
        Kjør(new CacheRecord { DynamicProperties = """{"lifeStage":"adult","sex":"male"}""" });

        _context.DynamicProperties["lifeStage"].Should().Be("adult");
        _context.DynamicProperties["sex"].Should().Be("male");
    }

    /// <summary>
    /// <c>JavaScriptSerializer</c> i Artskart 2 konverterte tall og bool til tekst. Bool
    /// ble <c>True</c>/<c>False</c>, ikke JSON-ens små bokstaver.
    /// </summary>
    [Fact]
    public void Konverterer_tall_og_bool_i_dynamicProperties_til_tekst()
    {
        Kjør(new CacheRecord { DynamicProperties = """{"antall":12,"sikker":true,"usikker":false}""" });

        _context.DynamicProperties["antall"].Should().Be("12");
        _context.DynamicProperties["sikker"].Should().Be("True");
        _context.DynamicProperties["usikker"].Should().Be("False");
    }

    /// <summary>Formen uten krøllparentes og anførselstegn: «nøkkel: verdi;nøkkel: verdi».</summary>
    [Fact]
    public void Leser_dynamicProperties_som_semikolonliste()
    {
        Kjør(new CacheRecord
        {
            DynamicProperties = "Life Stage Remarks: none recorded;Mode of Acquisition:Purchased"
        });

        _context.DynamicProperties["Life Stage Remarks"].Should().Be(" none recorded");
        _context.DynamicProperties["Mode of Acquisition"].Should().Be("Purchased");
    }

    [Fact]
    public void Leser_dynamicProperties_med_likhetstegn_som_skilletegn()
    {
        Kjør(new CacheRecord { DynamicProperties = "stadium=voksen;kjonn=hann" });

        _context.DynamicProperties["stadium"].Should().Be("voksen");
        _context.DynamicProperties["kjonn"].Should().Be("hann");
    }

    /// <summary>gbifissue er en array og velter deserialiseringen. Hele feltet hoppes over.</summary>
    [Fact]
    public void Hopper_over_dynamicProperties_med_gbifissue()
    {
        Kjør(new CacheRecord { DynamicProperties = """{"gbifissue":["a","b"],"lifeStage":"adult"}""" });

        _context.DynamicProperties.Should().BeEmpty();
        _observasjon.Notes.Should().BeNull();
    }

    [Fact]
    public void Skriver_merknad_i_notes_for_ugyldig_dynamicProperties()
    {
        Kjør(new CacheRecord { DynamicProperties = """{"ikke":json""" });

        _observasjon.Notes.Should().StartWith(" NonValid dynamicProperties: ");
    }

    /// <summary>
    /// Nøklene legges inn med <c>Add</c>. Kolliderer en av dem med noe som alt står i
    /// konteksten, kastes det — og de parene som rakk å bli lagt inn står igjen.
    /// </summary>
    [Fact]
    public void Kolliderende_nokkel_gir_merknad_og_etterlater_delvis_resultat()
    {
        _context.AppendDynamicProperty("b", "fra før");

        Kjør(new CacheRecord { DynamicProperties = """{"a":"1","b":"2","c":"3"}""" });

        _context.DynamicProperties["a"].Should().Be("1");
        _context.DynamicProperties["b"].Should().Be("fra før");
        _context.DynamicProperties.Should().NotContainKey("c");
        _observasjon.Notes.Should().Contain("NonValid dynamicProperties");
    }

    [Fact]
    public void AbsenceState_notrecovered_bytter_fravaerstagg_til_ikke_gjenfunnet()
    {
        Kjør(new CacheRecord
        {
            OccurrenceStatus = "absent",
            DynamicProperties = """{"absenceState":"notrecovered"}"""
        });

        _observasjon.HasTag(Tag.NotRecovered).Should().BeTrue();
        _observasjon.HasTag(Tag.Absent).Should().BeFalse("vi trenger ikke begge");
    }

    [Fact]
    public void Legger_malinger_i_dynamiske_egenskaper()
    {
        Kjør(new CacheRecord
        {
            MeasurementOrFacts =
            [
                new CacheMeasurementOrFact
                {
                    MeasurementType = "lengde", MeasurementValue = "12", MeasurementUnit = "cm"
                }
            ],
            ExtendedMeasurementOrFacts =
            [
                new CacheExtendedMeasurementOrFact
                {
                    MeasurementType = "temperatur", MeasurementValue = "4", MeasurementUnit = "C"
                }
            ]
        });

        _context.DynamicProperties["lengde"].Should().Be("12 cm");
        _context.DynamicProperties["temperatur"].Should().Be("4 C");
    }

    [Fact]
    public void Maling_uten_enhet_far_ikke_mellomrom_bakerst()
    {
        Kjør(new CacheRecord
        {
            MeasurementOrFacts =
            [
                new CacheMeasurementOrFact { MeasurementType = "lengde", MeasurementValue = "12" }
            ]
        });

        _context.DynamicProperties["lengde"].Should().Be("12");
    }

    [Fact]
    public void Legger_hendelsesfeltene_i_dynamiske_egenskaper()
    {
        Kjør(new CacheRecord
        {
            EventID = "e1",
            ParentEventID = "e0",
            FieldNotes = "notater",
            EventDate = "2024-05-17",
            EventTime = "14:00",
            EventRemarks = "fint vær",
            VerbatimEventDate = "17. mai 2024",
            SamplingProtocol = "håndplukking",
            SamplingEffort = "2 timer",
            SampleSizeValue = "5",
            SampleSizeUnit = "m2"
        });

        _context.DynamicProperties.Should().Contain(new Dictionary<string, string?>
        {
            ["EventID"] = "e1",
            ["ParentEventID"] = "e0",
            ["FieldNotes"] = "notater",
            ["EventDate"] = "2024-05-17",
            ["EventTime"] = "14:00",
            ["EventRemarks"] = "fint vær",
            ["VerbatimEventDate"] = "17. mai 2024",
            ["SamplingProtocol"] = "håndplukking",
            ["SamplingEffort"] = "2 timer",
            ["SampleSizeValue"] = "5",
            ["SampleSizeUnit"] = "m2"
        });
    }

    [Fact]
    public void Legger_dagnummer_og_datodeler_i_dynamiske_egenskaper()
    {
        Kjør(new CacheRecord { Day = 17, Month = 5, Year = 2024, StartDayOfYear = 138, EndDayOfYear = 138 });

        _context.DynamicProperties["Day"].Should().Be("17");
        _context.DynamicProperties["Month"].Should().Be("5");
        _context.DynamicProperties["Year"].Should().Be("2024");
        _context.DynamicProperties["StartDayOfYear"].Should().Be("138");
        _context.DynamicProperties["EndDayOfYear"].Should().Be("138");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("true")]
    [InlineData("false")]
    public void Lagrer_ikke_de_ubrukelige_kvalifikatorene(string verdi)
    {
        Kjør(new CacheRecord { IdentificationQualifier = verdi });

        _context.DynamicProperties.Should().NotContainKey("IdentificationQualifier");
    }

    [Fact]
    public void Tar_med_alle_ukjente_felt()
    {
        Kjør(new CacheRecord
        {
            UnknownFields = new Dictionary<string, string> { ["municipality"] = "Oslo", ["waterBody"] = "Oslofjorden" }
        });

        _context.DynamicProperties["municipality"].Should().Be("Oslo");
        _context.DynamicProperties["waterBody"].Should().Be("Oslofjorden");
    }

    // --- Korte felt, dybde og høyde ----------------------------------------

    [Fact]
    public void Kopierer_de_korte_feltene_med_sine_grenser()
    {
        Kjør(new CacheRecord
        {
            CollectingMethod = " håndplukking ",
            CollectorNumber = "nr 5",
            FieldNumber = "felt 7",
            KoordinatKilde = "GPS",
            PreparationType = "tørket",
            PreviousCatalogNumber = "gammelt-1",
            RelatedCatalogItem = "rel-1",
            RelationshipType = "duplikat",
            TypeStatus = "holotype",
            TimeOfDay = "14:00"
        });

        _observasjon.CollectingMethod.Should().Be("håndplukking");
        _observasjon.RecordNumber.Should().Be("nr 5");
        _observasjon.FieldNumber.Should().Be("felt 7");
        _observasjon.GeoreferenceRemarks.Should().Be("GPS");
        _observasjon.Preparations.Should().Be("tørket");
        _observasjon.OtherCatalogNumbers.Should().Be("gammelt-1");
        _observasjon.RelatedResourceId.Should().Be("rel-1");
        _observasjon.RelationshipOfResource.Should().Be("duplikat");
        _observasjon.TypeStatus.Should().Be("holotype");
        _observasjon.EventTime.Should().Be("14:00");
    }

    /// <summary>Prefikset er en del av verdien, så grensen er 89 og ikke 100.</summary>
    [Fact]
    public void Setter_hoydekilde_med_prefiks()
    {
        Kjør(new CacheRecord { ElevationKilde = "kart" });

        _observasjon.MeasurementMethod.Should().Be("Elevation: kart");
    }

    [Fact]
    public void Setter_dybde_med_prefiks_for_bade_maks_og_min()
    {
        Kjør(new CacheRecord { MaximumDepth = "20", MinimumDepthInMeters = "5" });

        _observasjon.VerbatimDepth.Should().Be("max: 20, min: 5");
    }

    [Fact]
    public void Setter_bare_min_naar_maks_mangler()
    {
        Kjør(new CacheRecord { MinimumDepthInMeters = "5" });

        _observasjon.VerbatimDepth.Should().Be("min: 5");
    }

    [Fact]
    public void Tolker_hoyde_som_tall()
    {
        Kjør(new CacheRecord { MaximumElevationInMeters = "1200 m", MinimumElevationInMeters = "ca 800" });

        _observasjon.MaximumElevationInMeters.Should().Be(1200);
        _observasjon.MinimumElevationInMeters.Should().Be(800);
        _context.RecordIssues.Should().BeEmpty();
    }

    [Fact]
    public void Gir_merknad_for_hoyde_som_ikke_kan_tolkes()
    {
        Kjør(new CacheRecord { MaximumElevationInMeters = "høyt oppe" });

        _observasjon.MaximumElevationInMeters.Should().BeNull();
        _context.RecordIssues.Should().ContainSingle(x => x.Term == "MaximumElevationInMeters");
    }

    // --- Koblinger ----------------------------------------------------------

    [Fact]
    public void Lager_kobling_av_kjent_relasjon()
    {
        Kjør(new CacheRecord { AssociatedOccurrences = "herbariumSpecimenDuplicate: O-123" });

        _observasjon.Links.Should().ContainSingle();
        _observasjon.Links[0].Remarks.Should().Be("O-123");
    }

    [Fact]
    public void Lager_en_kobling_per_kjent_relasjon_i_lista()
    {
        Kjør(new CacheRecord
        {
            AssociatedOccurrences = "herbariumSpecimenDuplicate: O-123|subsampleOf: O-124"
        });

        _observasjon.Links.Should().HaveCount(2);
        _observasjon.Links.Select(x => x.Remarks).Should().Equal(["O-123", "O-124"]);
    }

    [Fact]
    public void Lager_kobling_av_same_occurrence_as()
    {
        Kjør(new CacheRecord { AssociatedOccurrences = """{"same occurrence as":"O-999"}""" });

        _observasjon.Links.Should().ContainSingle();
        _observasjon.Links[0].Remarks.Should().Be("O-999");
    }

    /// <summary>Råteksten er bedre enn ingenting: noen må kunne se hva kilden sendte.</summary>
    [Fact]
    public void Lagrer_rateksten_som_kobling_naar_ingen_tolkning_gar()
    {
        Kjør(new CacheRecord { AssociatedOccurrences = "noe helt annet" });

        _observasjon.Links.Should().ContainSingle();
        _observasjon.Links[0].Remarks.Should().Contain("noe helt annet");
    }

    // --- Tilstand -----------------------------------------------------------

    [Fact]
    public void Baerer_hashen_fra_posten()
    {
        Kjør(new CacheRecord { HashCode = 4711 });

        _observasjon.HashCode.Should().Be(4711);
    }

    [Fact]
    public void Godtar_en_tom_post()
    {
        var resultat = Kjør(new CacheRecord());

        resultat.ValidationCode.Should().Be(RecordValidationCode.Ok);
        _observasjon.Errors.Should().BeEmpty();
    }
}
