using System.Net;

namespace DevForge.Infrastructure.Execution;

internal sealed class HttpHealthProbe : IHealthProbe, IDisposable
{
    /// <summary>An application that takes longer than this to answer one request is treated as not ready yet.</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);

    private readonly HttpClient _http = new() { Timeout = RequestTimeout };

    /// <summary>
    /// Any answer below 500 counts: the application is up and handling requests, even if this
    /// particular path needs a login or does not exist. A refused connection, a timeout or a
    /// server error means it is not ready.
    /// </summary>
    public async Task<bool> IsRespondingAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return response.StatusCode < HttpStatusCode.InternalServerError;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The request timed out; the caller did not cancel.
            return false;
        }
    }

    public void Dispose() => _http.Dispose();
}
