# NightscoutFoundation.Nocturne.Api.SupportApi

All URIs are relative to *http://localhost*

| Method | HTTP request | Description |
|--------|--------------|-------------|
| [**SupportCreateIssue**](SupportApi.md#supportcreateissue) | **POST** /api/v4/support/issues |  |
| [**SupportGetFallbackUrl**](SupportApi.md#supportgetfallbackurl) | **GET** /api/v4/support/issues/fallback-url | Returns a pre-filled GitHub new-issue URL for fallback when the API is unavailable. |
| [**SupportGetSupportConfig**](SupportApi.md#supportgetsupportconfig) | **GET** /api/v4/support/config | Returns operator support configuration for the frontend. When no operator is configured, accountBilling is null and the default GitHub flow applies. |

<a id="supportcreateissue"></a>
# **SupportCreateIssue**
> CreateIssueResponse SupportCreateIssue (string? template = null, string? title = null, string? description = null, string? stepsToReproduce = null, string? expectedBehavior = null, string? actualBehavior = null, string? cgmSource = null, string? timeRange = null, string? diagnosticInfo = null, List<FileParameter>? images = null)



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
    public class SupportCreateIssueExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new SupportApi(httpClient, config, httpClientHandler);
            var template = "template_example";  // string? |  (optional) 
            var title = "title_example";  // string? |  (optional) 
            var description = "description_example";  // string? |  (optional) 
            var stepsToReproduce = "stepsToReproduce_example";  // string? |  (optional) 
            var expectedBehavior = "expectedBehavior_example";  // string? |  (optional) 
            var actualBehavior = "actualBehavior_example";  // string? |  (optional) 
            var cgmSource = "cgmSource_example";  // string? |  (optional) 
            var timeRange = "timeRange_example";  // string? |  (optional) 
            var diagnosticInfo = "diagnosticInfo_example";  // string? |  (optional) 
            var images = new List<FileParameter>?(); // List<FileParameter>? |  (optional) 

            try
            {
                CreateIssueResponse result = apiInstance.SupportCreateIssue(template, title, description, stepsToReproduce, expectedBehavior, actualBehavior, cgmSource, timeRange, diagnosticInfo, images);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling SupportApi.SupportCreateIssue: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the SupportCreateIssueWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    ApiResponse<CreateIssueResponse> response = apiInstance.SupportCreateIssueWithHttpInfo(template, title, description, stepsToReproduce, expectedBehavior, actualBehavior, cgmSource, timeRange, diagnosticInfo, images);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling SupportApi.SupportCreateIssueWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **template** | **string?** |  | [optional]  |
| **title** | **string?** |  | [optional]  |
| **description** | **string?** |  | [optional]  |
| **stepsToReproduce** | **string?** |  | [optional]  |
| **expectedBehavior** | **string?** |  | [optional]  |
| **actualBehavior** | **string?** |  | [optional]  |
| **cgmSource** | **string?** |  | [optional]  |
| **timeRange** | **string?** |  | [optional]  |
| **diagnosticInfo** | **string?** |  | [optional]  |
| **images** | **List&lt;FileParameter&gt;?** |  | [optional]  |

### Return type

[**CreateIssueResponse**](CreateIssueResponse.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: multipart/form-data
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **201** |  |  -  |
| **400** |  |  -  |
| **502** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="supportgetfallbackurl"></a>
# **SupportGetFallbackUrl**
> FallbackUrlResponse SupportGetFallbackUrl (string? template = null, string? title = null, string? body = null)

Returns a pre-filled GitHub new-issue URL for fallback when the API is unavailable.

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
    public class SupportGetFallbackUrlExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new SupportApi(httpClient, config, httpClientHandler);
            var template = "template_example";  // string? |  (optional) 
            var title = "title_example";  // string? |  (optional) 
            var body = "body_example";  // string? |  (optional) 

            try
            {
                // Returns a pre-filled GitHub new-issue URL for fallback when the API is unavailable.
                FallbackUrlResponse result = apiInstance.SupportGetFallbackUrl(template, title, body);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling SupportApi.SupportGetFallbackUrl: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the SupportGetFallbackUrlWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Returns a pre-filled GitHub new-issue URL for fallback when the API is unavailable.
    ApiResponse<FallbackUrlResponse> response = apiInstance.SupportGetFallbackUrlWithHttpInfo(template, title, body);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling SupportApi.SupportGetFallbackUrlWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **template** | **string?** |  | [optional]  |
| **title** | **string?** |  | [optional]  |
| **body** | **string?** |  | [optional]  |

### Return type

[**FallbackUrlResponse**](FallbackUrlResponse.md)

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

<a id="supportgetsupportconfig"></a>
# **SupportGetSupportConfig**
> SupportConfigResponse SupportGetSupportConfig ()

Returns operator support configuration for the frontend. When no operator is configured, accountBilling is null and the default GitHub flow applies.

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
    public class SupportGetSupportConfigExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new SupportApi(httpClient, config, httpClientHandler);

            try
            {
                // Returns operator support configuration for the frontend. When no operator is configured, accountBilling is null and the default GitHub flow applies.
                SupportConfigResponse result = apiInstance.SupportGetSupportConfig();
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling SupportApi.SupportGetSupportConfig: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the SupportGetSupportConfigWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Returns operator support configuration for the frontend. When no operator is configured, accountBilling is null and the default GitHub flow applies.
    ApiResponse<SupportConfigResponse> response = apiInstance.SupportGetSupportConfigWithHttpInfo();
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling SupportApi.SupportGetSupportConfigWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters
This endpoint does not need any parameter.
### Return type

[**SupportConfigResponse**](SupportConfigResponse.md)

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

