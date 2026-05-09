# NightscoutFoundation.Nocturne.Api.ProfileApi

All URIs are relative to *http://localhost*

| Method | HTTP request | Description |
|--------|--------------|-------------|
| [**ProfileCreateBasalSchedule**](ProfileApi.md#profilecreatebasalschedule) | **POST** /api/v4/profile/basal | Create a new basal schedule |
| [**ProfileCreateCarbRatioSchedule**](ProfileApi.md#profilecreatecarbratioschedule) | **POST** /api/v4/profile/carb-ratio | Create a new carb ratio schedule |
| [**ProfileCreateSensitivitySchedule**](ProfileApi.md#profilecreatesensitivityschedule) | **POST** /api/v4/profile/sensitivity | Create a new sensitivity schedule |
| [**ProfileCreateTargetRangeSchedule**](ProfileApi.md#profilecreatetargetrangeschedule) | **POST** /api/v4/profile/target-range | Create a new target range schedule |
| [**ProfileCreateTherapySettings**](ProfileApi.md#profilecreatetherapysettings) | **POST** /api/v4/profile/settings | Create a new therapy settings record |
| [**ProfileDeleteBasalSchedule**](ProfileApi.md#profiledeletebasalschedule) | **DELETE** /api/v4/profile/basal/{id} | Delete a basal schedule |
| [**ProfileDeleteCarbRatioSchedule**](ProfileApi.md#profiledeletecarbratioschedule) | **DELETE** /api/v4/profile/carb-ratio/{id} | Delete a carb ratio schedule |
| [**ProfileDeleteSensitivitySchedule**](ProfileApi.md#profiledeletesensitivityschedule) | **DELETE** /api/v4/profile/sensitivity/{id} | Delete a sensitivity schedule |
| [**ProfileDeleteTargetRangeSchedule**](ProfileApi.md#profiledeletetargetrangeschedule) | **DELETE** /api/v4/profile/target-range/{id} | Delete a target range schedule |
| [**ProfileDeleteTherapySettings**](ProfileApi.md#profiledeletetherapysettings) | **DELETE** /api/v4/profile/settings/{id} | Delete a therapy settings record |
| [**ProfileGetBasalScheduleById**](ProfileApi.md#profilegetbasalschedulebyid) | **GET** /api/v4/profile/basal/by-id/{id} | Get a basal schedule by ID |
| [**ProfileGetBasalSchedulesByName**](ProfileApi.md#profilegetbasalschedulesbyname) | **GET** /api/v4/profile/basal/{profileName} | Get basal schedules by profile name |
| [**ProfileGetCarbRatioScheduleById**](ProfileApi.md#profilegetcarbratioschedulebyid) | **GET** /api/v4/profile/carb-ratio/by-id/{id} | Get a carb ratio schedule by ID |
| [**ProfileGetCarbRatioSchedulesByName**](ProfileApi.md#profilegetcarbratioschedulesbyname) | **GET** /api/v4/profile/carb-ratio/{profileName} | Get carb ratio schedules by profile name |
| [**ProfileGetProfileRecords**](ProfileApi.md#profilegetprofilerecords) | **GET** /api/v4/profile/records | Get legacy Nightscout-shaped profile records projected from V4 schedule data. Intended for connector consumption where the caller needs the monolithic Profile shape (store with basal/carbratio/sens/target arrays). |
| [**ProfileGetProfileSummary**](ProfileApi.md#profilegetprofilesummary) | **GET** /api/v4/profile/summary | Get a consolidated summary of all profile data across all profile names. Optionally provide a date range to include schedule change detection info. |
| [**ProfileGetSensitivityScheduleById**](ProfileApi.md#profilegetsensitivityschedulebyid) | **GET** /api/v4/profile/sensitivity/by-id/{id} | Get a sensitivity schedule by ID |
| [**ProfileGetSensitivitySchedulesByName**](ProfileApi.md#profilegetsensitivityschedulesbyname) | **GET** /api/v4/profile/sensitivity/{profileName} | Get sensitivity schedules by profile name |
| [**ProfileGetTargetRangeScheduleById**](ProfileApi.md#profilegettargetrangeschedulebyid) | **GET** /api/v4/profile/target-range/by-id/{id} | Get a target range schedule by ID |
| [**ProfileGetTargetRangeSchedulesByName**](ProfileApi.md#profilegettargetrangeschedulesbyname) | **GET** /api/v4/profile/target-range/{profileName} | Get target range schedules by profile name |
| [**ProfileGetTherapySettings**](ProfileApi.md#profilegettherapysettings) | **GET** /api/v4/profile/settings | Get all therapy settings with optional filtering |
| [**ProfileGetTherapySettingsById**](ProfileApi.md#profilegettherapysettingsbyid) | **GET** /api/v4/profile/settings/{id} | Get a therapy settings record by ID |
| [**ProfileGetTherapySettingsByName**](ProfileApi.md#profilegettherapysettingsbyname) | **GET** /api/v4/profile/settings/by-name/{profileName} | Get therapy settings by profile name |
| [**ProfileUpdateBasalSchedule**](ProfileApi.md#profileupdatebasalschedule) | **PUT** /api/v4/profile/basal/{id} | Update an existing basal schedule |
| [**ProfileUpdateCarbRatioSchedule**](ProfileApi.md#profileupdatecarbratioschedule) | **PUT** /api/v4/profile/carb-ratio/{id} | Update an existing carb ratio schedule |
| [**ProfileUpdateSensitivitySchedule**](ProfileApi.md#profileupdatesensitivityschedule) | **PUT** /api/v4/profile/sensitivity/{id} | Update an existing sensitivity schedule |
| [**ProfileUpdateTargetRangeSchedule**](ProfileApi.md#profileupdatetargetrangeschedule) | **PUT** /api/v4/profile/target-range/{id} | Update an existing target range schedule |
| [**ProfileUpdateTherapySettings**](ProfileApi.md#profileupdatetherapysettings) | **PUT** /api/v4/profile/settings/{id} | Update an existing therapy settings record |

<a id="profilecreatebasalschedule"></a>
# **ProfileCreateBasalSchedule**
> BasalSchedule ProfileCreateBasalSchedule (BasalSchedule basalSchedule)

Create a new basal schedule

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileCreateBasalScheduleExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var basalSchedule = new BasalSchedule(); // BasalSchedule | 

            try
            {
                // Create a new basal schedule
                BasalSchedule result = apiInstance.ProfileCreateBasalSchedule(basalSchedule);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileCreateBasalSchedule: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileCreateBasalScheduleWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Create a new basal schedule
    ApiResponse<BasalSchedule> response = apiInstance.ProfileCreateBasalScheduleWithHttpInfo(basalSchedule);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileCreateBasalScheduleWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **basalSchedule** | [**BasalSchedule**](BasalSchedule.md) |  |  |

### Return type

[**BasalSchedule**](BasalSchedule.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **201** |  |  -  |
| **400** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profilecreatecarbratioschedule"></a>
# **ProfileCreateCarbRatioSchedule**
> CarbRatioSchedule ProfileCreateCarbRatioSchedule (CarbRatioSchedule carbRatioSchedule)

Create a new carb ratio schedule

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileCreateCarbRatioScheduleExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var carbRatioSchedule = new CarbRatioSchedule(); // CarbRatioSchedule | 

            try
            {
                // Create a new carb ratio schedule
                CarbRatioSchedule result = apiInstance.ProfileCreateCarbRatioSchedule(carbRatioSchedule);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileCreateCarbRatioSchedule: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileCreateCarbRatioScheduleWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Create a new carb ratio schedule
    ApiResponse<CarbRatioSchedule> response = apiInstance.ProfileCreateCarbRatioScheduleWithHttpInfo(carbRatioSchedule);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileCreateCarbRatioScheduleWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **carbRatioSchedule** | [**CarbRatioSchedule**](CarbRatioSchedule.md) |  |  |

### Return type

[**CarbRatioSchedule**](CarbRatioSchedule.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **201** |  |  -  |
| **400** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profilecreatesensitivityschedule"></a>
# **ProfileCreateSensitivitySchedule**
> SensitivitySchedule ProfileCreateSensitivitySchedule (SensitivitySchedule sensitivitySchedule)

Create a new sensitivity schedule

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileCreateSensitivityScheduleExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var sensitivitySchedule = new SensitivitySchedule(); // SensitivitySchedule | 

            try
            {
                // Create a new sensitivity schedule
                SensitivitySchedule result = apiInstance.ProfileCreateSensitivitySchedule(sensitivitySchedule);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileCreateSensitivitySchedule: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileCreateSensitivityScheduleWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Create a new sensitivity schedule
    ApiResponse<SensitivitySchedule> response = apiInstance.ProfileCreateSensitivityScheduleWithHttpInfo(sensitivitySchedule);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileCreateSensitivityScheduleWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **sensitivitySchedule** | [**SensitivitySchedule**](SensitivitySchedule.md) |  |  |

### Return type

[**SensitivitySchedule**](SensitivitySchedule.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **201** |  |  -  |
| **400** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profilecreatetargetrangeschedule"></a>
# **ProfileCreateTargetRangeSchedule**
> TargetRangeSchedule ProfileCreateTargetRangeSchedule (TargetRangeSchedule targetRangeSchedule)

Create a new target range schedule

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileCreateTargetRangeScheduleExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var targetRangeSchedule = new TargetRangeSchedule(); // TargetRangeSchedule | 

            try
            {
                // Create a new target range schedule
                TargetRangeSchedule result = apiInstance.ProfileCreateTargetRangeSchedule(targetRangeSchedule);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileCreateTargetRangeSchedule: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileCreateTargetRangeScheduleWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Create a new target range schedule
    ApiResponse<TargetRangeSchedule> response = apiInstance.ProfileCreateTargetRangeScheduleWithHttpInfo(targetRangeSchedule);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileCreateTargetRangeScheduleWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **targetRangeSchedule** | [**TargetRangeSchedule**](TargetRangeSchedule.md) |  |  |

### Return type

[**TargetRangeSchedule**](TargetRangeSchedule.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **201** |  |  -  |
| **400** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profilecreatetherapysettings"></a>
# **ProfileCreateTherapySettings**
> TherapySettings ProfileCreateTherapySettings (TherapySettings therapySettings)

Create a new therapy settings record

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileCreateTherapySettingsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var therapySettings = new TherapySettings(); // TherapySettings | 

            try
            {
                // Create a new therapy settings record
                TherapySettings result = apiInstance.ProfileCreateTherapySettings(therapySettings);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileCreateTherapySettings: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileCreateTherapySettingsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Create a new therapy settings record
    ApiResponse<TherapySettings> response = apiInstance.ProfileCreateTherapySettingsWithHttpInfo(therapySettings);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileCreateTherapySettingsWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **therapySettings** | [**TherapySettings**](TherapySettings.md) |  |  |

### Return type

[**TherapySettings**](TherapySettings.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **201** |  |  -  |
| **400** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profiledeletebasalschedule"></a>
# **ProfileDeleteBasalSchedule**
> void ProfileDeleteBasalSchedule (string id)

Delete a basal schedule

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileDeleteBasalScheduleExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 

            try
            {
                // Delete a basal schedule
                apiInstance.ProfileDeleteBasalSchedule(id);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileDeleteBasalSchedule: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileDeleteBasalScheduleWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Delete a basal schedule
    apiInstance.ProfileDeleteBasalScheduleWithHttpInfo(id);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileDeleteBasalScheduleWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |

### Return type

void (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **204** |  |  -  |
| **404** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profiledeletecarbratioschedule"></a>
# **ProfileDeleteCarbRatioSchedule**
> void ProfileDeleteCarbRatioSchedule (string id)

Delete a carb ratio schedule

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileDeleteCarbRatioScheduleExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 

            try
            {
                // Delete a carb ratio schedule
                apiInstance.ProfileDeleteCarbRatioSchedule(id);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileDeleteCarbRatioSchedule: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileDeleteCarbRatioScheduleWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Delete a carb ratio schedule
    apiInstance.ProfileDeleteCarbRatioScheduleWithHttpInfo(id);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileDeleteCarbRatioScheduleWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |

### Return type

void (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **204** |  |  -  |
| **404** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profiledeletesensitivityschedule"></a>
# **ProfileDeleteSensitivitySchedule**
> void ProfileDeleteSensitivitySchedule (string id)

Delete a sensitivity schedule

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileDeleteSensitivityScheduleExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 

            try
            {
                // Delete a sensitivity schedule
                apiInstance.ProfileDeleteSensitivitySchedule(id);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileDeleteSensitivitySchedule: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileDeleteSensitivityScheduleWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Delete a sensitivity schedule
    apiInstance.ProfileDeleteSensitivityScheduleWithHttpInfo(id);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileDeleteSensitivityScheduleWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |

### Return type

void (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **204** |  |  -  |
| **404** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profiledeletetargetrangeschedule"></a>
# **ProfileDeleteTargetRangeSchedule**
> void ProfileDeleteTargetRangeSchedule (string id)

Delete a target range schedule

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileDeleteTargetRangeScheduleExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 

            try
            {
                // Delete a target range schedule
                apiInstance.ProfileDeleteTargetRangeSchedule(id);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileDeleteTargetRangeSchedule: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileDeleteTargetRangeScheduleWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Delete a target range schedule
    apiInstance.ProfileDeleteTargetRangeScheduleWithHttpInfo(id);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileDeleteTargetRangeScheduleWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |

### Return type

void (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **204** |  |  -  |
| **404** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profiledeletetherapysettings"></a>
# **ProfileDeleteTherapySettings**
> void ProfileDeleteTherapySettings (string id)

Delete a therapy settings record

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileDeleteTherapySettingsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 

            try
            {
                // Delete a therapy settings record
                apiInstance.ProfileDeleteTherapySettings(id);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileDeleteTherapySettings: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileDeleteTherapySettingsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Delete a therapy settings record
    apiInstance.ProfileDeleteTherapySettingsWithHttpInfo(id);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileDeleteTherapySettingsWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |

### Return type

void (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **204** |  |  -  |
| **404** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profilegetbasalschedulebyid"></a>
# **ProfileGetBasalScheduleById**
> BasalSchedule ProfileGetBasalScheduleById (string id)

Get a basal schedule by ID

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileGetBasalScheduleByIdExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 

            try
            {
                // Get a basal schedule by ID
                BasalSchedule result = apiInstance.ProfileGetBasalScheduleById(id);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileGetBasalScheduleById: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileGetBasalScheduleByIdWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get a basal schedule by ID
    ApiResponse<BasalSchedule> response = apiInstance.ProfileGetBasalScheduleByIdWithHttpInfo(id);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileGetBasalScheduleByIdWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |

### Return type

[**BasalSchedule**](BasalSchedule.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |
| **404** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profilegetbasalschedulesbyname"></a>
# **ProfileGetBasalSchedulesByName**
> List&lt;BasalSchedule&gt; ProfileGetBasalSchedulesByName (string profileName)

Get basal schedules by profile name

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileGetBasalSchedulesByNameExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var profileName = "profileName_example";  // string | 

            try
            {
                // Get basal schedules by profile name
                List<BasalSchedule> result = apiInstance.ProfileGetBasalSchedulesByName(profileName);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileGetBasalSchedulesByName: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileGetBasalSchedulesByNameWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get basal schedules by profile name
    ApiResponse<List<BasalSchedule>> response = apiInstance.ProfileGetBasalSchedulesByNameWithHttpInfo(profileName);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileGetBasalSchedulesByNameWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **profileName** | **string** |  |  |

### Return type

[**List&lt;BasalSchedule&gt;**](BasalSchedule.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profilegetcarbratioschedulebyid"></a>
# **ProfileGetCarbRatioScheduleById**
> CarbRatioSchedule ProfileGetCarbRatioScheduleById (string id)

Get a carb ratio schedule by ID

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileGetCarbRatioScheduleByIdExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 

            try
            {
                // Get a carb ratio schedule by ID
                CarbRatioSchedule result = apiInstance.ProfileGetCarbRatioScheduleById(id);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileGetCarbRatioScheduleById: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileGetCarbRatioScheduleByIdWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get a carb ratio schedule by ID
    ApiResponse<CarbRatioSchedule> response = apiInstance.ProfileGetCarbRatioScheduleByIdWithHttpInfo(id);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileGetCarbRatioScheduleByIdWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |

### Return type

[**CarbRatioSchedule**](CarbRatioSchedule.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |
| **404** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profilegetcarbratioschedulesbyname"></a>
# **ProfileGetCarbRatioSchedulesByName**
> List&lt;CarbRatioSchedule&gt; ProfileGetCarbRatioSchedulesByName (string profileName)

Get carb ratio schedules by profile name

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileGetCarbRatioSchedulesByNameExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var profileName = "profileName_example";  // string | 

            try
            {
                // Get carb ratio schedules by profile name
                List<CarbRatioSchedule> result = apiInstance.ProfileGetCarbRatioSchedulesByName(profileName);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileGetCarbRatioSchedulesByName: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileGetCarbRatioSchedulesByNameWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get carb ratio schedules by profile name
    ApiResponse<List<CarbRatioSchedule>> response = apiInstance.ProfileGetCarbRatioSchedulesByNameWithHttpInfo(profileName);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileGetCarbRatioSchedulesByNameWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **profileName** | **string** |  |  |

### Return type

[**List&lt;CarbRatioSchedule&gt;**](CarbRatioSchedule.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profilegetprofilerecords"></a>
# **ProfileGetProfileRecords**
> PaginatedResponseOfProfile ProfileGetProfileRecords (int? limit = null, int? offset = null)

Get legacy Nightscout-shaped profile records projected from V4 schedule data. Intended for connector consumption where the caller needs the monolithic Profile shape (store with basal/carbratio/sens/target arrays).

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileGetProfileRecordsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var limit = 100;  // int? |  (optional)  (default to 100)
            var offset = 0;  // int? |  (optional)  (default to 0)

            try
            {
                // Get legacy Nightscout-shaped profile records projected from V4 schedule data. Intended for connector consumption where the caller needs the monolithic Profile shape (store with basal/carbratio/sens/target arrays).
                PaginatedResponseOfProfile result = apiInstance.ProfileGetProfileRecords(limit, offset);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileGetProfileRecords: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileGetProfileRecordsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get legacy Nightscout-shaped profile records projected from V4 schedule data. Intended for connector consumption where the caller needs the monolithic Profile shape (store with basal/carbratio/sens/target arrays).
    ApiResponse<PaginatedResponseOfProfile> response = apiInstance.ProfileGetProfileRecordsWithHttpInfo(limit, offset);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileGetProfileRecordsWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **limit** | **int?** |  | [optional] [default to 100] |
| **offset** | **int?** |  | [optional] [default to 0] |

### Return type

[**PaginatedResponseOfProfile**](PaginatedResponseOfProfile.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profilegetprofilesummary"></a>
# **ProfileGetProfileSummary**
> ProfileSummary ProfileGetProfileSummary (DateTimeOffset? from = null, DateTimeOffset? to = null)

Get a consolidated summary of all profile data across all profile names. Optionally provide a date range to include schedule change detection info.

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileGetProfileSummaryExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var from = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? |  (optional) 
            var to = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? |  (optional) 

            try
            {
                // Get a consolidated summary of all profile data across all profile names. Optionally provide a date range to include schedule change detection info.
                ProfileSummary result = apiInstance.ProfileGetProfileSummary(from, to);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileGetProfileSummary: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileGetProfileSummaryWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get a consolidated summary of all profile data across all profile names. Optionally provide a date range to include schedule change detection info.
    ApiResponse<ProfileSummary> response = apiInstance.ProfileGetProfileSummaryWithHttpInfo(from, to);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileGetProfileSummaryWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **from** | **DateTimeOffset?** |  | [optional]  |
| **to** | **DateTimeOffset?** |  | [optional]  |

### Return type

[**ProfileSummary**](ProfileSummary.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profilegetsensitivityschedulebyid"></a>
# **ProfileGetSensitivityScheduleById**
> SensitivitySchedule ProfileGetSensitivityScheduleById (string id)

Get a sensitivity schedule by ID

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileGetSensitivityScheduleByIdExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 

            try
            {
                // Get a sensitivity schedule by ID
                SensitivitySchedule result = apiInstance.ProfileGetSensitivityScheduleById(id);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileGetSensitivityScheduleById: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileGetSensitivityScheduleByIdWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get a sensitivity schedule by ID
    ApiResponse<SensitivitySchedule> response = apiInstance.ProfileGetSensitivityScheduleByIdWithHttpInfo(id);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileGetSensitivityScheduleByIdWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |

### Return type

[**SensitivitySchedule**](SensitivitySchedule.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |
| **404** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profilegetsensitivityschedulesbyname"></a>
# **ProfileGetSensitivitySchedulesByName**
> List&lt;SensitivitySchedule&gt; ProfileGetSensitivitySchedulesByName (string profileName)

Get sensitivity schedules by profile name

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileGetSensitivitySchedulesByNameExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var profileName = "profileName_example";  // string | 

            try
            {
                // Get sensitivity schedules by profile name
                List<SensitivitySchedule> result = apiInstance.ProfileGetSensitivitySchedulesByName(profileName);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileGetSensitivitySchedulesByName: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileGetSensitivitySchedulesByNameWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get sensitivity schedules by profile name
    ApiResponse<List<SensitivitySchedule>> response = apiInstance.ProfileGetSensitivitySchedulesByNameWithHttpInfo(profileName);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileGetSensitivitySchedulesByNameWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **profileName** | **string** |  |  |

### Return type

[**List&lt;SensitivitySchedule&gt;**](SensitivitySchedule.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profilegettargetrangeschedulebyid"></a>
# **ProfileGetTargetRangeScheduleById**
> TargetRangeSchedule ProfileGetTargetRangeScheduleById (string id)

Get a target range schedule by ID

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileGetTargetRangeScheduleByIdExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 

            try
            {
                // Get a target range schedule by ID
                TargetRangeSchedule result = apiInstance.ProfileGetTargetRangeScheduleById(id);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileGetTargetRangeScheduleById: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileGetTargetRangeScheduleByIdWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get a target range schedule by ID
    ApiResponse<TargetRangeSchedule> response = apiInstance.ProfileGetTargetRangeScheduleByIdWithHttpInfo(id);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileGetTargetRangeScheduleByIdWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |

### Return type

[**TargetRangeSchedule**](TargetRangeSchedule.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |
| **404** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profilegettargetrangeschedulesbyname"></a>
# **ProfileGetTargetRangeSchedulesByName**
> List&lt;TargetRangeSchedule&gt; ProfileGetTargetRangeSchedulesByName (string profileName)

Get target range schedules by profile name

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileGetTargetRangeSchedulesByNameExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var profileName = "profileName_example";  // string | 

            try
            {
                // Get target range schedules by profile name
                List<TargetRangeSchedule> result = apiInstance.ProfileGetTargetRangeSchedulesByName(profileName);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileGetTargetRangeSchedulesByName: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileGetTargetRangeSchedulesByNameWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get target range schedules by profile name
    ApiResponse<List<TargetRangeSchedule>> response = apiInstance.ProfileGetTargetRangeSchedulesByNameWithHttpInfo(profileName);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileGetTargetRangeSchedulesByNameWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **profileName** | **string** |  |  |

### Return type

[**List&lt;TargetRangeSchedule&gt;**](TargetRangeSchedule.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profilegettherapysettings"></a>
# **ProfileGetTherapySettings**
> PaginatedResponseOfTherapySettings ProfileGetTherapySettings (DateTimeOffset? from = null, DateTimeOffset? to = null, int? limit = null, int? offset = null, string? sort = null, string? device = null, string? source = null)

Get all therapy settings with optional filtering

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileGetTherapySettingsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var from = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? |  (optional) 
            var to = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? |  (optional) 
            var limit = 100;  // int? |  (optional)  (default to 100)
            var offset = 0;  // int? |  (optional)  (default to 0)
            var sort = "\"timestamp_desc\"";  // string? |  (optional)  (default to "timestamp_desc")
            var device = "device_example";  // string? |  (optional) 
            var source = "source_example";  // string? |  (optional) 

            try
            {
                // Get all therapy settings with optional filtering
                PaginatedResponseOfTherapySettings result = apiInstance.ProfileGetTherapySettings(from, to, limit, offset, sort, device, source);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileGetTherapySettings: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileGetTherapySettingsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get all therapy settings with optional filtering
    ApiResponse<PaginatedResponseOfTherapySettings> response = apiInstance.ProfileGetTherapySettingsWithHttpInfo(from, to, limit, offset, sort, device, source);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileGetTherapySettingsWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **from** | **DateTimeOffset?** |  | [optional]  |
| **to** | **DateTimeOffset?** |  | [optional]  |
| **limit** | **int?** |  | [optional] [default to 100] |
| **offset** | **int?** |  | [optional] [default to 0] |
| **sort** | **string?** |  | [optional] [default to &quot;timestamp_desc&quot;] |
| **device** | **string?** |  | [optional]  |
| **source** | **string?** |  | [optional]  |

### Return type

[**PaginatedResponseOfTherapySettings**](PaginatedResponseOfTherapySettings.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |
| **400** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profilegettherapysettingsbyid"></a>
# **ProfileGetTherapySettingsById**
> TherapySettings ProfileGetTherapySettingsById (string id)

Get a therapy settings record by ID

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileGetTherapySettingsByIdExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 

            try
            {
                // Get a therapy settings record by ID
                TherapySettings result = apiInstance.ProfileGetTherapySettingsById(id);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileGetTherapySettingsById: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileGetTherapySettingsByIdWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get a therapy settings record by ID
    ApiResponse<TherapySettings> response = apiInstance.ProfileGetTherapySettingsByIdWithHttpInfo(id);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileGetTherapySettingsByIdWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |

### Return type

[**TherapySettings**](TherapySettings.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |
| **404** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profilegettherapysettingsbyname"></a>
# **ProfileGetTherapySettingsByName**
> List&lt;TherapySettings&gt; ProfileGetTherapySettingsByName (string profileName)

Get therapy settings by profile name

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileGetTherapySettingsByNameExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var profileName = "profileName_example";  // string | 

            try
            {
                // Get therapy settings by profile name
                List<TherapySettings> result = apiInstance.ProfileGetTherapySettingsByName(profileName);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileGetTherapySettingsByName: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileGetTherapySettingsByNameWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get therapy settings by profile name
    ApiResponse<List<TherapySettings>> response = apiInstance.ProfileGetTherapySettingsByNameWithHttpInfo(profileName);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileGetTherapySettingsByNameWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **profileName** | **string** |  |  |

### Return type

[**List&lt;TherapySettings&gt;**](TherapySettings.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profileupdatebasalschedule"></a>
# **ProfileUpdateBasalSchedule**
> BasalSchedule ProfileUpdateBasalSchedule (string id, BasalSchedule basalSchedule)

Update an existing basal schedule

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileUpdateBasalScheduleExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 
            var basalSchedule = new BasalSchedule(); // BasalSchedule | 

            try
            {
                // Update an existing basal schedule
                BasalSchedule result = apiInstance.ProfileUpdateBasalSchedule(id, basalSchedule);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileUpdateBasalSchedule: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileUpdateBasalScheduleWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Update an existing basal schedule
    ApiResponse<BasalSchedule> response = apiInstance.ProfileUpdateBasalScheduleWithHttpInfo(id, basalSchedule);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileUpdateBasalScheduleWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |
| **basalSchedule** | [**BasalSchedule**](BasalSchedule.md) |  |  |

### Return type

[**BasalSchedule**](BasalSchedule.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |
| **400** |  |  -  |
| **404** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profileupdatecarbratioschedule"></a>
# **ProfileUpdateCarbRatioSchedule**
> CarbRatioSchedule ProfileUpdateCarbRatioSchedule (string id, CarbRatioSchedule carbRatioSchedule)

Update an existing carb ratio schedule

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileUpdateCarbRatioScheduleExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 
            var carbRatioSchedule = new CarbRatioSchedule(); // CarbRatioSchedule | 

            try
            {
                // Update an existing carb ratio schedule
                CarbRatioSchedule result = apiInstance.ProfileUpdateCarbRatioSchedule(id, carbRatioSchedule);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileUpdateCarbRatioSchedule: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileUpdateCarbRatioScheduleWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Update an existing carb ratio schedule
    ApiResponse<CarbRatioSchedule> response = apiInstance.ProfileUpdateCarbRatioScheduleWithHttpInfo(id, carbRatioSchedule);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileUpdateCarbRatioScheduleWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |
| **carbRatioSchedule** | [**CarbRatioSchedule**](CarbRatioSchedule.md) |  |  |

### Return type

[**CarbRatioSchedule**](CarbRatioSchedule.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |
| **400** |  |  -  |
| **404** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profileupdatesensitivityschedule"></a>
# **ProfileUpdateSensitivitySchedule**
> SensitivitySchedule ProfileUpdateSensitivitySchedule (string id, SensitivitySchedule sensitivitySchedule)

Update an existing sensitivity schedule

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileUpdateSensitivityScheduleExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 
            var sensitivitySchedule = new SensitivitySchedule(); // SensitivitySchedule | 

            try
            {
                // Update an existing sensitivity schedule
                SensitivitySchedule result = apiInstance.ProfileUpdateSensitivitySchedule(id, sensitivitySchedule);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileUpdateSensitivitySchedule: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileUpdateSensitivityScheduleWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Update an existing sensitivity schedule
    ApiResponse<SensitivitySchedule> response = apiInstance.ProfileUpdateSensitivityScheduleWithHttpInfo(id, sensitivitySchedule);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileUpdateSensitivityScheduleWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |
| **sensitivitySchedule** | [**SensitivitySchedule**](SensitivitySchedule.md) |  |  |

### Return type

[**SensitivitySchedule**](SensitivitySchedule.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |
| **400** |  |  -  |
| **404** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profileupdatetargetrangeschedule"></a>
# **ProfileUpdateTargetRangeSchedule**
> TargetRangeSchedule ProfileUpdateTargetRangeSchedule (string id, TargetRangeSchedule targetRangeSchedule)

Update an existing target range schedule

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileUpdateTargetRangeScheduleExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 
            var targetRangeSchedule = new TargetRangeSchedule(); // TargetRangeSchedule | 

            try
            {
                // Update an existing target range schedule
                TargetRangeSchedule result = apiInstance.ProfileUpdateTargetRangeSchedule(id, targetRangeSchedule);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileUpdateTargetRangeSchedule: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileUpdateTargetRangeScheduleWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Update an existing target range schedule
    ApiResponse<TargetRangeSchedule> response = apiInstance.ProfileUpdateTargetRangeScheduleWithHttpInfo(id, targetRangeSchedule);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileUpdateTargetRangeScheduleWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |
| **targetRangeSchedule** | [**TargetRangeSchedule**](TargetRangeSchedule.md) |  |  |

### Return type

[**TargetRangeSchedule**](TargetRangeSchedule.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |
| **400** |  |  -  |
| **404** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="profileupdatetherapysettings"></a>
# **ProfileUpdateTherapySettings**
> TherapySettings ProfileUpdateTherapySettings (string id, TherapySettings therapySettings)

Update an existing therapy settings record

### Example
```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using NightscoutFoundation.Nocturne.Api;
using NightscoutFoundation.Nocturne.Client;
using NightscoutFoundation.Nocturne.Model;

namespace Example
{
    public class ProfileUpdateTherapySettingsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ProfileApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 
            var therapySettings = new TherapySettings(); // TherapySettings | 

            try
            {
                // Update an existing therapy settings record
                TherapySettings result = apiInstance.ProfileUpdateTherapySettings(id, therapySettings);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ProfileApi.ProfileUpdateTherapySettings: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ProfileUpdateTherapySettingsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Update an existing therapy settings record
    ApiResponse<TherapySettings> response = apiInstance.ProfileUpdateTherapySettingsWithHttpInfo(id, therapySettings);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ProfileApi.ProfileUpdateTherapySettingsWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |
| **therapySettings** | [**TherapySettings**](TherapySettings.md) |  |  |

### Return type

[**TherapySettings**](TherapySettings.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |
| **400** |  |  -  |
| **404** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

