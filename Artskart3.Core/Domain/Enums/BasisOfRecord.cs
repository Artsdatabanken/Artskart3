using System.ComponentModel;

namespace Artskart3.Core.Domain.Enums;

public enum BasisOfRecord
{
    [Description("Belagt funn")]
    Object = 1,

    [Description("Observasjon")]
    Observation = 2,

    [Description("Sakkyndig vurdering")]
    Assessment = 5,

    [Description("Litteratur")]
    Publication = 6,

    [Description("Fossil Specimen")]
    FossilSpecimen = 10,

    [Description("Machine Observation")]
    MachineObservation = 11,

    [Description("Living Specimen")]
    LivingSpecimen = 12,

    [Description("Material Sample")]
    MaterialSample = 13,

    [Description("Human Observasjon")]
    HumanObservasjon = 14,

    [Description("DNA")]
    DNA = 16,

    [Description("eDNA")]
    EDNA = 17,

    [Description("eDNA Barcoding")]
    EDNABarcoding = 18
}
