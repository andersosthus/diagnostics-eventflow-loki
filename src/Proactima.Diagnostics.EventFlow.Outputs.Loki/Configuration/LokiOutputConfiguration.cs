using Microsoft.Diagnostics.EventFlow.Configuration;
using System.Collections.Generic;

namespace Proactima.Diagnostics.EventFlow.Outputs.Configuration
{
    public class LokiOutputConfiguration : ItemConfiguration
    {
        public string LokiUri { get; set; }
        public string BasicAuthenticationUserName { get; set; }
        public string BasicAuthenticationUserPassword { get; set; }
        public bool BasicAuthHeader { get; set; } = true;
        public string XScopeOrgId { get; set; }
        public bool GzipPayload { get; set; } = true;
        public List<string> FieldsToLabels { get; set; }
        public List<string> SkipFields { get; set; }
        public Dictionary<string, string> StaticLabels { get; set; } 
        public Dictionary<string, string> Headers { get; set; }

        public LokiOutputConfiguration()
        {
            FieldsToLabels = new List<string>();
            SkipFields = new List<string>();
            StaticLabels = new Dictionary<string, string>();
            Headers = new Dictionary<string, string>();
        }

        public LokiOutputConfiguration DeepClone()
        {
            var other = new LokiOutputConfiguration()
            {
                LokiUri = this.LokiUri,
                BasicAuthenticationUserName = this.BasicAuthenticationUserName,
                BasicAuthenticationUserPassword = this.BasicAuthenticationUserPassword,
                BasicAuthHeader = this.BasicAuthHeader,
                XScopeOrgId = this.XScopeOrgId,
                GzipPayload = this.GzipPayload,
                FieldsToLabels = this.FieldsToLabels,
                StaticLabels = this.StaticLabels,
                SkipFields = this.SkipFields,
                Headers = new Dictionary<string, string>(this.Headers)
            };

            return other;
        }
    }
}
