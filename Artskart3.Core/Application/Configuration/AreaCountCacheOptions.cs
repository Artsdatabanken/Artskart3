namespace Artskart3.Core.Application.Configuration;

public class AreaCountCacheOptions
{
    public const string SectionName = "AreaCountCache";

    /// <summary>
    /// Slår av oppslag mot områdebufferen uten deploy. Av betyr at alle kall teller
    /// observasjoner som før — bufferen kan altså skrus av i drift dersom den skulle
    /// vise seg å gi feil tall, uten at noe annet endres.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Slår av toernivået alene. Ettnivå tar 60 % av den trege tiden for 17 MB;
    /// toernivået legger til 37 % for 0,9 GB. Med denne kan ettnivå settes i drift
    /// og måles før toernivået skrus på.
    /// </summary>
    public bool EnableLevel2 { get; set; } = true;
}
