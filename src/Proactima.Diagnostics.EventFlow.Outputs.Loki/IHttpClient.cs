using System.Net.Http.Headers;

namespace Microsoft.Diagnostics.EventFlow.Outputs.Loki
{
    public interface IHttpClient
    {
        Task<HttpResponseMessage> PostAsync(Uri requestUri, HttpContent content);
        HttpRequestHeaders DefaultRequestHeaders { get; }
    }
}
