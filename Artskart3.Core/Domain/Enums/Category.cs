using System.ComponentModel;

namespace Artskart3.Core.Domain.Enums
{
    public enum Category
    {
        [Description("Ukjent")]
        Unknown = 0,

        [Description("Ikke vurdert")]
        NE = 1, // RedList

        [Description("Ikke egnet")]
        NA = 2, // AlienSpecies

        [Description("Ikke risikovurdert")]
        NR = 3, // AlienSpecies

        [Description("Ingen kjent risiko")]
        NK = 4, // AlienSpecies

        [Description("Lav risiko")]
        LO = 5, // AlienSpecies

        [Description("Potensielt høy risiko")]
        PH = 6, // AlienSpecies

        [Description("Høy risiko")]
        HI = 7, // AlienSpecies

        [Description("Svært høy risiko")]
        SE = 8, // AlienSpecies

        [Description("Livskraftig")]
        LC = 9, // RedList

        [Description("Datamangel")]
        DD = 10, // RedList

        [Description("Nær truet")]
        NT = 11, // RedList

        [Description("Sårbar")]
        VU = 12, // RedList

        [Description("Sterkt truet")]
        EN = 13, // RedList

        [Description("Kritisk truet")]
        CR = 14, // RedList

        [Description("Regionalt utdødd")]
        RE = 15 // RedList
    }
}
