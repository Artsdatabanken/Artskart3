namespace Artskart3.Core.Application.Validation;

public static class WebUrl
{
    public static bool IsValid(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme is "https" or "http" && string.IsNullOrEmpty(uri.UserInfo);
}
