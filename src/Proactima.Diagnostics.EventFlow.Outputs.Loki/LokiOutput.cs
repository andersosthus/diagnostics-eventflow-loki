﻿using Microsoft.Diagnostics.EventFlow;
using Microsoft.Diagnostics.EventFlow.Utilities;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Proactima.Diagnostics.EventFlow.Outputs.Configuration;
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

namespace Proactima.Diagnostics.EventFlow.Outputs.Loki
{
    public class LokiOutput : IOutput
    {
        private IHttpClient _httpClient;
        public static readonly string TraceTag = nameof(LokiOutput);

        private readonly IHealthReporter _healthReporter;
        private LokiOutputConfiguration _configuration;

        public LokiOutput(IConfiguration configuration, IHealthReporter healthReporter, IHttpClient httpClient = null)
        {
            Requires.NotNull(configuration, nameof(configuration));
            Requires.NotNull(healthReporter, nameof(healthReporter));

            _healthReporter = healthReporter;

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

            _healthReporter = healthReporter;

            // Clone the configuration instance since we are going to hold onto it (via this.connectionData)
            Initialize(configuration.DeepClone(), httpClient);
        }

        private void Initialize(LokiOutputConfiguration configuration, IHttpClient httpClient)
        {
            string errorMessage;

            Debug.Assert(configuration != null);
            Debug.Assert(this._healthReporter != null);

            _httpClient = httpClient ?? new StandardHttpClient();
            _configuration = configuration;

            if (string.IsNullOrWhiteSpace(_configuration.LokiUri))
            {
                var errMsg = $"{nameof(LokiOutput)}: no LokiUri configured";
                _healthReporter.ReportProblem(errMsg);
                throw new Exception(errMsg);
            }

            string userName = configuration.BasicAuthenticationUserName;
            string password = configuration.BasicAuthenticationUserPassword;
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
            }

            foreach (KeyValuePair<string, string> header in configuration.Headers)
            {
                _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
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
                    sb.Clear();

                    var labels = new Dictionary<string, string>();
                    foreach (var mapping in _configuration.FieldsToLabels)
                    {
                        e.Payload.TryGetValue(mapping, out object val);
                        labels[mapping] = val as string ?? string.Empty;
                    }

                    e.Payload.TryGetValue("Message", out object message);

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

                var streams = StreamGrouper.Process(items, _configuration.StaticLabels);
                var payload = new LokiStreams { Streams = streams };

                HttpResponseMessage response;

                if (_configuration.GzipPayload)
                {
                    response = await SendGzipJsonAsync(payload);
                }
                else
                {
                    response = await SendJsonAsync(payload);
                }

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

        private async Task<HttpResponseMessage> SendJsonAsync(LokiStreams streams)
        {
            var payload = JsonConvert.SerializeObject(streams);

            var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
            return await _httpClient.PostAsync(new Uri(_configuration.LokiUri), content);
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

                return await _httpClient.PostAsync(new Uri(_configuration.LokiUri), content);
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
