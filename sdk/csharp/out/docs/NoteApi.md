# NightscoutFoundation.Nocturne.Api.NoteApi

All URIs are relative to *http://localhost*

| Method | HTTP request | Description |
|--------|--------------|-------------|
| [**NoteCreate**](NoteApi.md#notecreate) | **POST** /api/v4/observations/notes | Creates a new record and returns it with a &#x60;Location&#x60; header pointing to the created resource. |
| [**NoteDelete**](NoteApi.md#notedelete) | **DELETE** /api/v4/observations/notes/{id} | Deletes a record by ID. |
| [**NoteDeleteBySyncIdentifier**](NoteApi.md#notedeletebysyncidentifier) | **DELETE** /api/v4/observations/notes/by-sync-id | Delete a note by its external sync identifier (dataSource + syncIdentifier pair). |
| [**NoteGetAll**](NoteApi.md#notegetall) | **GET** /api/v4/observations/notes | Lists records with pagination, optional date range, device, and source filtering. |
| [**NoteGetById**](NoteApi.md#notegetbyid) | **GET** /api/v4/observations/notes/{id} | Retrieves a single record by its unique identifier. |
| [**NoteUpdate**](NoteApi.md#noteupdate) | **PUT** /api/v4/observations/notes/{id} | Updates an existing record by ID and returns the updated record. |

<a id="notecreate"></a>
# **NoteCreate**
> Note NoteCreate (UpsertNoteRequest upsertNoteRequest)

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
    public class NoteCreateExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new NoteApi(httpClient, config, httpClientHandler);
            var upsertNoteRequest = new UpsertNoteRequest(); // UpsertNoteRequest | The data used to create the record.

            try
            {
                // Creates a new record and returns it with a `Location` header pointing to the created resource.
                Note result = apiInstance.NoteCreate(upsertNoteRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling NoteApi.NoteCreate: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the NoteCreateWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Creates a new record and returns it with a `Location` header pointing to the created resource.
    ApiResponse<Note> response = apiInstance.NoteCreateWithHttpInfo(upsertNoteRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling NoteApi.NoteCreateWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **upsertNoteRequest** | [**UpsertNoteRequest**](UpsertNoteRequest.md) | The data used to create the record. |  |

### Return type

[**Note**](Note.md)

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

<a id="notedelete"></a>
# **NoteDelete**
> void NoteDelete (string id)

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
    public class NoteDeleteExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new NoteApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | The unique identifier of the record to delete.

            try
            {
                // Deletes a record by ID.
                apiInstance.NoteDelete(id);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling NoteApi.NoteDelete: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the NoteDeleteWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Deletes a record by ID.
    apiInstance.NoteDeleteWithHttpInfo(id);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling NoteApi.NoteDeleteWithHttpInfo: " + e.Message);
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

<a id="notedeletebysyncidentifier"></a>
# **NoteDeleteBySyncIdentifier**
> void NoteDeleteBySyncIdentifier (string? dataSource = null, string? syncIdentifier = null)

Delete a note by its external sync identifier (dataSource + syncIdentifier pair).

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
    public class NoteDeleteBySyncIdentifierExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new NoteApi(httpClient, config, httpClientHandler);
            var dataSource = "dataSource_example";  // string? |  (optional) 
            var syncIdentifier = "syncIdentifier_example";  // string? |  (optional) 

            try
            {
                // Delete a note by its external sync identifier (dataSource + syncIdentifier pair).
                apiInstance.NoteDeleteBySyncIdentifier(dataSource, syncIdentifier);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling NoteApi.NoteDeleteBySyncIdentifier: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the NoteDeleteBySyncIdentifierWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Delete a note by its external sync identifier (dataSource + syncIdentifier pair).
    apiInstance.NoteDeleteBySyncIdentifierWithHttpInfo(dataSource, syncIdentifier);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling NoteApi.NoteDeleteBySyncIdentifierWithHttpInfo: " + e.Message);
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

<a id="notegetall"></a>
# **NoteGetAll**
> PaginatedResponseOfNote NoteGetAll (DateTimeOffset? from = null, DateTimeOffset? to = null, int? limit = null, int? offset = null, string? sort = null, string? device = null, string? source = null)

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
    public class NoteGetAllExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new NoteApi(httpClient, config, httpClientHandler);
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
                PaginatedResponseOfNote result = apiInstance.NoteGetAll(from, to, limit, offset, sort, device, source);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling NoteApi.NoteGetAll: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the NoteGetAllWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Lists records with pagination, optional date range, device, and source filtering.
    ApiResponse<PaginatedResponseOfNote> response = apiInstance.NoteGetAllWithHttpInfo(from, to, limit, offset, sort, device, source);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling NoteApi.NoteGetAllWithHttpInfo: " + e.Message);
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

[**PaginatedResponseOfNote**](PaginatedResponseOfNote.md)

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

<a id="notegetbyid"></a>
# **NoteGetById**
> Note NoteGetById (string id)

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
    public class NoteGetByIdExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new NoteApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | The unique identifier of the record.

            try
            {
                // Retrieves a single record by its unique identifier.
                Note result = apiInstance.NoteGetById(id);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling NoteApi.NoteGetById: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the NoteGetByIdWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Retrieves a single record by its unique identifier.
    ApiResponse<Note> response = apiInstance.NoteGetByIdWithHttpInfo(id);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling NoteApi.NoteGetByIdWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** | The unique identifier of the record. |  |

### Return type

[**Note**](Note.md)

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

<a id="noteupdate"></a>
# **NoteUpdate**
> Note NoteUpdate (string id, UpsertNoteRequest upsertNoteRequest)

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
    public class NoteUpdateExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new NoteApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | The unique identifier of the record to update.
            var upsertNoteRequest = new UpsertNoteRequest(); // UpsertNoteRequest | The data to apply to the existing record.

            try
            {
                // Updates an existing record by ID and returns the updated record.
                Note result = apiInstance.NoteUpdate(id, upsertNoteRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling NoteApi.NoteUpdate: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the NoteUpdateWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Updates an existing record by ID and returns the updated record.
    ApiResponse<Note> response = apiInstance.NoteUpdateWithHttpInfo(id, upsertNoteRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling NoteApi.NoteUpdateWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** | The unique identifier of the record to update. |  |
| **upsertNoteRequest** | [**UpsertNoteRequest**](UpsertNoteRequest.md) | The data to apply to the existing record. |  |

### Return type

[**Note**](Note.md)

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

