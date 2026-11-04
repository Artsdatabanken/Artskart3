using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services;

/// <summary>
/// De fem bool-filtrene pakket i én bitmaske, lagret som tredje nøkkel på hver
/// bufferrad i stedet for som fem egne dimensjoner.
///
/// HVORFOR
/// Bufferen svarer på maksimalt to aktive dimensjoner. Som egne dimensjoner spiste
/// to bool-filtre hele budsjettet, og alt utover falt gjennom til databasen —
/// målt 2091-2752 ms på LocationPolygons mot 14-215 ms fra bufferen. Som maske
/// koster alle fem til sammen null dimensjonsplasser, så to vanlige filtre pluss
/// hvilken som helst bool-kombinasjon besvares fortsatt fra bufferen.
///
/// MÅLT KOSTNAD
/// Masken multipliserer radene i en celle med antall masker som faktisk forekommer
/// der. Det er langt under de 32 teoretisk mulige:
///
///   Regstatus x Funntype (grove bøtter, store celler)   2,40x
///   Takson x Periode (fine bøtter, små celler)          1,19x
///   Periode per lokasjon                                1,06x
///
/// De fine parene dominerer, så områdebufferens nivå 2 vokser fra 41,7 til ~45
/// millioner rader. Lokasjonsbufferen KRYMPER fra 95,5 til ~74 millioner, fordi de
/// fem bool-dimensjonene forsvinner og multiplikatoren der bare er 1,06.
///
/// Av 16 mulige tagg-kombinasjoner forekommer 9 i produksjonslik data, og de fem
/// minste har 2 til 2818 rader. Maskerommet er ekstremt skjevt, og det er derfor
/// dette skalerer: nye bool-filtre dobler det teoretiske rommet, men knapt det
/// befolkede.
///
/// BITENE ER STABILE
/// Verdiene ligger i bufferdataene. Endres en bit, peker eksisterende rader på feil
/// filter. Nye filtre skal ha nye bit, aldri gjenbruke en ledig.
///
/// SQL-UTTRYKKET MÅ STEMME med <see cref="SqlExpression"/>, som byggeskriptene
/// bruker ordrett. Går de to fra hverandre, bøtter bygget etter én definisjon mens
/// oppslaget leser etter en annen — riktige tall for feil filter.
/// </summary>
public static class FilterMask
{
    public const byte ChangedByAnnotation = 1;
    public const byte Sensitive = 2;
    public const byte UncertainIdentification = 4;
    public const byte Introduced = 8;
    public const byte HasMediaFiles = 16;

    /// <summary>Antall gyldige maskeverdier, 0 til og med 31.</summary>
    public const int Count = 32;

    /// <summary>
    /// Uttrykket byggeskriptene grupperer på. Kolonnealiaset <c>i</c> er
    /// ObservationEntityIndex, som i resten av #dim-lista.
    /// </summary>
    public const string SqlExpression =
        "CAST(i.ChangedByAnnotation AS INT) + CAST(i.Sensitive AS INT) * 2 " +
        "+ CAST(i.UncertainIdentification AS INT) * 4 + CAST(i.Introduced AS INT) * 8 " +
        "+ CAST(i.HasMediaFiles AS INT) * 16";

    /// <summary>
    /// Hvilke bit filteret bryr seg om, og hvilke verdier det vil ha.
    ///
    /// Oppslaget blir <c>(Mask &amp; Care) == Want</c> — ett restledd, ikke en
    /// IN-liste over opptil 16 maskeverdier. Er <see cref="Care"/> null, er ingen
    /// bool satt, og predikatet utelates helt slik at cellens masker summeres.
    /// </summary>
    public readonly record struct Selection(byte Care, byte Want)
    {
        public bool IsActive => Care != 0;
    }

    /// <summary>Leser maskeutvalget ut av filteret.</summary>
    public static Selection Select(IObservationFilter filter)
    {
        byte care = 0, want = 0;

        Add(filter.ChangedByAnnotation, ChangedByAnnotation);
        Add(filter.Sensitive, Sensitive);
        Add(filter.UncertainIdentification, UncertainIdentification);
        Add(filter.Introduced, Introduced);
        Add(filter.WithImages, HasMediaFiles);

        return new Selection(care, want);

        void Add(bool? value, byte bit)
        {
            if (value is not { } v) return;
            care |= bit;
            if (v) want |= bit;
        }
    }

    /// <summary>Samme utvalg lest ut av lokasjonsfilteret.</summary>
    public static Selection Select(LocationSearchFilterDto filter)
    {
        byte care = 0, want = 0;

        Add(filter.ChangedByAnnotation, ChangedByAnnotation);
        Add(filter.Sensitive, Sensitive);
        Add(filter.UncertainIdentification, UncertainIdentification);
        Add(filter.Introduced, Introduced);
        Add(filter.WithImages, HasMediaFiles);

        return new Selection(care, want);

        void Add(bool? value, byte bit)
        {
            if (value is not { } v) return;
            care |= bit;
            if (v) want |= bit;
        }
    }
}
