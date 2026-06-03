namespace Artskart3.Core.Application.DTOs
{
    //TODO  HACK for å få svalbard og jan mayen riktig i frontend
    public class AreaResponseDto
    {
        public CountyDto? Counties {  get; set; }
        public AreaTypeDto? Municipalities { get; set; }
        public AreaTypeDto? RestrictedAreas { get; set; }
        public AreaTypeDto? OceanAreas { get; set; }

    }

    public class CountyDto
    {
        public AreaDto[]? FastlandsNorge { get; set; }
        public AreaDto? JanMayen { get; set; }
        public AreaDto? Svalbard { get; set; }
    }
}
