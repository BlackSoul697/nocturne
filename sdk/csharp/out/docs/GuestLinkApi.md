# NightscoutFoundation.Nocturne.Api.GuestLinkApi

All URIs are relative to *http://localhost*

| Method | HTTP request | Description |
|--------|--------------|-------------|
| [**GuestLinkActivateGuestLink**](GuestLinkApi.md#guestlinkactivateguestlink) | **POST** /api/v4/guest-links/activate | Activate a guest link by code and receive a session cookie. |
| [**GuestLinkCreateGuestLink**](GuestLinkApi.md#guestlinkcreateguestlink) | **POST** /api/v4/guest-links | Create a new guest link for temporary read-only data sharing. |
| [**GuestLinkGetGuestLinks**](GuestLinkApi.md#guestlinkgetguestlinks) | **GET** /api/v4/guest-links | List guest links for the current user&#39;s effective subject. |
| [**GuestLinkRevokeGuestLink**](GuestLinkApi.md#guestlinkrevokeguestlink) | **DELETE** /api/v4/guest-links/{grantId} | Revoke an active guest link. |

<a id="guestlinkactivateguestlink"></a>
# **GuestLinkActivateGuestLink**
> ActivateGuestLinkResponse GuestLinkActivateGuestLink (ActivateGuestLinkRequest activateGuestLinkRequest)

Activate a guest link by code and receive a session cookie.

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
    public class GuestLinkActivateGuestLinkExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new GuestLinkApi(httpClient, config, httpClientHandler);
            var activateGuestLinkRequest = new ActivateGuestLinkRequest(); // ActivateGuestLinkRequest | 

            try
            {
                // Activate a guest link by code and receive a session cookie.
                ActivateGuestLinkResponse result = apiInstance.GuestLinkActivateGuestLink(activateGuestLinkRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling GuestLinkApi.GuestLinkActivateGuestLink: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the GuestLinkActivateGuestLinkWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Activate a guest link by code and receive a session cookie.
    ApiResponse<ActivateGuestLinkResponse> response = apiInstance.GuestLinkActivateGuestLinkWithHttpInfo(activateGuestLinkRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling GuestLinkApi.GuestLinkActivateGuestLinkWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **activateGuestLinkRequest** | [**ActivateGuestLinkRequest**](ActivateGuestLinkRequest.md) |  |  |

### Return type

[**ActivateGuestLinkResponse**](ActivateGuestLinkResponse.md)

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

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="guestlinkcreateguestlink"></a>
# **GuestLinkCreateGuestLink**
> GuestLinkCreationResult GuestLinkCreateGuestLink (CreateGuestLinkRequest createGuestLinkRequest)

Create a new guest link for temporary read-only data sharing.

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
    public class GuestLinkCreateGuestLinkExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new GuestLinkApi(httpClient, config, httpClientHandler);
            var createGuestLinkRequest = new CreateGuestLinkRequest(); // CreateGuestLinkRequest | 

            try
            {
                // Create a new guest link for temporary read-only data sharing.
                GuestLinkCreationResult result = apiInstance.GuestLinkCreateGuestLink(createGuestLinkRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling GuestLinkApi.GuestLinkCreateGuestLink: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the GuestLinkCreateGuestLinkWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Create a new guest link for temporary read-only data sharing.
    ApiResponse<GuestLinkCreationResult> response = apiInstance.GuestLinkCreateGuestLinkWithHttpInfo(createGuestLinkRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling GuestLinkApi.GuestLinkCreateGuestLinkWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **createGuestLinkRequest** | [**CreateGuestLinkRequest**](CreateGuestLinkRequest.md) |  |  |

### Return type

[**GuestLinkCreationResult**](GuestLinkCreationResult.md)

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
| **403** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="guestlinkgetguestlinks"></a>
# **GuestLinkGetGuestLinks**
> List&lt;GuestLinkInfo&gt; GuestLinkGetGuestLinks ()

List guest links for the current user's effective subject.

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
    public class GuestLinkGetGuestLinksExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new GuestLinkApi(httpClient, config, httpClientHandler);

            try
            {
                // List guest links for the current user's effective subject.
                List<GuestLinkInfo> result = apiInstance.GuestLinkGetGuestLinks();
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling GuestLinkApi.GuestLinkGetGuestLinks: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the GuestLinkGetGuestLinksWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // List guest links for the current user's effective subject.
    ApiResponse<List<GuestLinkInfo>> response = apiInstance.GuestLinkGetGuestLinksWithHttpInfo();
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling GuestLinkApi.GuestLinkGetGuestLinksWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters
This endpoint does not need any parameter.
### Return type

[**List&lt;GuestLinkInfo&gt;**](GuestLinkInfo.md)

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

<a id="guestlinkrevokeguestlink"></a>
# **GuestLinkRevokeGuestLink**
> void GuestLinkRevokeGuestLink (string grantId)

Revoke an active guest link.

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
    public class GuestLinkRevokeGuestLinkExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new GuestLinkApi(httpClient, config, httpClientHandler);
            var grantId = "grantId_example";  // string | 

            try
            {
                // Revoke an active guest link.
                apiInstance.GuestLinkRevokeGuestLink(grantId);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling GuestLinkApi.GuestLinkRevokeGuestLink: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the GuestLinkRevokeGuestLinkWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Revoke an active guest link.
    apiInstance.GuestLinkRevokeGuestLinkWithHttpInfo(grantId);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling GuestLinkApi.GuestLinkRevokeGuestLinkWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **grantId** | **string** |  |  |

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

