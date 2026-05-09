# NightscoutFoundation.Nocturne.Api.WebhookSettingsApi

All URIs are relative to *http://localhost*

| Method | HTTP request | Description |
|--------|--------------|-------------|
| [**WebhookSettingsGetWebhookSettings**](WebhookSettingsApi.md#webhooksettingsgetwebhooksettings) | **GET** /api/v4/ui-settings/notifications/webhooks | Gets the webhook notification settings for the current tenant. |
| [**WebhookSettingsSaveWebhookSettings**](WebhookSettingsApi.md#webhooksettingssavewebhooksettings) | **PUT** /api/v4/ui-settings/notifications/webhooks | Saves webhook notification settings. |
| [**WebhookSettingsTestWebhookSettings**](WebhookSettingsApi.md#webhooksettingstestwebhooksettings) | **POST** /api/v4/ui-settings/notifications/webhooks/test | Tests webhook settings by sending test payloads to configured URLs. |

<a id="webhooksettingsgetwebhooksettings"></a>
# **WebhookSettingsGetWebhookSettings**
> WebhookNotificationSettings WebhookSettingsGetWebhookSettings ()

Gets the webhook notification settings for the current tenant.

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
    public class WebhookSettingsGetWebhookSettingsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new WebhookSettingsApi(httpClient, config, httpClientHandler);

            try
            {
                // Gets the webhook notification settings for the current tenant.
                WebhookNotificationSettings result = apiInstance.WebhookSettingsGetWebhookSettings();
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling WebhookSettingsApi.WebhookSettingsGetWebhookSettings: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the WebhookSettingsGetWebhookSettingsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Gets the webhook notification settings for the current tenant.
    ApiResponse<WebhookNotificationSettings> response = apiInstance.WebhookSettingsGetWebhookSettingsWithHttpInfo();
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling WebhookSettingsApi.WebhookSettingsGetWebhookSettingsWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters
This endpoint does not need any parameter.
### Return type

[**WebhookNotificationSettings**](WebhookNotificationSettings.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |
| **500** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="webhooksettingssavewebhooksettings"></a>
# **WebhookSettingsSaveWebhookSettings**
> WebhookNotificationSettings WebhookSettingsSaveWebhookSettings (WebhookNotificationSettings webhookNotificationSettings)

Saves webhook notification settings.

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
    public class WebhookSettingsSaveWebhookSettingsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new WebhookSettingsApi(httpClient, config, httpClientHandler);
            var webhookNotificationSettings = new WebhookNotificationSettings(); // WebhookNotificationSettings | 

            try
            {
                // Saves webhook notification settings.
                WebhookNotificationSettings result = apiInstance.WebhookSettingsSaveWebhookSettings(webhookNotificationSettings);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling WebhookSettingsApi.WebhookSettingsSaveWebhookSettings: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the WebhookSettingsSaveWebhookSettingsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Saves webhook notification settings.
    ApiResponse<WebhookNotificationSettings> response = apiInstance.WebhookSettingsSaveWebhookSettingsWithHttpInfo(webhookNotificationSettings);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling WebhookSettingsApi.WebhookSettingsSaveWebhookSettingsWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **webhookNotificationSettings** | [**WebhookNotificationSettings**](WebhookNotificationSettings.md) |  |  |

### Return type

[**WebhookNotificationSettings**](WebhookNotificationSettings.md)

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

<a id="webhooksettingstestwebhooksettings"></a>
# **WebhookSettingsTestWebhookSettings**
> WebhookTestResult WebhookSettingsTestWebhookSettings (WebhookTestRequest webhookTestRequest)

Tests webhook settings by sending test payloads to configured URLs.

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
    public class WebhookSettingsTestWebhookSettingsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new WebhookSettingsApi(httpClient, config, httpClientHandler);
            var webhookTestRequest = new WebhookTestRequest(); // WebhookTestRequest | 

            try
            {
                // Tests webhook settings by sending test payloads to configured URLs.
                WebhookTestResult result = apiInstance.WebhookSettingsTestWebhookSettings(webhookTestRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling WebhookSettingsApi.WebhookSettingsTestWebhookSettings: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the WebhookSettingsTestWebhookSettingsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Tests webhook settings by sending test payloads to configured URLs.
    ApiResponse<WebhookTestResult> response = apiInstance.WebhookSettingsTestWebhookSettingsWithHttpInfo(webhookTestRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling WebhookSettingsApi.WebhookSettingsTestWebhookSettingsWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **webhookTestRequest** | [**WebhookTestRequest**](WebhookTestRequest.md) |  |  |

### Return type

[**WebhookTestResult**](WebhookTestResult.md)

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

