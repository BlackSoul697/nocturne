# NightscoutFoundation.Nocturne.Api.PlatformApi

All URIs are relative to *http://localhost*

| Method | HTTP request | Description |
|--------|--------------|-------------|
| [**PlatformCreateTenant**](PlatformApi.md#platformcreatetenant) | **POST** /api/v4/platform/tenants | Creates a new tenant with the authenticated subject as owner. Requires OperatorConfiguration.AllowSelfServiceCreation to be enabled. |
| [**PlatformGetTenants**](PlatformApi.md#platformgettenants) | **GET** /api/v4/platform/tenants | Returns all tenants the authenticated subject is a member of. |
| [**PlatformGetTransitionStatus**](PlatformApi.md#platformgettransitionstatus) | **GET** /api/v4/platform/transition-status | Returns the current multitenancy configuration status. Used by the frontend to display subdomain URLs and transition notices. |

<a id="platformcreatetenant"></a>
# **PlatformCreateTenant**
> TenantCreatedDto PlatformCreateTenant (CreatePlatformTenantRequest createPlatformTenantRequest)

Creates a new tenant with the authenticated subject as owner. Requires OperatorConfiguration.AllowSelfServiceCreation to be enabled.

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
    public class PlatformCreateTenantExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new PlatformApi(httpClient, config, httpClientHandler);
            var createPlatformTenantRequest = new CreatePlatformTenantRequest(); // CreatePlatformTenantRequest | 

            try
            {
                // Creates a new tenant with the authenticated subject as owner. Requires OperatorConfiguration.AllowSelfServiceCreation to be enabled.
                TenantCreatedDto result = apiInstance.PlatformCreateTenant(createPlatformTenantRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling PlatformApi.PlatformCreateTenant: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the PlatformCreateTenantWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Creates a new tenant with the authenticated subject as owner. Requires OperatorConfiguration.AllowSelfServiceCreation to be enabled.
    ApiResponse<TenantCreatedDto> response = apiInstance.PlatformCreateTenantWithHttpInfo(createPlatformTenantRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling PlatformApi.PlatformCreateTenantWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **createPlatformTenantRequest** | [**CreatePlatformTenantRequest**](CreatePlatformTenantRequest.md) |  |  |

### Return type

[**TenantCreatedDto**](TenantCreatedDto.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **201** |  |  -  |
| **401** |  |  -  |
| **403** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="platformgettenants"></a>
# **PlatformGetTenants**
> List&lt;TenantDto&gt; PlatformGetTenants ()

Returns all tenants the authenticated subject is a member of.

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
    public class PlatformGetTenantsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new PlatformApi(httpClient, config, httpClientHandler);

            try
            {
                // Returns all tenants the authenticated subject is a member of.
                List<TenantDto> result = apiInstance.PlatformGetTenants();
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling PlatformApi.PlatformGetTenants: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the PlatformGetTenantsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Returns all tenants the authenticated subject is a member of.
    ApiResponse<List<TenantDto>> response = apiInstance.PlatformGetTenantsWithHttpInfo();
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling PlatformApi.PlatformGetTenantsWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters
This endpoint does not need any parameter.
### Return type

[**List&lt;TenantDto&gt;**](TenantDto.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |
| **401** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="platformgettransitionstatus"></a>
# **PlatformGetTransitionStatus**
> TransitionStatusDto PlatformGetTransitionStatus ()

Returns the current multitenancy configuration status. Used by the frontend to display subdomain URLs and transition notices.

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
    public class PlatformGetTransitionStatusExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new PlatformApi(httpClient, config, httpClientHandler);

            try
            {
                // Returns the current multitenancy configuration status. Used by the frontend to display subdomain URLs and transition notices.
                TransitionStatusDto result = apiInstance.PlatformGetTransitionStatus();
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling PlatformApi.PlatformGetTransitionStatus: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the PlatformGetTransitionStatusWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Returns the current multitenancy configuration status. Used by the frontend to display subdomain URLs and transition notices.
    ApiResponse<TransitionStatusDto> response = apiInstance.PlatformGetTransitionStatusWithHttpInfo();
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling PlatformApi.PlatformGetTransitionStatusWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters
This endpoint does not need any parameter.
### Return type

[**TransitionStatusDto**](TransitionStatusDto.md)

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

