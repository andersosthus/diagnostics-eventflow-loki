using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.EventFlow.Outputs.Loki
{
    public interface IHttpClient
    {
        Task<HttpResponseMessage> PostAsync(Uri requestUri, HttpContent content);
        HttpRequestHeaders DefaultRequestHeaders { get; }
    }
}
