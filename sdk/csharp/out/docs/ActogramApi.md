# NightscoutFoundation.Nocturne.Api.ActogramApi

All URIs are relative to *http://localhost*

| Method | HTTP request | Description |
|--------|--------------|-------------|
| [**ActogramGetActogram**](ActogramApi.md#actogramgetactogram) | **GET** /api/v4/Actogram | Get actogram report data for a time window. |

<a id="actogramgetactogram"></a>
# **ActogramGetActogram**
> ActogramReportData ActogramGetActogram (long? startTime = null, long? endTime = null)

Get actogram report data for a time window.

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
    public class ActogramGetActogramExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new ActogramApi(httpClient, config, httpClientHandler);
            var startTime = 789L;  // long? | Start of the window as Unix milliseconds (inclusive). (optional) 
            var endTime = 789L;  // long? | End of the window as Unix milliseconds (exclusive).             Must be greater than startTime. (optional) 

            try
            {
                // Get actogram report data for a time window.
                ActogramReportData result = apiInstance.ActogramGetActogram(startTime, endTime);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling ActogramApi.ActogramGetActogram: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the ActogramGetActogramWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get actogram report data for a time window.
    ApiResponse<ActogramReportData> response = apiInstance.ActogramGetActogramWithHttpInfo(startTime, endTime);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling ActogramApi.ActogramGetActogramWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **startTime** | **long?** | Start of the window as Unix milliseconds (inclusive). | [optional]  |
| **endTime** | **long?** | End of the window as Unix milliseconds (exclusive).             Must be greater than startTime. | [optional]  |

### Return type

[**ActogramReportData**](ActogramReportData.md)

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
| **500** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

