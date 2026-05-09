# NightscoutFoundation.Nocturne.Api.HeartRateApi

All URIs are relative to *http://localhost*

| Method | HTTP request | Description |
|--------|--------------|-------------|
| [**HeartRateCreateHeartRates**](HeartRateApi.md#heartratecreateheartrates) | **POST** /api/v4/HeartRate | Create one or more heart rate records |
| [**HeartRateDeleteHeartRate**](HeartRateApi.md#heartratedeleteheartrate) | **DELETE** /api/v4/HeartRate/{id} | Delete a heart rate record by ID |
| [**HeartRateGetHeartRate**](HeartRateApi.md#heartrategetheartrate) | **GET** /api/v4/HeartRate/{id} | Get a specific heart rate record by ID |
| [**HeartRateGetHeartRates**](HeartRateApi.md#heartrategetheartrates) | **GET** /api/v4/HeartRate | Get heart rate records with optional pagination and date filtering |
| [**HeartRateUpdateHeartRate**](HeartRateApi.md#heartrateupdateheartrate) | **PUT** /api/v4/HeartRate/{id} | Update an existing heart rate record |

<a id="heartratecreateheartrates"></a>
# **HeartRateCreateHeartRates**
> List&lt;HeartRate&gt; HeartRateCreateHeartRates (List<UpsertHeartRateRequest> upsertHeartRateRequest)

Create one or more heart rate records

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
    public class HeartRateCreateHeartRatesExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new HeartRateApi(httpClient, config, httpClientHandler);
            var upsertHeartRateRequest = new List<UpsertHeartRateRequest>(); // List<UpsertHeartRateRequest> | 

            try
            {
                // Create one or more heart rate records
                List<HeartRate> result = apiInstance.HeartRateCreateHeartRates(upsertHeartRateRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling HeartRateApi.HeartRateCreateHeartRates: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the HeartRateCreateHeartRatesWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Create one or more heart rate records
    ApiResponse<List<HeartRate>> response = apiInstance.HeartRateCreateHeartRatesWithHttpInfo(upsertHeartRateRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling HeartRateApi.HeartRateCreateHeartRatesWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **upsertHeartRateRequest** | [**List&lt;UpsertHeartRateRequest&gt;**](UpsertHeartRateRequest.md) |  |  |

### Return type

[**List&lt;HeartRate&gt;**](HeartRate.md)

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
| **500** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="heartratedeleteheartrate"></a>
# **HeartRateDeleteHeartRate**
> void HeartRateDeleteHeartRate (string id)

Delete a heart rate record by ID

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
    public class HeartRateDeleteHeartRateExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new HeartRateApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 

            try
            {
                // Delete a heart rate record by ID
                apiInstance.HeartRateDeleteHeartRate(id);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling HeartRateApi.HeartRateDeleteHeartRate: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the HeartRateDeleteHeartRateWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Delete a heart rate record by ID
    apiInstance.HeartRateDeleteHeartRateWithHttpInfo(id);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling HeartRateApi.HeartRateDeleteHeartRateWithHttpInfo: " + e.Message);
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
| **200** |  |  -  |
| **404** |  |  -  |
| **500** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="heartrategetheartrate"></a>
# **HeartRateGetHeartRate**
> HeartRate HeartRateGetHeartRate (string id)

Get a specific heart rate record by ID

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
    public class HeartRateGetHeartRateExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new HeartRateApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | Record ID

            try
            {
                // Get a specific heart rate record by ID
                HeartRate result = apiInstance.HeartRateGetHeartRate(id);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling HeartRateApi.HeartRateGetHeartRate: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the HeartRateGetHeartRateWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get a specific heart rate record by ID
    ApiResponse<HeartRate> response = apiInstance.HeartRateGetHeartRateWithHttpInfo(id);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling HeartRateApi.HeartRateGetHeartRateWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** | Record ID |  |

### Return type

[**HeartRate**](HeartRate.md)

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
| **500** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="heartrategetheartrates"></a>
# **HeartRateGetHeartRates**
> List&lt;HeartRate&gt; HeartRateGetHeartRates (int? count = null, int? skip = null, DateTimeOffset? from = null, DateTimeOffset? to = null)

Get heart rate records with optional pagination and date filtering

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
    public class HeartRateGetHeartRatesExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new HeartRateApi(httpClient, config, httpClientHandler);
            var count = 10;  // int? | Maximum number of records to return (default: 10, ignored when from/to are specified) (optional)  (default to 10)
            var skip = 0;  // int? | Number of records to skip for pagination (default: 0, ignored when from/to are specified) (optional)  (default to 0)
            var from = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? | Start of date range (inclusive). When specified with 'to', returns all records in range. (optional) 
            var to = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? | End of date range (exclusive). When specified with 'from', returns all records in range. (optional) 

            try
            {
                // Get heart rate records with optional pagination and date filtering
                List<HeartRate> result = apiInstance.HeartRateGetHeartRates(count, skip, from, to);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling HeartRateApi.HeartRateGetHeartRates: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the HeartRateGetHeartRatesWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get heart rate records with optional pagination and date filtering
    ApiResponse<List<HeartRate>> response = apiInstance.HeartRateGetHeartRatesWithHttpInfo(count, skip, from, to);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling HeartRateApi.HeartRateGetHeartRatesWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **count** | **int?** | Maximum number of records to return (default: 10, ignored when from/to are specified) | [optional] [default to 10] |
| **skip** | **int?** | Number of records to skip for pagination (default: 0, ignored when from/to are specified) | [optional] [default to 0] |
| **from** | **DateTimeOffset?** | Start of date range (inclusive). When specified with &#39;to&#39;, returns all records in range. | [optional]  |
| **to** | **DateTimeOffset?** | End of date range (exclusive). When specified with &#39;from&#39;, returns all records in range. | [optional]  |

### Return type

[**List&lt;HeartRate&gt;**](HeartRate.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | List of heart rate records |  -  |
| **500** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="heartrateupdateheartrate"></a>
# **HeartRateUpdateHeartRate**
> HeartRate HeartRateUpdateHeartRate (string id, UpsertHeartRateRequest upsertHeartRateRequest)

Update an existing heart rate record

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
    public class HeartRateUpdateHeartRateExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new HeartRateApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 
            var upsertHeartRateRequest = new UpsertHeartRateRequest(); // UpsertHeartRateRequest | 

            try
            {
                // Update an existing heart rate record
                HeartRate result = apiInstance.HeartRateUpdateHeartRate(id, upsertHeartRateRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling HeartRateApi.HeartRateUpdateHeartRate: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the HeartRateUpdateHeartRateWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Update an existing heart rate record
    ApiResponse<HeartRate> response = apiInstance.HeartRateUpdateHeartRateWithHttpInfo(id, upsertHeartRateRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling HeartRateApi.HeartRateUpdateHeartRateWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |
| **upsertHeartRateRequest** | [**UpsertHeartRateRequest**](UpsertHeartRateRequest.md) |  |  |

### Return type

[**HeartRate**](HeartRate.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |
| **404** |  |  -  |
| **500** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

