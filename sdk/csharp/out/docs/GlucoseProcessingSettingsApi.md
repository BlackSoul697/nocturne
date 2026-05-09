# NightscoutFoundation.Nocturne.Api.GlucoseProcessingSettingsApi

All URIs are relative to *http://localhost*

| Method | HTTP request | Description |
|--------|--------------|-------------|
| [**GlucoseProcessingSettingsGetPreference**](GlucoseProcessingSettingsApi.md#glucoseprocessingsettingsgetpreference) | **GET** /api/v4/settings/glucose-processing/preference |  |
| [**GlucoseProcessingSettingsGetSourceDefaults**](GlucoseProcessingSettingsApi.md#glucoseprocessingsettingsgetsourcedefaults) | **GET** /api/v4/settings/glucose-processing/source-defaults |  |
| [**GlucoseProcessingSettingsSetPreference**](GlucoseProcessingSettingsApi.md#glucoseprocessingsettingssetpreference) | **PUT** /api/v4/settings/glucose-processing/preference |  |
| [**GlucoseProcessingSettingsSetSourceDefaults**](GlucoseProcessingSettingsApi.md#glucoseprocessingsettingssetsourcedefaults) | **PUT** /api/v4/settings/glucose-processing/source-defaults |  |

<a id="glucoseprocessingsettingsgetpreference"></a>
# **GlucoseProcessingSettingsGetPreference**
> GlucoseProcessingPreferenceResponse GlucoseProcessingSettingsGetPreference ()



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
    public class GlucoseProcessingSettingsGetPreferenceExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new GlucoseProcessingSettingsApi(httpClient, config, httpClientHandler);

            try
            {
                GlucoseProcessingPreferenceResponse result = apiInstance.GlucoseProcessingSettingsGetPreference();
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling GlucoseProcessingSettingsApi.GlucoseProcessingSettingsGetPreference: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the GlucoseProcessingSettingsGetPreferenceWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    ApiResponse<GlucoseProcessingPreferenceResponse> response = apiInstance.GlucoseProcessingSettingsGetPreferenceWithHttpInfo();
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling GlucoseProcessingSettingsApi.GlucoseProcessingSettingsGetPreferenceWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters
This endpoint does not need any parameter.
### Return type

[**GlucoseProcessingPreferenceResponse**](GlucoseProcessingPreferenceResponse.md)

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

<a id="glucoseprocessingsettingsgetsourcedefaults"></a>
# **GlucoseProcessingSettingsGetSourceDefaults**
> GlucoseProcessingSourceDefaultsResponse GlucoseProcessingSettingsGetSourceDefaults ()



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
    public class GlucoseProcessingSettingsGetSourceDefaultsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new GlucoseProcessingSettingsApi(httpClient, config, httpClientHandler);

            try
            {
                GlucoseProcessingSourceDefaultsResponse result = apiInstance.GlucoseProcessingSettingsGetSourceDefaults();
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling GlucoseProcessingSettingsApi.GlucoseProcessingSettingsGetSourceDefaults: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the GlucoseProcessingSettingsGetSourceDefaultsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    ApiResponse<GlucoseProcessingSourceDefaultsResponse> response = apiInstance.GlucoseProcessingSettingsGetSourceDefaultsWithHttpInfo();
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling GlucoseProcessingSettingsApi.GlucoseProcessingSettingsGetSourceDefaultsWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters
This endpoint does not need any parameter.
### Return type

[**GlucoseProcessingSourceDefaultsResponse**](GlucoseProcessingSourceDefaultsResponse.md)

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

<a id="glucoseprocessingsettingssetpreference"></a>
# **GlucoseProcessingSettingsSetPreference**
> void GlucoseProcessingSettingsSetPreference (SetGlucoseProcessingPreferenceRequest setGlucoseProcessingPreferenceRequest)



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
    public class GlucoseProcessingSettingsSetPreferenceExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new GlucoseProcessingSettingsApi(httpClient, config, httpClientHandler);
            var setGlucoseProcessingPreferenceRequest = new SetGlucoseProcessingPreferenceRequest(); // SetGlucoseProcessingPreferenceRequest | 

            try
            {
                apiInstance.GlucoseProcessingSettingsSetPreference(setGlucoseProcessingPreferenceRequest);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling GlucoseProcessingSettingsApi.GlucoseProcessingSettingsSetPreference: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the GlucoseProcessingSettingsSetPreferenceWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    apiInstance.GlucoseProcessingSettingsSetPreferenceWithHttpInfo(setGlucoseProcessingPreferenceRequest);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling GlucoseProcessingSettingsApi.GlucoseProcessingSettingsSetPreferenceWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **setGlucoseProcessingPreferenceRequest** | [**SetGlucoseProcessingPreferenceRequest**](SetGlucoseProcessingPreferenceRequest.md) |  |  |

### Return type

void (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: Not defined


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **204** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="glucoseprocessingsettingssetsourcedefaults"></a>
# **GlucoseProcessingSettingsSetSourceDefaults**
> void GlucoseProcessingSettingsSetSourceDefaults (SetGlucoseProcessingSourceDefaultsRequest setGlucoseProcessingSourceDefaultsRequest)



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
    public class GlucoseProcessingSettingsSetSourceDefaultsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new GlucoseProcessingSettingsApi(httpClient, config, httpClientHandler);
            var setGlucoseProcessingSourceDefaultsRequest = new SetGlucoseProcessingSourceDefaultsRequest(); // SetGlucoseProcessingSourceDefaultsRequest | 

            try
            {
                apiInstance.GlucoseProcessingSettingsSetSourceDefaults(setGlucoseProcessingSourceDefaultsRequest);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling GlucoseProcessingSettingsApi.GlucoseProcessingSettingsSetSourceDefaults: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the GlucoseProcessingSettingsSetSourceDefaultsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    apiInstance.GlucoseProcessingSettingsSetSourceDefaultsWithHttpInfo(setGlucoseProcessingSourceDefaultsRequest);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling GlucoseProcessingSettingsApi.GlucoseProcessingSettingsSetSourceDefaultsWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **setGlucoseProcessingSourceDefaultsRequest** | [**SetGlucoseProcessingSourceDefaultsRequest**](SetGlucoseProcessingSourceDefaultsRequest.md) |  |  |

### Return type

void (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: Not defined


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **204** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

