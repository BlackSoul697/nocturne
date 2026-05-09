# NightscoutFoundation.Nocturne.Api.DevAdminApi

All URIs are relative to *http://localhost*

| Method | HTTP request | Description |
|--------|--------------|-------------|
| [**DevAdminCreateTenant**](DevAdminApi.md#devadmincreatetenant) | **POST** /api/v4/dev-only/admin/tenants | Create a new tenant without authentication (dev-only). Used by the Aspire dashboard \&quot;Create Tenant\&quot; command. |
| [**DevAdminDeleteTenant**](DevAdminApi.md#devadmindeletetenant) | **DELETE** /api/v4/dev-only/admin/tenants/{id} | Delete a tenant and all associated data without authentication (dev-only). |
| [**DevAdminExportSnapshot**](DevAdminApi.md#devadminexportsnapshot) | **GET** /api/v4/dev-only/admin/snapshot | Export a full snapshot of all tenants and their identity/config data. Secrets are decrypted to plaintext for portability. |
| [**DevAdminImportScopedSnapshot**](DevAdminApi.md#devadminimportscopedsnapshot) | **POST** /api/v4/dev-only/admin/tenants/{id}/import-snapshot | Import snapshot data for a single tenant, matched by slug in the provided snapshot. Upserts referenced subjects and passkeys without affecting other tenants. |
| [**DevAdminImportSnapshot**](DevAdminApi.md#devadminimportsnapshot) | **POST** /api/v4/dev-only/admin/snapshot | Import a snapshot, replacing all identity/config data. Wraps the entire operation in a transaction. |
| [**DevAdminListTenants**](DevAdminApi.md#devadminlisttenants) | **GET** /api/v4/dev-only/admin/tenants | List all tenants with record counts and connector health (dev-only). Used by the Aspire dashboard \&quot;List Tenants\&quot; command. |
| [**DevAdminSyncAll**](DevAdminApi.md#devadminsyncall) | **POST** /api/v4/dev-only/admin/sync-all | Trigger a sync for every configured connector across all tenants. |

<a id="devadmincreatetenant"></a>
# **DevAdminCreateTenant**
> TenantCreatedDto DevAdminCreateTenant (DevCreateTenantRequest devCreateTenantRequest)

Create a new tenant without authentication (dev-only). Used by the Aspire dashboard \"Create Tenant\" command.

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
    public class DevAdminCreateTenantExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new DevAdminApi(httpClient, config, httpClientHandler);
            var devCreateTenantRequest = new DevCreateTenantRequest(); // DevCreateTenantRequest | 

            try
            {
                // Create a new tenant without authentication (dev-only). Used by the Aspire dashboard \"Create Tenant\" command.
                TenantCreatedDto result = apiInstance.DevAdminCreateTenant(devCreateTenantRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling DevAdminApi.DevAdminCreateTenant: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the DevAdminCreateTenantWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Create a new tenant without authentication (dev-only). Used by the Aspire dashboard \"Create Tenant\" command.
    ApiResponse<TenantCreatedDto> response = apiInstance.DevAdminCreateTenantWithHttpInfo(devCreateTenantRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling DevAdminApi.DevAdminCreateTenantWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **devCreateTenantRequest** | [**DevCreateTenantRequest**](DevCreateTenantRequest.md) |  |  |

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
| **200** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="devadmindeletetenant"></a>
# **DevAdminDeleteTenant**
> FileParameter DevAdminDeleteTenant (string id)

Delete a tenant and all associated data without authentication (dev-only).

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
    public class DevAdminDeleteTenantExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new DevAdminApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 

            try
            {
                // Delete a tenant and all associated data without authentication (dev-only).
                FileParameter result = apiInstance.DevAdminDeleteTenant(id);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling DevAdminApi.DevAdminDeleteTenant: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the DevAdminDeleteTenantWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Delete a tenant and all associated data without authentication (dev-only).
    ApiResponse<FileParameter> response = apiInstance.DevAdminDeleteTenantWithHttpInfo(id);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling DevAdminApi.DevAdminDeleteTenantWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |

### Return type

[**FileParameter**](FileParameter.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/octet-stream


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="devadminexportsnapshot"></a>
# **DevAdminExportSnapshot**
> DevSnapshotDto DevAdminExportSnapshot ()

Export a full snapshot of all tenants and their identity/config data. Secrets are decrypted to plaintext for portability.

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
    public class DevAdminExportSnapshotExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new DevAdminApi(httpClient, config, httpClientHandler);

            try
            {
                // Export a full snapshot of all tenants and their identity/config data. Secrets are decrypted to plaintext for portability.
                DevSnapshotDto result = apiInstance.DevAdminExportSnapshot();
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling DevAdminApi.DevAdminExportSnapshot: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the DevAdminExportSnapshotWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Export a full snapshot of all tenants and their identity/config data. Secrets are decrypted to plaintext for portability.
    ApiResponse<DevSnapshotDto> response = apiInstance.DevAdminExportSnapshotWithHttpInfo();
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling DevAdminApi.DevAdminExportSnapshotWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters
This endpoint does not need any parameter.
### Return type

[**DevSnapshotDto**](DevSnapshotDto.md)

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

<a id="devadminimportscopedsnapshot"></a>
# **DevAdminImportScopedSnapshot**
> FileParameter DevAdminImportScopedSnapshot (string id, TenantSnapshotDto tenantSnapshotDto)

Import snapshot data for a single tenant, matched by slug in the provided snapshot. Upserts referenced subjects and passkeys without affecting other tenants.

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
    public class DevAdminImportScopedSnapshotExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new DevAdminApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 
            var tenantSnapshotDto = new TenantSnapshotDto(); // TenantSnapshotDto | 

            try
            {
                // Import snapshot data for a single tenant, matched by slug in the provided snapshot. Upserts referenced subjects and passkeys without affecting other tenants.
                FileParameter result = apiInstance.DevAdminImportScopedSnapshot(id, tenantSnapshotDto);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling DevAdminApi.DevAdminImportScopedSnapshot: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the DevAdminImportScopedSnapshotWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Import snapshot data for a single tenant, matched by slug in the provided snapshot. Upserts referenced subjects and passkeys without affecting other tenants.
    ApiResponse<FileParameter> response = apiInstance.DevAdminImportScopedSnapshotWithHttpInfo(id, tenantSnapshotDto);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling DevAdminApi.DevAdminImportScopedSnapshotWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |
| **tenantSnapshotDto** | [**TenantSnapshotDto**](TenantSnapshotDto.md) |  |  |

### Return type

[**FileParameter**](FileParameter.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/octet-stream


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="devadminimportsnapshot"></a>
# **DevAdminImportSnapshot**
> FileParameter DevAdminImportSnapshot (DevSnapshotDto devSnapshotDto)

Import a snapshot, replacing all identity/config data. Wraps the entire operation in a transaction.

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
    public class DevAdminImportSnapshotExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new DevAdminApi(httpClient, config, httpClientHandler);
            var devSnapshotDto = new DevSnapshotDto(); // DevSnapshotDto | 

            try
            {
                // Import a snapshot, replacing all identity/config data. Wraps the entire operation in a transaction.
                FileParameter result = apiInstance.DevAdminImportSnapshot(devSnapshotDto);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling DevAdminApi.DevAdminImportSnapshot: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the DevAdminImportSnapshotWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Import a snapshot, replacing all identity/config data. Wraps the entire operation in a transaction.
    ApiResponse<FileParameter> response = apiInstance.DevAdminImportSnapshotWithHttpInfo(devSnapshotDto);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling DevAdminApi.DevAdminImportSnapshotWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **devSnapshotDto** | [**DevSnapshotDto**](DevSnapshotDto.md) |  |  |

### Return type

[**FileParameter**](FileParameter.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/octet-stream


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="devadminlisttenants"></a>
# **DevAdminListTenants**
> List&lt;DevTenantSummaryDto&gt; DevAdminListTenants ()

List all tenants with record counts and connector health (dev-only). Used by the Aspire dashboard \"List Tenants\" command.

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
    public class DevAdminListTenantsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new DevAdminApi(httpClient, config, httpClientHandler);

            try
            {
                // List all tenants with record counts and connector health (dev-only). Used by the Aspire dashboard \"List Tenants\" command.
                List<DevTenantSummaryDto> result = apiInstance.DevAdminListTenants();
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling DevAdminApi.DevAdminListTenants: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the DevAdminListTenantsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // List all tenants with record counts and connector health (dev-only). Used by the Aspire dashboard \"List Tenants\" command.
    ApiResponse<List<DevTenantSummaryDto>> response = apiInstance.DevAdminListTenantsWithHttpInfo();
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling DevAdminApi.DevAdminListTenantsWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters
This endpoint does not need any parameter.
### Return type

[**List&lt;DevTenantSummaryDto&gt;**](DevTenantSummaryDto.md)

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

<a id="devadminsyncall"></a>
# **DevAdminSyncAll**
> FileParameter DevAdminSyncAll ()

Trigger a sync for every configured connector across all tenants.

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
    public class DevAdminSyncAllExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new DevAdminApi(httpClient, config, httpClientHandler);

            try
            {
                // Trigger a sync for every configured connector across all tenants.
                FileParameter result = apiInstance.DevAdminSyncAll();
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling DevAdminApi.DevAdminSyncAll: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the DevAdminSyncAllWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Trigger a sync for every configured connector across all tenants.
    ApiResponse<FileParameter> response = apiInstance.DevAdminSyncAllWithHttpInfo();
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling DevAdminApi.DevAdminSyncAllWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters
This endpoint does not need any parameter.
### Return type

[**FileParameter**](FileParameter.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/octet-stream


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

