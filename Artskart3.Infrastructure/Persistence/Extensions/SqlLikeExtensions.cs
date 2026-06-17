namespace Artskart3.Infrastructure.Persistence.Extensions;

public static class SqlLikeExtensions
{
    public static string EscapeSqlLikePattern(this string input)
    {
        return input
            .Replace("[", "[[]")
            .Replace("%", "[%]")
            .Replace("_", "[_]");
    }
}
