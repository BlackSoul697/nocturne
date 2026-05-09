# NightscoutFoundation.Nocturne.Model.UpdateBolusRequest
Request body for updating an existing insulin bolus record via the V4 API. The bolus is identified by the route-level ID parameter.

## Properties

Name | Type | Description | Notes
------------ | ------------- | ------------- | -------------
**Timestamp** | **DateTimeOffset** | When the bolus was delivered. | [optional] 
**UtcOffset** | **int?** | UTC offset in minutes at the time of the event, for local-time display. | [optional] 
**Device** | **string** | Identifier of the device that delivered the bolus. | [optional] 
**App** | **string** | Name of the application that submitted this record. | [optional] 
**DataSource** | **string** | Upstream data source identifier; required when SyncIdentifier is supplied. | [optional] 
**Insulin** | **double** | Total insulin amount in units. | [optional] 
**Programmed** | **double?** | Programmed insulin amount in units. | [optional] 
**Delivered** | **double?** | Actually delivered insulin amount in units. | [optional] 
**Automatic** | **bool** | Whether this bolus was delivered automatically by an APS/loop system. | [optional] 
**Duration** | **double?** | Extended/square bolus duration in minutes. | [optional] 
**SyncIdentifier** | **string** | Upstream sync identifier for deduplication, paired with DataSource. | [optional] 
**InsulinType** | **string** | Type or brand of insulin used (e.g. \&quot;Humalog\&quot;, \&quot;NovoRapid\&quot;). | [optional] 
**PatientInsulinId** | **string** | Optional reference to a PatientInsulin. When provided, the server resolves it to a TreatmentInsulinContext snapshot and overwrites InsulinType with the insulin&#39;s name. | [optional] 
**Unabsorbed** | **double?** | Insulin on board (unabsorbed) at the time of the bolus, in units. | [optional] 
**BolusCalculationId** | **string** | Links this bolus to the bolus calculation that recommended it. | [optional] 
**ApsSnapshotId** | **string** | Links this bolus to the APS decision snapshot that triggered it. | [optional] 
**CorrelationId** | **string** | Correlation identifier for grouping related events. | [optional] 

[[Back to Model list]](../README.md#documentation-for-models) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to README]](../README.md)

