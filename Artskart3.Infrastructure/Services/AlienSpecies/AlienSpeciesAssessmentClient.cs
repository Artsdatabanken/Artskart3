using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Artskart3.Infrastructure.Services.AlienSpecies;

public sealed class AlienSpeciesAssessmentClient(
    HttpClient httpClient,
    IOptions<AlienSpeciesAssessmentOptions> options,
    ILogger<AlienSpeciesAssessmentClient> logger) : IAlienSpeciesAssessmentClient
{
    private readonly AlienSpeciesAssessmentOptions _options = options.Value;

    public Task<TaxonAssessmentFetchResult<CurrentAlienSpeciesAssessment>> GetCurrentAsync(
        CancellationToken cancellationToken = default) =>
        GetPagesAsync<CurrentAlienSpeciesAssessment>(_options.CurrentPath, cancellationToken);

    public Task<TaxonAssessmentFetchResult<AlienSpeciesAssessment2023>> GetSnapshot2023Async(
        CancellationToken cancellationToken = default) =>
        GetPagesAsync<AlienSpeciesAssessment2023>(_options.Snapshot2023Path, cancellationToken);

    private async Task<TaxonAssessmentFetchResult<T>> GetPagesAsync<T>(
        string initialPath, CancellationToken cancellationToken)
    {
        var items = new List<T>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        Uri? next = new(initialPath, UriKind.RelativeOrAbsolute);
        var pageCount = 0;

        while (next is not null)
        {
            if (++pageCount > _options.MaxPageCount)
                throw new InvalidDataException($"Lister returned more than {_options.MaxPageCount} pages.");

            var requestUri = next.IsAbsoluteUri
                ? next
                : new Uri(httpClient.BaseAddress!, next);
            ValidateRequestUri(requestUri);

            if (!visited.Add(requestUri.AbsoluteUri))
                throw new InvalidDataException($"Lister pagination repeated '{requestUri.AbsoluteUri}'.");

            var page = await GetPageAsync<T>(requestUri, cancellationToken);
            items.AddRange(page.Value);
            next = ParseNextLink(page.NextLink, requestUri);
        }

        return new TaxonAssessmentFetchResult<T>(items, pageCount);
    }

    private async Task<ODataPage<T>> GetPageAsync<T>(Uri uri, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                response = await SendAsync(uri, cancellationToken);
            }
            catch (HttpRequestException ex) when (attempt < _options.RetryCount)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(
                    _options.MaxRetryDelaySeconds,
                    Math.Pow(2, attempt)));
                logger.LogWarning(ex, "Lister-kallet feilet. Forsøker side på nytt etter {DelayMs} ms.", delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken);
                continue;
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    var page = await response.Content.ReadFromJsonAsync<ODataPage<T>>(cancellationToken);
                    if (page?.Value is null)
                        throw new InvalidDataException($"Lister returned an invalid OData page from '{uri}'.");
                    return page;
                }

                if (!IsTransient(response.StatusCode) || attempt >= _options.RetryCount)
                    response.EnsureSuccessStatusCode();

                var delay = GetRetryDelay(response, attempt);
                await LogAndDelayAsync(response.StatusCode, delay, cancellationToken);
            }
        }
    }

    private async Task LogAndDelayAsync(
        HttpStatusCode statusCode, TimeSpan delay, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Lister-svaret var {StatusCode}. Forsøker side på nytt etter {DelayMs} ms.",
            (int)statusCode, delay.TotalMilliseconds);
        await Task.Delay(delay, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException &&
                                    !cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException($"Could not retrieve assessment page '{uri}'.", ex);
        }
    }

    private Uri? ParseNextLink(string? nextLink, Uri currentUri)
    {
        if (string.IsNullOrWhiteSpace(nextLink))
            return null;

        var next = new Uri(nextLink, UriKind.RelativeOrAbsolute);
        var absolute = next.IsAbsoluteUri ? next : new Uri(currentUri, next);
        ValidateRequestUri(absolute);
        return absolute;
    }

    private void ValidateRequestUri(Uri uri)
    {
        var baseUri = httpClient.BaseAddress
            ?? throw new InvalidOperationException("The Lister HttpClient must have a base address.");

        if (uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase) ||
            uri.Port != baseUri.Port)
        {
            throw new InvalidDataException($"Lister continuation URL is outside the configured origin: '{uri}'.");
        }

        if (!uri.AbsolutePath.StartsWith("/odata/v1/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Lister continuation URL has an unexpected path: '{uri}'.");
    }

    private TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is not null)
        {
            var value = retryAfter.Delta
                ?? retryAfter.Date - DateTimeOffset.UtcNow;
            if (value is { } retryAfterDelay && retryAfterDelay > TimeSpan.Zero)
                return retryAfterDelay < TimeSpan.FromSeconds(_options.MaxRetryDelaySeconds)
                    ? retryAfterDelay
                    : TimeSpan.FromSeconds(_options.MaxRetryDelaySeconds);
        }

        var seconds = Math.Min(
            _options.MaxRetryDelaySeconds,
            Math.Pow(2, attempt));
        return TimeSpan.FromSeconds(seconds);
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or
            HttpStatusCode.TooManyRequests or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout;
}
