using Microsoft.Diagnostics.EventFlow;
using Microsoft.Diagnostics.EventFlow.Metadata;
using Microsoft.Diagnostics.EventFlow.Utilities;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Proactima.Diagnostics.EventFlow.Outputs.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Validation;

namespace Proactima.Diagnostics.EventFlow.Outputs.Loki
{
    public class LokiOutput : IOutput
    {
        private IHttpClient _httpClient;
        public static readonly string TraceTag = nameof(LokiOutput);

        private readonly IHealthReporter _healthReporter;
        private LokiOutputConfiguration _configuration;

        public LokiOutput(IConfiguration configuration, IHealthReporter healthReporter)
        {
            Requires.NotNull(configuration, nameof(configuration));
            Requires.NotNull(healthReporter, nameof(healthReporter));

            _healthReporter = healthReporter;

            var LokiOutputConfiguration = new LokiOutputConfiguration();
            try
            {
                configuration.Bind(LokiOutputConfiguration);

                _healthReporter.ReportHealthy($"{nameof(LokiOutput)}: Loaded configuration with values: '{configuration}'");
            }
            catch
            {
                _healthReporter.ReportProblem($"{nameof(LokiOutput)}: Invalid configuration encountered: '{configuration}'",
                    EventFlowContextIdentifiers.Configuration);
                throw;
            }

            Initialize(LokiOutputConfiguration);
        }

        public LokiOutput(LokiOutputConfiguration configuration, IHealthReporter healthReporter)
        {
            Requires.NotNull(configuration, nameof(configuration));
            Requires.NotNull(healthReporter, nameof(healthReporter));

            _healthReporter = healthReporter;

            // Clone the configuration instance since we are going to hold onto it (via this.connectionData)
            Initialize(configuration.DeepClone());
        }

        private void Initialize(LokiOutputConfiguration configuration)
        {
            string errorMessage;
            _configuration = configuration;

            Debug.Assert(configuration != null);
            Debug.Assert(_healthReporter != null);

            _httpClient = new StandardHttpClient();

            if (string.IsNullOrWhiteSpace(_configuration.LokiUri))
            {
                var errMsg = $"{nameof(LokiOutput)}: no LokiUri configured";
                _healthReporter.ReportProblem(errMsg);
                throw new Exception(errMsg);
            }

            string userName = _configuration.BasicAuthenticationUserName;
            string password = _configuration.BasicAuthenticationUserPassword;
            bool credentialsIncomplete = string.IsNullOrWhiteSpace(userName) ^ string.IsNullOrWhiteSpace(password);
            if (credentialsIncomplete)
            {
                errorMessage = $"{nameof(configuration)}: for basic authentication to work both user name and password must be specified";
                _healthReporter.ReportWarning(errorMessage, EventFlowContextIdentifiers.Configuration);
                userName = password = null;
            }

            if (!string.IsNullOrWhiteSpace(userName) && !string.IsNullOrWhiteSpace(password))
            {
                string httpAuthValue = Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", userName, password)));
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", httpAuthValue);
            }

            if (!string.IsNullOrWhiteSpace(configuration.XScopeOrgId))
            {
                _httpClient.DefaultRequestHeaders.Add("X-Scope-OrgId", configuration.XScopeOrgId);
                _healthReporter.ReportHealthy($"{nameof(LokiOutput)}: Added X-Scope-OrgId with value {configuration.XScopeOrgId}");
            }

            foreach (KeyValuePair<string, string> header in configuration.Headers)
            {
                _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                _healthReporter.ReportHealthy($"{nameof(LokiOutput)}: Added header {header.Key} with value {header.Value}");
            }
        }

