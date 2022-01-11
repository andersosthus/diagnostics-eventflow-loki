using Microsoft.Diagnostics.EventFlow.Configuration;

namespace Microsoft.Diagnostics.EventFlow.Outputs.Configuration
{
    public class LokiOutputConfiguration : ItemConfiguration
    {
        public string LokiUri { get; set; }
        public string BasicAuthenticationUserName { get; set; }
        public string BasicAuthenticationUserPassword { get; set; }
        public string XScopeOrgId { get; set; }
        public bool GzipPayload { get; set; }
        public List<string> FieldsToLabels { get; set; }
        public Dictionary<string, string> StaticLabels { get; set; }
        public Dictionary<string, string> Headers { get; set; }

        public LokiOutputConfiguration()
        {
            Headers = new Dictionary<string, string>();
        }

        public LokiOutputConfiguration DeepClone()
        {
            var other = new LokiOutputConfiguration()
            {
                LokiUri = this.LokiUri,
                BasicAuthenticationUserName = this.BasicAuthenticationUserName,
                BasicAuthenticationUserPassword = this.BasicAuthenticationUserPassword,
                XScopeOrgId = this.XScopeOrgId,
                GzipPayload = this.GzipPayload,
                FieldsToLabels = this.FieldsToLabels,
                StaticLabels = this.StaticLabels,
                Headers = new Dictionary<string, string>(this.Headers)
            };

            return other;
        }
    }
}
