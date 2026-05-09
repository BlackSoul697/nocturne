# NightscoutFoundation.Nocturne.Api.AuditApi

All URIs are relative to *http://localhost*

| Method | HTTP request | Description |
|--------|--------------|-------------|
| [**AuditGetAuditConfig**](AuditApi.md#auditgetauditconfig) | **GET** /api/v4/audit/config | Get the audit configuration for the current tenant. |
| [**AuditGetMutationAuditLog**](AuditApi.md#auditgetmutationauditlog) | **GET** /api/v4/audit/mutations | Query mutation audit log entries for the current tenant. |
| [**AuditGetReadAccessAuditLog**](AuditApi.md#auditgetreadaccessauditlog) | **GET** /api/v4/audit/reads | Query read access audit log entries for the current tenant. |
| [**AuditUpdateAuditConfig**](AuditApi.md#auditupdateauditconfig) | **PUT** /api/v4/audit/config | Create or update the audit configuration for the current tenant. |

<a id="auditgetauditconfig"></a>
# **AuditGetAuditConfig**
> AuditConfigDto AuditGetAuditConfig ()

Get the audit configuration for the current tenant.

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
    public class AuditGetAuditConfigExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new AuditApi(httpClient, config, httpClientHandler);

            try
            {
                // Get the audit configuration for the current tenant.
                AuditConfigDto result = apiInstance.AuditGetAuditConfig();
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling AuditApi.AuditGetAuditConfig: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the AuditGetAuditConfigWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get the audit configuration for the current tenant.
    ApiResponse<AuditConfigDto> response = apiInstance.AuditGetAuditConfigWithHttpInfo();
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling AuditApi.AuditGetAuditConfigWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters
This endpoint does not need any parameter.
### Return type

[**AuditConfigDto**](AuditConfigDto.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |
| **403** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="auditgetmutationauditlog"></a>
# **AuditGetMutationAuditLog**
> PaginatedResponseOfMutationAuditDto AuditGetMutationAuditLog (DateTimeOffset? from = null, DateTimeOffset? to = null, int? limit = null, int? offset = null, string? sort = null, string? subjectId = null, string? entityType = null, string? action = null, string? entityId = null)

Query mutation audit log entries for the current tenant.

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
    public class AuditGetMutationAuditLogExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new AuditApi(httpClient, config, httpClientHandler);
            var from = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? |  (optional) 
            var to = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? |  (optional) 
            var limit = 100;  // int? |  (optional)  (default to 100)
            var offset = 0;  // int? |  (optional)  (default to 0)
            var sort = "\"created_at_desc\"";  // string? |  (optional)  (default to "created_at_desc")
            var subjectId = "subjectId_example";  // string? |  (optional) 
            var entityType = "entityType_example";  // string? |  (optional) 
            var action = "action_example";  // string? |  (optional) 
            var entityId = "entityId_example";  // string? |  (optional) 

            try
            {
                // Query mutation audit log entries for the current tenant.
                PaginatedResponseOfMutationAuditDto result = apiInstance.AuditGetMutationAuditLog(from, to, limit, offset, sort, subjectId, entityType, action, entityId);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling AuditApi.AuditGetMutationAuditLog: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the AuditGetMutationAuditLogWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Query mutation audit log entries for the current tenant.
    ApiResponse<PaginatedResponseOfMutationAuditDto> response = apiInstance.AuditGetMutationAuditLogWithHttpInfo(from, to, limit, offset, sort, subjectId, entityType, action, entityId);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling AuditApi.AuditGetMutationAuditLogWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **from** | **DateTimeOffset?** |  | [optional]  |
| **to** | **DateTimeOffset?** |  | [optional]  |
| **limit** | **int?** |  | [optional] [default to 100] |
| **offset** | **int?** |  | [optional] [default to 0] |
| **sort** | **string?** |  | [optional] [default to &quot;created_at_desc&quot;] |
| **subjectId** | **string?** |  | [optional]  |
| **entityType** | **string?** |  | [optional]  |
| **action** | **string?** |  | [optional]  |
| **entityId** | **string?** |  | [optional]  |

### Return type

[**PaginatedResponseOfMutationAuditDto**](PaginatedResponseOfMutationAuditDto.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |
| **403** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="auditgetreadaccessauditlog"></a>
# **AuditGetReadAccessAuditLog**
> PaginatedResponseOfReadAccessAuditDto AuditGetReadAccessAuditLog (DateTimeOffset? from = null, DateTimeOffset? to = null, int? limit = null, int? offset = null, string? sort = null, string? subjectId = null, string? entityType = null, string? endpoint = null, int? statusCode = null)

Query read access audit log entries for the current tenant.

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
    public class AuditGetReadAccessAuditLogExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new AuditApi(httpClient, config, httpClientHandler);
            var from = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? |  (optional) 
            var to = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? |  (optional) 
            var limit = 100;  // int? |  (optional)  (default to 100)
            var offset = 0;  // int? |  (optional)  (default to 0)
            var sort = "\"created_at_desc\"";  // string? |  (optional)  (default to "created_at_desc")
            var subjectId = "subjectId_example";  // string? |  (optional) 
            var entityType = "entityType_example";  // string? |  (optional) 
            var endpoint = "endpoint_example";  // string? |  (optional) 
            var statusCode = 56;  // int? |  (optional) 

            try
            {
                // Query read access audit log entries for the current tenant.
                PaginatedResponseOfReadAccessAuditDto result = apiInstance.AuditGetReadAccessAuditLog(from, to, limit, offset, sort, subjectId, entityType, endpoint, statusCode);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling AuditApi.AuditGetReadAccessAuditLog: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the AuditGetReadAccessAuditLogWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Query read access audit log entries for the current tenant.
    ApiResponse<PaginatedResponseOfReadAccessAuditDto> response = apiInstance.AuditGetReadAccessAuditLogWithHttpInfo(from, to, limit, offset, sort, subjectId, entityType, endpoint, statusCode);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling AuditApi.AuditGetReadAccessAuditLogWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **from** | **DateTimeOffset?** |  | [optional]  |
| **to** | **DateTimeOffset?** |  | [optional]  |
| **limit** | **int?** |  | [optional] [default to 100] |
| **offset** | **int?** |  | [optional] [default to 0] |
| **sort** | **string?** |  | [optional] [default to &quot;created_at_desc&quot;] |
| **subjectId** | **string?** |  | [optional]  |
| **entityType** | **string?** |  | [optional]  |
| **endpoint** | **string?** |  | [optional]  |
| **statusCode** | **int?** |  | [optional]  |

### Return type

[**PaginatedResponseOfReadAccessAuditDto**](PaginatedResponseOfReadAccessAuditDto.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |
| **403** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="auditupdateauditconfig"></a>
# **AuditUpdateAuditConfig**
> AuditConfigDto AuditUpdateAuditConfig (AuditConfigDto auditConfigDto)

Create or update the audit configuration for the current tenant.

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
    public class AuditUpdateAuditConfigExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new AuditApi(httpClient, config, httpClientHandler);
            var auditConfigDto = new AuditConfigDto(); // AuditConfigDto | 

            try
            {
                // Create or update the audit configuration for the current tenant.
                AuditConfigDto result = apiInstance.AuditUpdateAuditConfig(auditConfigDto);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling AuditApi.AuditUpdateAuditConfig: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the AuditUpdateAuditConfigWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Create or update the audit configuration for the current tenant.
    ApiResponse<AuditConfigDto> response = apiInstance.AuditUpdateAuditConfigWithHttpInfo(auditConfigDto);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling AuditApi.AuditUpdateAuditConfigWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **auditConfigDto** | [**AuditConfigDto**](AuditConfigDto.md) |  |  |

### Return type

[**AuditConfigDto**](AuditConfigDto.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |
| **403** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