        public async Task SendEventsAsync(IReadOnlyCollection<EventData> events, long transmissionSequenceNumber, CancellationToken cancellationToken)
        {
            if (events == null || events.Count == 0)
            {
                return;
            }

            try
            {
                var sb = new StringBuilder();
                var items = new List<LokiItem>();

                foreach (var e in events)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    sb.Clear();

                    var labels = new Dictionary<string, string>();
                    foreach (var mapping in _configuration.FieldsToLabels)
                    {
                        if (e.Payload.TryGetValue(mapping, out object val))
                        {
                            labels[mapping] = val as string ?? string.Empty;
                        }
                    }

                    sb.Append("level=");
                    sb.Append(e.Level.GetName());

                    foreach (var kvp in e.Payload)
                    {
                        if (_configuration.SkipFields.Contains(kvp.Key))
                        {
                            continue;
                        }

                        var val = kvp.Value as string;
                        if (string.IsNullOrWhiteSpace(val))
                        {
                            continue;
                        }

                        sb.Append(' ');
                        sb.Append(kvp.Key.Replace("\"\"", ""));
                        sb.Append("=\"");
                        sb.Append(val.Replace("\"\"", ""));
                        sb.Append("\"");
                    }

                    items.Add(new LokiItem
                    {
                        Labels = labels,
                        Payload = new[] { (e.Timestamp.ToUnixTimeMilliseconds() * 1000000).ToString(), sb.ToString() },
                    });
                }

                var streams = StreamGrouper.Process(items, _configuration.StaticLabels);
                var payload = new LokiStreams { Streams = streams };

                if (_configuration.GzipPayload)
                {
                    await SendGzipJsonAsync(payload).ConfigureAwait(false);
                }
                else
                {
                    await SendJsonAsync(payload).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                ErrorHandlingPolicies.HandleOutputTaskError(e, () =>
                {
                    string errorMessage = nameof(LokiOutput) + ": diagnostic data upload failed: " + Environment.NewLine + e.ToString();
                    _healthReporter.ReportWarning(errorMessage, EventFlowContextIdentifiers.Output);
                });
            }
        }

        private async Task SendJsonAsync(LokiStreams streams)
        {
            try
            {
                var payload = JsonConvert.SerializeObject(streams);

                var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(new Uri(_configuration.LokiUri), content).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();
                _healthReporter.ReportHealthy();
            }
            catch (Exception e)
            {
                ErrorHandlingPolicies.HandleOutputTaskError(e, () =>
                {
                    string errorMessage = nameof(LokiOutput) + ": diagnostic data upload failed: " + Environment.NewLine + e.ToString();
                    _healthReporter.ReportWarning(errorMessage, EventFlowContextIdentifiers.Output);
                });
            }
        }

        private async Task SendGzipJsonAsync(LokiStreams logData)
        {
            try
            {
                using (var compressed = new MemoryStream())
                {
                    using (var gzipStream = new GZipStream(compressed, CompressionMode.Compress, true))
                    using (var streamWriter = new StreamWriter(gzipStream))
                    using (var jsonWriter = new JsonTextWriter(streamWriter))
                    {
                        var serializer = JsonSerializer.Create();
                        serializer.Serialize(jsonWriter, logData);
                    }
                    compressed.Position = 0;

                    var content = new StreamContent(compressed);
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    content.Headers.Add("Content-Encoding", "gzip");

                    var response = await _httpClient.PostAsync(new Uri(_configuration.LokiUri), content).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    _healthReporter.ReportHealthy();
                }
            }
            catch (Exception e)
            {
                ErrorHandlingPolicies.HandleOutputTaskError(e, () =>
                {
                    string errorMessage = nameof(LokiOutput) + ": diagnostic data upload failed: " + Environment.NewLine + e.ToString();
                    _healthReporter.ReportWarning(errorMessage, EventFlowContextIdentifiers.Output);
                });
            }
        }

        private class StandardHttpClient : IHttpClient
        {
            private readonly HttpClient _httpClient = new HttpClient();

            public HttpRequestHeaders DefaultRequestHeaders => _httpClient.DefaultRequestHeaders;

            public Task<HttpResponseMessage> PostAsync(Uri requestUri, HttpContent content)
            {
                return _httpClient.PostAsync(requestUri, content);
            }
        }
    }
}
