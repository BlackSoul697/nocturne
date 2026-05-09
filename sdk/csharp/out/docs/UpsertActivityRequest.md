# NightscoutFoundation.Nocturne.Model.UpsertActivityRequest
Request body for upserting an activity record via the V4 API.

## Properties

Name | Type | Description | Notes
------------ | ------------- | ------------- | -------------
**Mills** | **long** | When the activity occurred, as a Unix millisecond timestamp. | [optional] 
**UtcOffset** | **int?** | UTC offset in minutes at the time of the event, for local-time display. | [optional] 
**Type** | **string** | Activity type or category (e.g., \&quot;exercise\&quot;, \&quot;walk\&quot;, \&quot;run\&quot;). | [optional] 
**Description** | **string** | Activity description or notes. | [optional] 
**Duration** | **double?** | Duration of the activity in minutes. | [optional] 
**Intensity** | **string** | Intensity level of the activity. | [optional] 
**Notes** | **string** | Additional notes about the activity. | [optional] 
**EnteredBy** | **string** | Name of the application or person that submitted this record. | [optional] 
**Distance** | **double?** | Distance covered during the activity. | [optional] 
**DistanceUnits** | **string** | Units for distance (e.g., \&quot;meters\&quot;, \&quot;kilometers\&quot;, \&quot;miles\&quot;). | [optional] 
**Energy** | **double?** | Energy expended during the activity (calories). | [optional] 
**EnergyUnits** | **string** | Units for energy (e.g., \&quot;calories\&quot;, \&quot;kilocalories\&quot;, \&quot;joules\&quot;). | [optional] 
**Name** | **string** | Name or title of the activity. | [optional] 

[[Back to Model list]](../README.md#documentation-for-models) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to README]](../README.md)

