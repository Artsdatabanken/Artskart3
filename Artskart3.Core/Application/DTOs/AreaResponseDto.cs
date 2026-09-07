namespace Artskart3.Core.Application.DTOs;

public class AreaResponseDto
{
    public AreaTypeDto? Counties { get; set; }
    public AreaTypeDto? Municipalities { get; set; }
    public AreaTypeDto? RestrictedAreas { get; set; }
    public AreaTypeDto? OceanAreas { get; set; }
    public AreaTypeDto? SvalbardBjørnøyaAndJanMayen { get; set; }

}
