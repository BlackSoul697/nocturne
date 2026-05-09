# NightscoutFoundation.Nocturne.Model.UpsertSensorGlucoseRequest
Request body for upserting a CGM sensor glucose reading via the V4 API.

## Properties

Name | Type | Description | Notes
------------ | ------------- | ------------- | -------------
**Timestamp** | **DateTimeOffset** | When the sensor reading was taken. | [optional] 
**UtcOffset** | **int?** | UTC offset in minutes at the time of the event, for local-time display. | [optional] 
**Device** | **string** | Identifier of the CGM transmitter or receiver. | [optional] 
**App** | **string** | Name of the application that submitted this record. | [optional] 
**DataSource** | **string** | Upstream data source identifier. | [optional] 
**Mgdl** | **double** | Glucose reading in mg/dL (validated 0-10,000). | [optional] 
**Direction** | **GlucoseDirection** |  | [optional] 
**TrendRate** | **double?** | Rate of glucose change in mg/dL per minute. | [optional] 
**Noise** | **int?** | Sensor noise level indicator (device-specific scale). | [optional] 
**Filtered** | **double?** | Raw filtered sensor value (scaled ADC) | [optional] 
**Unfiltered** | **double?** | Raw unfiltered sensor value (scaled ADC) | [optional] 
**Delta** | **double?** | Glucose delta in mg/dL over the last 5 minutes | [optional] 
**GlucoseProcessing** | **string** | Whether this glucose value is smoothed or unsmoothed. Accepted values: \&quot;Smoothed\&quot;, \&quot;Unsmoothed\&quot;. Case-insensitive. Null for unknown. | [optional] 
**SmoothedMgdl** | **double?** | Smoothed glucose value in mg/dL, when known. | [optional] 
**UnsmoothedMgdl** | **double?** | Unsmoothed (raw) glucose value in mg/dL, when known. | [optional] 

[[Back to Model list]](../README.md#documentation-for-models) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to README]](../README.md)

