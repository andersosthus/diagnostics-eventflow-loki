using Microsoft.Diagnostics.EventFlow.Outputs.Configuration;
using Microsoft.Diagnostics.EventFlow.Utilities;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Validation;

namespace Microsoft.Diagnostics.EventFlow.Outputs.Loki
{
    public class LokiOutput : IOutput
    {
        private IHttpClient httpClient;
        public static readonly string TraceTag = nameof(LokiOutput);

        private readonly IHealthReporter healthReporter;
        private LokiOutputConfiguration configuration;

        public LokiOutput(IConfiguration configuration, IHealthReporter healthReporter, IHttpClient httpClient = null)
        {
            Requires.NotNull(configuration, nameof(configuration));
            Requires.NotNull(healthReporter, nameof(healthReporter));

            this.healthReporter = healthReporter;

            var LokiOutputConfiguration = new LokiOutputConfiguration();
            try
            {
                configuration.Bind(LokiOutputConfiguration);
            }
            catch
            {
                healthReporter.ReportProblem($"Invalid {nameof(LokiOutput)} configuration encountered: '{configuration.ToString()}'",
                    EventFlowContextIdentifiers.Configuration);
                throw;
            }

            Initialize(LokiOutputConfiguration, httpClient);
        }

        public LokiOutput(LokiOutputConfiguration configuration, IHealthReporter healthReporter, IHttpClient httpClient = null)
        {
            Requires.NotNull(configuration, nameof(configuration));
            Requires.NotNull(healthReporter, nameof(healthReporter));

            this.healthReporter = healthReporter;

            // Clone the configuration instance since we are going to hold onto it (via this.connectionData)
            Initialize(configuration.DeepClone(), httpClient);
        }

        public JsonSerializerSettings SerializerSettings { get; set; }

        private void Initialize(LokiOutputConfiguration configuration, IHttpClient httpClient)
        {
            string errorMessage;

            Debug.Assert(configuration != null);
            Debug.Assert(this.healthReporter != null);

            this.httpClient = httpClient ?? new StandardHttpClient();
            this.configuration = configuration;

            if (string.IsNullOrWhiteSpace(this.configuration.LokiUri))
            {
                var errMsg = $"{nameof(LokiOutput)}: no ServiceUri configured";
                healthReporter.ReportProblem(errMsg);
                throw new Exception(errMsg);
            }

            string userName = configuration.BasicAuthenticationUserName;
            string password = configuration.BasicAuthenticationUserPassword;
            bool credentialsIncomplete = string.IsNullOrWhiteSpace(userName) ^ string.IsNullOrWhiteSpace(password);
            if (credentialsIncomplete)
            {
                errorMessage = $"{nameof(configuration)}: for basic authentication to work both user name and password must be specified";
                healthReporter.ReportWarning(errorMessage, EventFlowContextIdentifiers.Configuration);
                userName = password = null;
            }

            if (!string.IsNullOrWhiteSpace(userName) && !string.IsNullOrWhiteSpace(password))
            {
                string httpAuthValue = Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", userName, password)));
                this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", httpAuthValue);
            }

            if (!string.IsNullOrWhiteSpace(configuration.XScopeOrgId))
            {
                this.httpClient.DefaultRequestHeaders.Add("X-Scope-OrgId", configuration.XScopeOrgId);
            }

            foreach (KeyValuePair<string, string> header in configuration.Headers)
            {
                this.httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }

            SerializerSettings = EventFlowJsonUtilities.GetDefaultSerializerSettings();
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
                    sb.Clear();

                    var labels = new Dictionary<string, string>();
                    foreach (var mapping in configuration.FieldsToLabels)
                    {
                        object val = null;
                        e.Payload.TryGetValue(mapping, out val);
                        labels[mapping] = val as string ?? string.Empty;
                    }

                    object message = null;
                    e.Payload.TryGetValue("Message", out message);

                    sb.Append("level=");
                    sb.Append(e.Level.GetName());
                    sb.Append(' ');
                    sb.Append(message);

                    items.Add(new LokiItem
                    {
                        Labels = labels,
                        Payload = new [] { (e.Timestamp.ToUnixTimeMilliseconds() * 1000000).ToString() , sb.ToString()},
                    });

                }

                var streams = StreamGrouper.Process(items, configuration.StaticLabels);
                var payload = new LokiStreams { Streams = streams };

                HttpResponseMessage response;

                if (configuration.GzipPayload)
                {
                    response = await SendGzipJsonAsync(payload);
                }
                else
                {
                    response = await SendJsonAsync(payload);
                }

                response.EnsureSuccessStatusCode();
                this.healthReporter.ReportHealthy();
            }
            catch (Exception e)
            {
                ErrorHandlingPolicies.HandleOutputTaskError(e, () =>
                {
                    string errorMessage = nameof(LokiOutput) + ": diagnostic data upload failed: " + Environment.NewLine + e.ToString();
                    this.healthReporter.ReportWarning(errorMessage, EventFlowContextIdentifiers.Output);
                });
            }
        }

        private async Task<HttpResponseMessage> SendJsonAsync(LokiStreams streams)
        {
            var payload = JsonConvert.SerializeObject(streams);

            var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
            return await httpClient.PostAsync(new Uri(configuration.LokiUri), content);
        }

        private async Task<HttpResponseMessage> SendGzipJsonAsync(LokiStreams streams)
        {
            var serializer = JsonSerializer.Create();
            using (var jsonStream = new MemoryStream())
            using (var sw = new StreamWriter(jsonStream))
            using (var jw = new JsonTextWriter(sw))
            using (var compressed = new MemoryStream())
            using (var compressor = new GZipStream(compressed, CompressionMode.Compress))
            {
                serializer.Serialize(jw, streams);
                jw.Flush();
                jsonStream.Position = 0;

                jsonStream.CopyTo(compressor);
                await compressor.FlushAsync();
                compressed.Position = 0;

                var content = new ByteArrayContent(compressed.ToArray());
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                content.Headers.Add("Content-Encoding", "gzip");

                return await httpClient.PostAsync(new Uri(configuration.LokiUri), content);
            }
        }

        private class StandardHttpClient : IHttpClient
        {
            private HttpClient httpClient = new HttpClient();

            public HttpRequestHeaders DefaultRequestHeaders => this.httpClient.DefaultRequestHeaders;

            public Task<HttpResponseMessage> PostAsync(Uri requestUri, HttpContent content)
            {
                return this.httpClient.PostAsync(requestUri, content);
            }
        }
    }
}
