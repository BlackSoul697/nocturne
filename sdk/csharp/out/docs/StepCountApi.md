# NightscoutFoundation.Nocturne.Api.StepCountApi

All URIs are relative to *http://localhost*

| Method | HTTP request | Description |
|--------|--------------|-------------|
| [**StepCountCreateStepCounts**](StepCountApi.md#stepcountcreatestepcounts) | **POST** /api/v4/StepCount | Create one or more step count records |
| [**StepCountDeleteStepCount**](StepCountApi.md#stepcountdeletestepcount) | **DELETE** /api/v4/StepCount/{id} | Delete a step count record by ID |
| [**StepCountGetStepCount**](StepCountApi.md#stepcountgetstepcount) | **GET** /api/v4/StepCount/{id} | Get a specific step count record by ID |
| [**StepCountGetStepCounts**](StepCountApi.md#stepcountgetstepcounts) | **GET** /api/v4/StepCount | Get step count records with optional pagination and date filtering |
| [**StepCountUpdateStepCount**](StepCountApi.md#stepcountupdatestepcount) | **PUT** /api/v4/StepCount/{id} | Update an existing step count record |

<a id="stepcountcreatestepcounts"></a>
# **StepCountCreateStepCounts**
> List&lt;StepCount&gt; StepCountCreateStepCounts (List<UpsertStepCountRequest> upsertStepCountRequest)

Create one or more step count records

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
    public class StepCountCreateStepCountsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StepCountApi(httpClient, config, httpClientHandler);
            var upsertStepCountRequest = new List<UpsertStepCountRequest>(); // List<UpsertStepCountRequest> | 

            try
            {
                // Create one or more step count records
                List<StepCount> result = apiInstance.StepCountCreateStepCounts(upsertStepCountRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StepCountApi.StepCountCreateStepCounts: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StepCountCreateStepCountsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Create one or more step count records
    ApiResponse<List<StepCount>> response = apiInstance.StepCountCreateStepCountsWithHttpInfo(upsertStepCountRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StepCountApi.StepCountCreateStepCountsWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **upsertStepCountRequest** | [**List&lt;UpsertStepCountRequest&gt;**](UpsertStepCountRequest.md) |  |  |

### Return type

[**List&lt;StepCount&gt;**](StepCount.md)

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

<a id="stepcountdeletestepcount"></a>
# **StepCountDeleteStepCount**
> void StepCountDeleteStepCount (string id)

Delete a step count record by ID

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
    public class StepCountDeleteStepCountExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StepCountApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 

            try
            {
                // Delete a step count record by ID
                apiInstance.StepCountDeleteStepCount(id);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StepCountApi.StepCountDeleteStepCount: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StepCountDeleteStepCountWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Delete a step count record by ID
    apiInstance.StepCountDeleteStepCountWithHttpInfo(id);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StepCountApi.StepCountDeleteStepCountWithHttpInfo: " + e.Message);
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

<a id="stepcountgetstepcount"></a>
# **StepCountGetStepCount**
> StepCount StepCountGetStepCount (string id)

Get a specific step count record by ID

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
    public class StepCountGetStepCountExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StepCountApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | Record ID

            try
            {
                // Get a specific step count record by ID
                StepCount result = apiInstance.StepCountGetStepCount(id);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StepCountApi.StepCountGetStepCount: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StepCountGetStepCountWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get a specific step count record by ID
    ApiResponse<StepCount> response = apiInstance.StepCountGetStepCountWithHttpInfo(id);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StepCountApi.StepCountGetStepCountWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** | Record ID |  |

### Return type

[**StepCount**](StepCount.md)

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

<a id="stepcountgetstepcounts"></a>
# **StepCountGetStepCounts**
> List&lt;StepCount&gt; StepCountGetStepCounts (int? count = null, int? skip = null, DateTimeOffset? from = null, DateTimeOffset? to = null)

Get step count records with optional pagination and date filtering

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
    public class StepCountGetStepCountsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StepCountApi(httpClient, config, httpClientHandler);
            var count = 10;  // int? | Maximum number of records to return (default: 10, ignored when from/to are specified) (optional)  (default to 10)
            var skip = 0;  // int? | Number of records to skip for pagination (default: 0, ignored when from/to are specified) (optional)  (default to 0)
            var from = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? | Start of date range (inclusive). When specified with 'to', returns all records in range. (optional) 
            var to = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? | End of date range (exclusive). When specified with 'from', returns all records in range. (optional) 

            try
            {
                // Get step count records with optional pagination and date filtering
                List<StepCount> result = apiInstance.StepCountGetStepCounts(count, skip, from, to);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StepCountApi.StepCountGetStepCounts: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StepCountGetStepCountsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get step count records with optional pagination and date filtering
    ApiResponse<List<StepCount>> response = apiInstance.StepCountGetStepCountsWithHttpInfo(count, skip, from, to);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StepCountApi.StepCountGetStepCountsWithHttpInfo: " + e.Message);
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

[**List&lt;StepCount&gt;**](StepCount.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | List of step count records |  -  |
| **500** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="stepcountupdatestepcount"></a>
# **StepCountUpdateStepCount**
> StepCount StepCountUpdateStepCount (string id, UpsertStepCountRequest upsertStepCountRequest)

Update an existing step count record

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
    public class StepCountUpdateStepCountExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StepCountApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 
            var upsertStepCountRequest = new UpsertStepCountRequest(); // UpsertStepCountRequest | 

            try
            {
                // Update an existing step count record
                StepCount result = apiInstance.StepCountUpdateStepCount(id, upsertStepCountRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StepCountApi.StepCountUpdateStepCount: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StepCountUpdateStepCountWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Update an existing step count record
    ApiResponse<StepCount> response = apiInstance.StepCountUpdateStepCountWithHttpInfo(id, upsertStepCountRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StepCountApi.StepCountUpdateStepCountWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |
| **upsertStepCountRequest** | [**UpsertStepCountRequest**](UpsertStepCountRequest.md) |  |  |

### Return type

[**StepCount**](StepCount.md)

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

