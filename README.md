# diagnostics-eventflow-loki

## Introduction
Extensions to Microsoft Diagnostics EventFlow to output to [Grafana Loki](https://grafana.com/docs/loki/latest/).
Based on [Microsoft.Diagnostics.EventFlow.Outputs.HttpOutput](https://github.com/Azure/diagnostics-eventflow/)

**Outputs**
- [Loki](#loki)

### Outputs

#### Loki
This output writes data to Loki. Here is an example showing all possible settings:
```json
{
  "inputs": [
    {
      "type": "Microsoft.Extensions.Logging"
    }
  ],
  "filters": [],
  "outputs": [
    {
      "type": "Loki",
      "lokiUri": "http://localhost:3100/loki/api/v1/push",
      "gzipPayload": true,
      "xScopeOrgId": "",
      "fieldsToLabels": ["EventName"],
      "staticLabels": {
        "mylabel": "myvalue"
      }
    }
  ],
  "extensions": [
    {
      "category": "outputFactory",
      "type": "Loki",
      "qualifiedTypeName": "Proactima.Diagnostics.EventFlow.Outputs.Loki.LokiOutputFactory, Proactima.Diagnostics.EventFlow.Outputs.Loki"
    }
  ],
  "schemaVersion": "2016-08-11"
}
```
| Field                             | Values/Types               | Required | Description                                                                      |
|:----------------------------------|:---------------------------|:---------|:---------------------------------------------------------------------------------|
| `type`                            | `Loki`                     | Yes      | Specifies the output type. For this output, it must be *Loki*                    |
| `lokiUri`                         | string                     | Yes      | Full URI of the Loki endpoint                                                    |
| `gzipPayload`                     | bool                       | No       | If the payload should be gzipped before sending                                  |
| `xScopeOrgId`                     | string                     | No       | A value to add as the X-Scope-OrgId header                                       |
| `fieldsToLabel`                   | string[]                   | No       | A set of fields in the Event that should be added as labels                      |
| `staticLabels`                    | Dictionary<string, string> | No       | A set of labels to be added to all logs                                          |
| `skipFields`                      | string[]                   | No       | A set of fields to exclude from the log message                                  |
| `basicAuthHeader`                 | bool                       | No       | If true, sets auth as a Basic Authorization header. If false, adds it to the url |
| `basicAuthenticationUserName`     | string                     | No       | Basic Auth Username                                                              |
| `basicAuthenticationUserPassword` | string                     | No       | Basic Auth Password                                                              |