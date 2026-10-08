namespace Artskart3.Core.Application.DTOs;

public static class ObservationFilterExtensions
{
    public static int[] GetDatasetOrgIds(this IObservationFilter filter) => WithSingle(filter.DatasetOrgIds, filter.DatasetOrgId);

    public static int[] GetProjectOrgIds(this IObservationFilter filter) => WithSingle(filter.ProjectOrgIds, filter.ProjectOrgId);

    private static int[] WithSingle(int[]? ids, int? single) =>
        single is { } id ? [.. ids ?? [], id] : ids ?? [];
}
