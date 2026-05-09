# NightscoutFoundation.Nocturne.Api.AvatarApi

All URIs are relative to *http://localhost*

| Method | HTTP request | Description |
|--------|--------------|-------------|
| [**AvatarDelete**](AvatarApi.md#avatardelete) | **DELETE** /api/v4/me/avatar | Delete the current subject&#39;s avatar. |
| [**AvatarGet**](AvatarApi.md#avatarget) | **GET** /api/v4/me/avatar | Serve the current subject&#39;s avatar image. |
| [**AvatarUpload**](AvatarApi.md#avatarupload) | **POST** /api/v4/me/avatar | Upload or replace the current subject&#39;s avatar. Image is resized to 256x256 WebP. |

<a id="avatardelete"></a>
# **AvatarDelete**
> void AvatarDelete ()

Delete the current subject's avatar.

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
    public class AvatarDeleteExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new AvatarApi(httpClient, config, httpClientHandler);

            try
            {
                // Delete the current subject's avatar.
                apiInstance.AvatarDelete();
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling AvatarApi.AvatarDelete: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the AvatarDeleteWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Delete the current subject's avatar.
    apiInstance.AvatarDeleteWithHttpInfo();
}
catch (ApiException e)
{
    Debug.Print("Exception when calling AvatarApi.AvatarDeleteWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters
This endpoint does not need any parameter.
### Return type

void (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: Not defined


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **204** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="avatarget"></a>
# **AvatarGet**
> void AvatarGet (string? id = null)

Serve the current subject's avatar image.

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
    public class AvatarGetExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new AvatarApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string? |  (optional) 

            try
            {
                // Serve the current subject's avatar image.
                apiInstance.AvatarGet(id);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling AvatarApi.AvatarGet: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the AvatarGetWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Serve the current subject's avatar image.
    apiInstance.AvatarGetWithHttpInfo(id);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling AvatarApi.AvatarGetWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string?** |  | [optional]  |

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
| **200** |  |  -  |
| **404** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="avatarupload"></a>
# **AvatarUpload**
> AvatarUploadResponse AvatarUpload (string? contentType = null, string? contentDisposition = null, List<Object>? headers = null, long? length = null, string? name = null, string? fileName = null)

Upload or replace the current subject's avatar. Image is resized to 256x256 WebP.

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
    public class AvatarUploadExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new AvatarApi(httpClient, config, httpClientHandler);
            var contentType = "contentType_example";  // string? |  (optional) 
            var contentDisposition = "contentDisposition_example";  // string? |  (optional) 
            var headers = new List<Object>?(); // List<Object>? |  (optional) 
            var length = 789L;  // long? |  (optional) 
            var name = "name_example";  // string? |  (optional) 
            var fileName = "fileName_example";  // string? |  (optional) 

            try
            {
                // Upload or replace the current subject's avatar. Image is resized to 256x256 WebP.
                AvatarUploadResponse result = apiInstance.AvatarUpload(contentType, contentDisposition, headers, length, name, fileName);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling AvatarApi.AvatarUpload: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the AvatarUploadWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Upload or replace the current subject's avatar. Image is resized to 256x256 WebP.
    ApiResponse<AvatarUploadResponse> response = apiInstance.AvatarUploadWithHttpInfo(contentType, contentDisposition, headers, length, name, fileName);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling AvatarApi.AvatarUploadWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **contentType** | **string?** |  | [optional]  |
| **contentDisposition** | **string?** |  | [optional]  |
| **headers** | [**List&lt;Object&gt;?**](Object.md) |  | [optional]  |
| **length** | **long?** |  | [optional]  |
| **name** | **string?** |  | [optional]  |
| **fileName** | **string?** |  | [optional]  |

### Return type

[**AvatarUploadResponse**](AvatarUploadResponse.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: multipart/form-data
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |
| **400** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

