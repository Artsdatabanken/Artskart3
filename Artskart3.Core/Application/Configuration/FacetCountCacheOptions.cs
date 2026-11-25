namespace Artskart3.Core.Application.Configuration;

public class FacetCountCacheOptions
{
    public const string SectionName = "FacetCountCache";

    /// <summary>
    /// Slår av oppslag mot fasett-bufferen uten deploy. Av betyr at hvert kall
    /// aggregerer som før — bufferen kan altså skrus av i drift dersom den skulle
    /// vise seg å gi feil tall, uten at noe annet endres.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Slår av toernivået alene, som i områdebufferen. Nivå 0 og 1 er til sammen
    /// under 69 000 rader og dekker tomt filter; toernivået legger til 4,39 mill.
    /// rader og dekker ett aktivt filter — det vanligste tilfellet. Med denne kan de
    /// to første settes i drift og måles før toernivået skrus på.
    /// </summary>
    public bool EnableLevel2 { get; set; } = true;
}
