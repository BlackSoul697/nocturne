# NightscoutFoundation.Nocturne.Api.DeviceEventApi

All URIs are relative to *http://localhost*

| Method | HTTP request | Description |
|--------|--------------|-------------|
| [**DeviceEventCreate**](DeviceEventApi.md#deviceeventcreate) | **POST** /api/v4/observations/device-events | Creates a new record and returns it with a &#x60;Location&#x60; header pointing to the created resource. |
| [**DeviceEventDelete**](DeviceEventApi.md#deviceeventdelete) | **DELETE** /api/v4/observations/device-events/{id} | Deletes a record by ID. |
| [**DeviceEventDeleteBySyncIdentifier**](DeviceEventApi.md#deviceeventdeletebysyncidentifier) | **DELETE** /api/v4/observations/device-events/by-sync-id | Delete a device event by its external sync identifier (dataSource + syncIdentifier pair). |
| [**DeviceEventGetAll**](DeviceEventApi.md#deviceeventgetall) | **GET** /api/v4/observations/device-events | Lists records with pagination, optional date range, device, and source filtering. |
| [**DeviceEventGetById**](DeviceEventApi.md#deviceeventgetbyid) | **GET** /api/v4/observations/device-events/{id} | Retrieves a single record by its unique identifier. |
| [**DeviceEventUpdate**](DeviceEventApi.md#deviceeventupdate) | **PUT** /api/v4/observations/device-events/{id} | Updates an existing record by ID and returns the updated record. |

<a id="deviceeventcreate"></a>
# **DeviceEventCreate**
> DeviceEvent DeviceEventCreate (UpsertDeviceEventRequest upsertDeviceEventRequest)

Creates a new record and returns it with a `Location` header pointing to the created resource.

`Timestamp` must be set on the mapped model; requests that resolve to a default timestamp are rejected with `400 Bad Request`.              On success, responds with `201 Created` and a `Location` header containing the URL of the newly created record.

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
    public class DeviceEventCreateExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new DeviceEventApi(httpClient, config, httpClientHandler);
            var upsertDeviceEventRequest = new UpsertDeviceEventRequest(); // UpsertDeviceEventRequest | The data used to create the record.

            try
            {
                // Creates a new record and returns it with a `Location` header pointing to the created resource.
                DeviceEvent result = apiInstance.DeviceEventCreate(upsertDeviceEventRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling DeviceEventApi.DeviceEventCreate: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the DeviceEventCreateWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Creates a new record and returns it with a `Location` header pointing to the created resource.
    ApiResponse<DeviceEvent> response = apiInstance.DeviceEventCreateWithHttpInfo(upsertDeviceEventRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling DeviceEventApi.DeviceEventCreateWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **upsertDeviceEventRequest** | [**UpsertDeviceEventRequest**](UpsertDeviceEventRequest.md) | The data used to create the record. |  |

### Return type

[**DeviceEvent**](DeviceEvent.md)

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

<a id="deviceeventdelete"></a>
# **DeviceEventDelete**
> void DeviceEventDelete (string id)

Deletes a record by ID.

Returns `204 No Content` on success, or `404 Not Found` if no record with the given id exists.

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
    public class DeviceEventDeleteExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new DeviceEventApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | The unique identifier of the record to delete.

            try
            {
                // Deletes a record by ID.
                apiInstance.DeviceEventDelete(id);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling DeviceEventApi.DeviceEventDelete: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the DeviceEventDeleteWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Deletes a record by ID.
    apiInstance.DeviceEventDeleteWithHttpInfo(id);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling DeviceEventApi.DeviceEventDeleteWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** | The unique identifier of the record to delete. |  |

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

<a id="deviceeventdeletebysyncidentifier"></a>
# **DeviceEventDeleteBySyncIdentifier**
> void DeviceEventDeleteBySyncIdentifier (string? dataSource = null, string? syncIdentifier = null)

Delete a device event by its external sync identifier (dataSource + syncIdentifier pair).

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
    public class DeviceEventDeleteBySyncIdentifierExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new DeviceEventApi(httpClient, config, httpClientHandler);
            var dataSource = "dataSource_example";  // string? |  (optional) 
            var syncIdentifier = "syncIdentifier_example";  // string? |  (optional) 

            try
            {
                // Delete a device event by its external sync identifier (dataSource + syncIdentifier pair).
                apiInstance.DeviceEventDeleteBySyncIdentifier(dataSource, syncIdentifier);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling DeviceEventApi.DeviceEventDeleteBySyncIdentifier: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the DeviceEventDeleteBySyncIdentifierWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Delete a device event by its external sync identifier (dataSource + syncIdentifier pair).
    apiInstance.DeviceEventDeleteBySyncIdentifierWithHttpInfo(dataSource, syncIdentifier);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling DeviceEventApi.DeviceEventDeleteBySyncIdentifierWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **dataSource** | **string?** |  | [optional]  |
| **syncIdentifier** | **string?** |  | [optional]  |

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
| **400** |  |  -  |
| **404** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="deviceeventgetall"></a>
# **DeviceEventGetAll**
> PaginatedResponseOfDeviceEvent DeviceEventGetAll (DateTimeOffset? from = null, DateTimeOffset? to = null, int? limit = null, int? offset = null, string? sort = null, string? device = null, string? source = null)

Lists records with pagination, optional date range, device, and source filtering.

The `sort` parameter accepts exactly two values: - `timestamp_asc` — oldest records first - `timestamp_desc` — newest records first (default)              Use `limit` and `offset` together for paginated access to large result sets.

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
    public class DeviceEventGetAllExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new DeviceEventApi(httpClient, config, httpClientHandler);
            var from = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? | Inclusive start of the date range filter. (optional) 
            var to = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? | Inclusive end of the date range filter. (optional) 
            var limit = 100;  // int? | Maximum number of records to return. Defaults to `100`. (optional)  (default to 100)
            var offset = 0;  // int? | Number of records to skip for pagination. Defaults to `0`. (optional)  (default to 0)
            var sort = "\"timestamp_desc\"";  // string? | Sort order for results by timestamp. Defaults to `timestamp_desc`. (optional)  (default to "timestamp_desc")
            var device = "device_example";  // string? | Optional filter to restrict results to a specific device. (optional) 
            var source = "source_example";  // string? | Optional filter to restrict results to a specific data source. (optional) 

            try
            {
                // Lists records with pagination, optional date range, device, and source filtering.
                PaginatedResponseOfDeviceEvent result = apiInstance.DeviceEventGetAll(from, to, limit, offset, sort, device, source);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling DeviceEventApi.DeviceEventGetAll: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the DeviceEventGetAllWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Lists records with pagination, optional date range, device, and source filtering.
    ApiResponse<PaginatedResponseOfDeviceEvent> response = apiInstance.DeviceEventGetAllWithHttpInfo(from, to, limit, offset, sort, device, source);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling DeviceEventApi.DeviceEventGetAllWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **from** | **DateTimeOffset?** | Inclusive start of the date range filter. | [optional]  |
| **to** | **DateTimeOffset?** | Inclusive end of the date range filter. | [optional]  |
| **limit** | **int?** | Maximum number of records to return. Defaults to &#x60;100&#x60;. | [optional] [default to 100] |
| **offset** | **int?** | Number of records to skip for pagination. Defaults to &#x60;0&#x60;. | [optional] [default to 0] |
| **sort** | **string?** | Sort order for results by timestamp. Defaults to &#x60;timestamp_desc&#x60;. | [optional] [default to &quot;timestamp_desc&quot;] |
| **device** | **string?** | Optional filter to restrict results to a specific device. | [optional]  |
| **source** | **string?** | Optional filter to restrict results to a specific data source. | [optional]  |

### Return type

[**PaginatedResponseOfDeviceEvent**](PaginatedResponseOfDeviceEvent.md)

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

<a id="deviceeventgetbyid"></a>
# **DeviceEventGetById**
> DeviceEvent DeviceEventGetById (string id)

Retrieves a single record by its unique identifier.

Returns `404 Not Found` if no record with the given id exists.

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
    public class DeviceEventGetByIdExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new DeviceEventApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | The unique identifier of the record.

            try
            {
                // Retrieves a single record by its unique identifier.
                DeviceEvent result = apiInstance.DeviceEventGetById(id);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling DeviceEventApi.DeviceEventGetById: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the DeviceEventGetByIdWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Retrieves a single record by its unique identifier.
    ApiResponse<DeviceEvent> response = apiInstance.DeviceEventGetByIdWithHttpInfo(id);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling DeviceEventApi.DeviceEventGetByIdWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** | The unique identifier of the record. |  |

### Return type

[**DeviceEvent**](DeviceEvent.md)

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

<a id="deviceeventupdate"></a>
# **DeviceEventUpdate**
> DeviceEvent DeviceEventUpdate (string id, UpsertDeviceEventRequest upsertDeviceEventRequest)

Updates an existing record by ID and returns the updated record.

Returns `404 Not Found` if no record with the given id exists.              `Timestamp` must be set on the mapped model; requests that resolve to a default timestamp are rejected with `400 Bad Request`.

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
    public class DeviceEventUpdateExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new DeviceEventApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | The unique identifier of the record to update.
            var upsertDeviceEventRequest = new UpsertDeviceEventRequest(); // UpsertDeviceEventRequest | The data to apply to the existing record.

            try
            {
                // Updates an existing record by ID and returns the updated record.
                DeviceEvent result = apiInstance.DeviceEventUpdate(id, upsertDeviceEventRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling DeviceEventApi.DeviceEventUpdate: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the DeviceEventUpdateWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Updates an existing record by ID and returns the updated record.
    ApiResponse<DeviceEvent> response = apiInstance.DeviceEventUpdateWithHttpInfo(id, upsertDeviceEventRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling DeviceEventApi.DeviceEventUpdateWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** | The unique identifier of the record to update. |  |
| **upsertDeviceEventRequest** | [**UpsertDeviceEventRequest**](UpsertDeviceEventRequest.md) | The data to apply to the existing record. |  |

### Return type

[**DeviceEvent**](DeviceEvent.md)

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

