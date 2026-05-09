# NightscoutFoundation.Nocturne.Api.SetupApi

All URIs are relative to *http://localhost*

| Method | HTTP request | Description |
|--------|--------------|-------------|
| [**SetupCreateTenant**](SetupApi.md#setupcreatetenant) | **POST** /api/v4/setup/tenant | Create the first tenant on a fresh install. Only succeeds when zero tenants exist. |
| [**SetupOidcCallback**](SetupApi.md#setupoidccallback) | **GET** /api/v4/setup/oidc/callback | OIDC callback for setup owner creation. Called by the OIDC provider after authentication. Links the identity, issues session cookies, and redirects to /setup. |
| [**SetupOwnerComplete**](SetupApi.md#setupownercomplete) | **POST** /api/v4/setup/owner/complete | Complete passkey registration for the first owner account. Verifies attestation, generates recovery codes, issues a full JWT session. |
| [**SetupOwnerOidc**](SetupApi.md#setupowneroidc) | **POST** /api/v4/setup/owner/oidc | Initiate OIDC-based owner creation. Creates the subject and owner role, then redirects to the OIDC provider to link an identity. |
| [**SetupOwnerOptions**](SetupApi.md#setupowneroptions) | **POST** /api/v4/setup/owner/options | Generate passkey registration options for the first owner account. Guard: exactly one tenant must exist with zero non-system members. |
| [**SetupValidateUsername**](SetupApi.md#setupvalidateusername) | **GET** /api/v4/setup/validate-username | Check whether a username is available for the owner account. |

<a id="setupcreatetenant"></a>
# **SetupCreateTenant**
> SetupTenantResponse SetupCreateTenant (SetupTenantRequest setupTenantRequest)

Create the first tenant on a fresh install. Only succeeds when zero tenants exist.

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
    public class SetupCreateTenantExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new SetupApi(httpClient, config, httpClientHandler);
            var setupTenantRequest = new SetupTenantRequest(); // SetupTenantRequest | 

            try
            {
                // Create the first tenant on a fresh install. Only succeeds when zero tenants exist.
                SetupTenantResponse result = apiInstance.SetupCreateTenant(setupTenantRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling SetupApi.SetupCreateTenant: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the SetupCreateTenantWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Create the first tenant on a fresh install. Only succeeds when zero tenants exist.
    ApiResponse<SetupTenantResponse> response = apiInstance.SetupCreateTenantWithHttpInfo(setupTenantRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling SetupApi.SetupCreateTenantWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **setupTenantRequest** | [**SetupTenantRequest**](SetupTenantRequest.md) |  |  |

### Return type

[**SetupTenantResponse**](SetupTenantResponse.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |
| **409** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="setupoidccallback"></a>
# **SetupOidcCallback**
> void SetupOidcCallback (string? code = null, string? state = null, string? error = null, string? errorDescription = null)

OIDC callback for setup owner creation. Called by the OIDC provider after authentication. Links the identity, issues session cookies, and redirects to /setup.

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
    public class SetupOidcCallbackExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new SetupApi(httpClient, config, httpClientHandler);
            var code = "code_example";  // string? |  (optional) 
            var state = "state_example";  // string? |  (optional) 
            var error = "error_example";  // string? |  (optional) 
            var errorDescription = "errorDescription_example";  // string? |  (optional) 

            try
            {
                // OIDC callback for setup owner creation. Called by the OIDC provider after authentication. Links the identity, issues session cookies, and redirects to /setup.
                apiInstance.SetupOidcCallback(code, state, error, errorDescription);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling SetupApi.SetupOidcCallback: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the SetupOidcCallbackWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // OIDC callback for setup owner creation. Called by the OIDC provider after authentication. Links the identity, issues session cookies, and redirects to /setup.
    apiInstance.SetupOidcCallbackWithHttpInfo(code, state, error, errorDescription);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling SetupApi.SetupOidcCallbackWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **code** | **string?** |  | [optional]  |
| **state** | **string?** |  | [optional]  |
| **error** | **string?** |  | [optional]  |
| **errorDescription** | **string?** |  | [optional]  |

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
| **302** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="setupownercomplete"></a>
# **SetupOwnerComplete**
> SetupOwnerCompleteResponse SetupOwnerComplete (SetupOwnerCompleteRequest setupOwnerCompleteRequest)

Complete passkey registration for the first owner account. Verifies attestation, generates recovery codes, issues a full JWT session.

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
    public class SetupOwnerCompleteExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new SetupApi(httpClient, config, httpClientHandler);
            var setupOwnerCompleteRequest = new SetupOwnerCompleteRequest(); // SetupOwnerCompleteRequest | 

            try
            {
                // Complete passkey registration for the first owner account. Verifies attestation, generates recovery codes, issues a full JWT session.
                SetupOwnerCompleteResponse result = apiInstance.SetupOwnerComplete(setupOwnerCompleteRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling SetupApi.SetupOwnerComplete: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the SetupOwnerCompleteWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Complete passkey registration for the first owner account. Verifies attestation, generates recovery codes, issues a full JWT session.
    ApiResponse<SetupOwnerCompleteResponse> response = apiInstance.SetupOwnerCompleteWithHttpInfo(setupOwnerCompleteRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling SetupApi.SetupOwnerCompleteWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **setupOwnerCompleteRequest** | [**SetupOwnerCompleteRequest**](SetupOwnerCompleteRequest.md) |  |  |

### Return type

[**SetupOwnerCompleteResponse**](SetupOwnerCompleteResponse.md)

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
| **409** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="setupowneroidc"></a>
# **SetupOwnerOidc**
> SetupOwnerOidcResponse SetupOwnerOidc (SetupOwnerOidcRequest setupOwnerOidcRequest)

Initiate OIDC-based owner creation. Creates the subject and owner role, then redirects to the OIDC provider to link an identity.

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
    public class SetupOwnerOidcExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new SetupApi(httpClient, config, httpClientHandler);
            var setupOwnerOidcRequest = new SetupOwnerOidcRequest(); // SetupOwnerOidcRequest | 

            try
            {
                // Initiate OIDC-based owner creation. Creates the subject and owner role, then redirects to the OIDC provider to link an identity.
                SetupOwnerOidcResponse result = apiInstance.SetupOwnerOidc(setupOwnerOidcRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling SetupApi.SetupOwnerOidc: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the SetupOwnerOidcWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Initiate OIDC-based owner creation. Creates the subject and owner role, then redirects to the OIDC provider to link an identity.
    ApiResponse<SetupOwnerOidcResponse> response = apiInstance.SetupOwnerOidcWithHttpInfo(setupOwnerOidcRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling SetupApi.SetupOwnerOidcWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **setupOwnerOidcRequest** | [**SetupOwnerOidcRequest**](SetupOwnerOidcRequest.md) |  |  |

### Return type

[**SetupOwnerOidcResponse**](SetupOwnerOidcResponse.md)

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
| **409** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="setupowneroptions"></a>
# **SetupOwnerOptions**
> SetupOwnerOptionsResponse SetupOwnerOptions (SetupOwnerOptionsRequest setupOwnerOptionsRequest)

Generate passkey registration options for the first owner account. Guard: exactly one tenant must exist with zero non-system members.

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
    public class SetupOwnerOptionsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new SetupApi(httpClient, config, httpClientHandler);
            var setupOwnerOptionsRequest = new SetupOwnerOptionsRequest(); // SetupOwnerOptionsRequest | 

            try
            {
                // Generate passkey registration options for the first owner account. Guard: exactly one tenant must exist with zero non-system members.
                SetupOwnerOptionsResponse result = apiInstance.SetupOwnerOptions(setupOwnerOptionsRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling SetupApi.SetupOwnerOptions: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the SetupOwnerOptionsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Generate passkey registration options for the first owner account. Guard: exactly one tenant must exist with zero non-system members.
    ApiResponse<SetupOwnerOptionsResponse> response = apiInstance.SetupOwnerOptionsWithHttpInfo(setupOwnerOptionsRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling SetupApi.SetupOwnerOptionsWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **setupOwnerOptionsRequest** | [**SetupOwnerOptionsRequest**](SetupOwnerOptionsRequest.md) |  |  |

### Return type

[**SetupOwnerOptionsResponse**](SetupOwnerOptionsResponse.md)

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
| **409** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="setupvalidateusername"></a>
# **SetupValidateUsername**
> SlugValidationResult SetupValidateUsername (string? username = null)

Check whether a username is available for the owner account.

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
    public class SetupValidateUsernameExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new SetupApi(httpClient, config, httpClientHandler);
            var username = "username_example";  // string? |  (optional) 

            try
            {
                // Check whether a username is available for the owner account.
                SlugValidationResult result = apiInstance.SetupValidateUsername(username);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling SetupApi.SetupValidateUsername: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the SetupValidateUsernameWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Check whether a username is available for the owner account.
    ApiResponse<SlugValidationResult> response = apiInstance.SetupValidateUsernameWithHttpInfo(username);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling SetupApi.SetupValidateUsernameWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **username** | **string?** |  | [optional]  |

### Return type

[**SlugValidationResult**](SlugValidationResult.md)

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

