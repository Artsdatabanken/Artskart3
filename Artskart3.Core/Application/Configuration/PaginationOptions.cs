namespace Artskart3.Core.Application.Configuration;

public class PaginationOptions
{
    public const string SectionName = "Pagination";

    public int LookaheadMultiplier { get; set; } = 4;
}
