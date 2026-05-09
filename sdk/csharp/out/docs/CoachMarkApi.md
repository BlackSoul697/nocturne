# NightscoutFoundation.Nocturne.Api.CoachMarkApi

All URIs are relative to *http://localhost*

| Method | HTTP request | Description |
|--------|--------------|-------------|
| [**CoachMarkDeleteAll**](CoachMarkApi.md#coachmarkdeleteall) | **DELETE** /api/v4/coach-marks | Delete all coach mark states for the current user, resetting all tutorials. |
| [**CoachMarkGetAll**](CoachMarkApi.md#coachmarkgetall) | **GET** /api/v4/coach-marks | Get all coach mark states for the current user. |
| [**CoachMarkUpdateStatus**](CoachMarkApi.md#coachmarkupdatestatus) | **PATCH** /api/v4/coach-marks/{key} | Update a coach mark&#39;s status. |

<a id="coachmarkdeleteall"></a>
# **CoachMarkDeleteAll**
> FileParameter CoachMarkDeleteAll ()

Delete all coach mark states for the current user, resetting all tutorials.

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
    public class CoachMarkDeleteAllExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new CoachMarkApi(httpClient, config, httpClientHandler);

            try
            {
                // Delete all coach mark states for the current user, resetting all tutorials.
                FileParameter result = apiInstance.CoachMarkDeleteAll();
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling CoachMarkApi.CoachMarkDeleteAll: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the CoachMarkDeleteAllWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Delete all coach mark states for the current user, resetting all tutorials.
    ApiResponse<FileParameter> response = apiInstance.CoachMarkDeleteAllWithHttpInfo();
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling CoachMarkApi.CoachMarkDeleteAllWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters
This endpoint does not need any parameter.
### Return type

[**FileParameter**](FileParameter.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/octet-stream


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="coachmarkgetall"></a>
# **CoachMarkGetAll**
> List&lt;CoachMarkState&gt; CoachMarkGetAll ()

Get all coach mark states for the current user.

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
    public class CoachMarkGetAllExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new CoachMarkApi(httpClient, config, httpClientHandler);

            try
            {
                // Get all coach mark states for the current user.
                List<CoachMarkState> result = apiInstance.CoachMarkGetAll();
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling CoachMarkApi.CoachMarkGetAll: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the CoachMarkGetAllWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get all coach mark states for the current user.
    ApiResponse<List<CoachMarkState>> response = apiInstance.CoachMarkGetAllWithHttpInfo();
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling CoachMarkApi.CoachMarkGetAllWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters
This endpoint does not need any parameter.
### Return type

[**List&lt;CoachMarkState&gt;**](CoachMarkState.md)

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

<a id="coachmarkupdatestatus"></a>
# **CoachMarkUpdateStatus**
> CoachMarkState CoachMarkUpdateStatus (string key, UpdateCoachMarkRequest updateCoachMarkRequest)

Update a coach mark's status.

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
    public class CoachMarkUpdateStatusExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new CoachMarkApi(httpClient, config, httpClientHandler);
            var key = "key_example";  // string | The coach mark key to update.
            var updateCoachMarkRequest = new UpdateCoachMarkRequest(); // UpdateCoachMarkRequest | The new status value.

            try
            {
                // Update a coach mark's status.
                CoachMarkState result = apiInstance.CoachMarkUpdateStatus(key, updateCoachMarkRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling CoachMarkApi.CoachMarkUpdateStatus: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the CoachMarkUpdateStatusWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Update a coach mark's status.
    ApiResponse<CoachMarkState> response = apiInstance.CoachMarkUpdateStatusWithHttpInfo(key, updateCoachMarkRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling CoachMarkApi.CoachMarkUpdateStatusWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **key** | **string** | The coach mark key to update. |  |
| **updateCoachMarkRequest** | [**UpdateCoachMarkRequest**](UpdateCoachMarkRequest.md) | The new status value. |  |

### Return type

[**CoachMarkState**](CoachMarkState.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

