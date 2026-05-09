# NightscoutFoundation.Nocturne.Api.NutritionApi

All URIs are relative to *http://localhost*

| Method | HTTP request | Description |
|--------|--------------|-------------|
| [**NutritionAddCarbIntakeFood**](NutritionApi.md#nutritionaddcarbintakefood) | **POST** /api/v4/nutrition/carbs/{id}/foods | Add a food breakdown entry to a carb intake record. |
| [**NutritionCreateCarbIntake**](NutritionApi.md#nutritioncreatecarbintake) | **POST** /api/v4/nutrition/carbs | Create a new carb intake |
| [**NutritionCreateMeal**](NutritionApi.md#nutritioncreatemeal) | **POST** /api/v4/nutrition/meals | Atomically create a correlated Bolus + CarbIntake for a meal event. Both records share a single CorrelationId and are persisted within a single transaction. When an existing row matches on (DataSource, SyncIdentifier), the idempotent upsert applies and the response returns 200 instead of 201. |
| [**NutritionDeleteCarbIntake**](NutritionApi.md#nutritiondeletecarbintake) | **DELETE** /api/v4/nutrition/carbs/{id} | Delete a carb intake |
| [**NutritionDeleteCarbIntakeBySyncIdentifier**](NutritionApi.md#nutritiondeletecarbintakebysyncidentifier) | **DELETE** /api/v4/nutrition/carbs/by-sync-id | Delete a carb intake by its external sync identifier (dataSource + syncIdentifier pair). |
| [**NutritionDeleteCarbIntakeFood**](NutritionApi.md#nutritiondeletecarbintakefood) | **DELETE** /api/v4/nutrition/carbs/{id}/foods/{foodEntryId} | Remove a food breakdown entry. |
| [**NutritionGetCarbIntakeById**](NutritionApi.md#nutritiongetcarbintakebyid) | **GET** /api/v4/nutrition/carbs/{id} | Get a carb intake by ID |
| [**NutritionGetCarbIntakeFoods**](NutritionApi.md#nutritiongetcarbintakefoods) | **GET** /api/v4/nutrition/carbs/{id}/foods | Get food breakdown for a carb intake record. |
| [**NutritionGetCarbIntakes**](NutritionApi.md#nutritiongetcarbintakes) | **GET** /api/v4/nutrition/carbs | Get carb intakes with optional filtering |
| [**NutritionGetMeals**](NutritionApi.md#nutritiongetmeals) | **GET** /api/v4/nutrition/meals | Get meal events grouped by CorrelationId. Each event carries its own carb intakes, correlated boluses, food attribution rows, and aggregated totals. Carb intakes with a null CorrelationId become single-member events on their own (they are NOT collapsed together). |
| [**NutritionUpdateCarbIntake**](NutritionApi.md#nutritionupdatecarbintake) | **PUT** /api/v4/nutrition/carbs/{id} | Update an existing carb intake |
| [**NutritionUpdateCarbIntakeFood**](NutritionApi.md#nutritionupdatecarbintakefood) | **PUT** /api/v4/nutrition/carbs/{id}/foods/{foodEntryId} | Update a food breakdown entry. |

<a id="nutritionaddcarbintakefood"></a>
# **NutritionAddCarbIntakeFood**
> TreatmentFoodBreakdown NutritionAddCarbIntakeFood (string id, CarbIntakeFoodRequest carbIntakeFoodRequest)

Add a food breakdown entry to a carb intake record.

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
    public class NutritionAddCarbIntakeFoodExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new NutritionApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 
            var carbIntakeFoodRequest = new CarbIntakeFoodRequest(); // CarbIntakeFoodRequest | 

            try
            {
                // Add a food breakdown entry to a carb intake record.
                TreatmentFoodBreakdown result = apiInstance.NutritionAddCarbIntakeFood(id, carbIntakeFoodRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling NutritionApi.NutritionAddCarbIntakeFood: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the NutritionAddCarbIntakeFoodWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Add a food breakdown entry to a carb intake record.
    ApiResponse<TreatmentFoodBreakdown> response = apiInstance.NutritionAddCarbIntakeFoodWithHttpInfo(id, carbIntakeFoodRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling NutritionApi.NutritionAddCarbIntakeFoodWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |
| **carbIntakeFoodRequest** | [**CarbIntakeFoodRequest**](CarbIntakeFoodRequest.md) |  |  |

### Return type

[**TreatmentFoodBreakdown**](TreatmentFoodBreakdown.md)

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

<a id="nutritioncreatecarbintake"></a>
# **NutritionCreateCarbIntake**
> CarbIntake NutritionCreateCarbIntake (CreateCarbIntakeRequest createCarbIntakeRequest)

Create a new carb intake

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
    public class NutritionCreateCarbIntakeExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new NutritionApi(httpClient, config, httpClientHandler);
            var createCarbIntakeRequest = new CreateCarbIntakeRequest(); // CreateCarbIntakeRequest | 

            try
            {
                // Create a new carb intake
                CarbIntake result = apiInstance.NutritionCreateCarbIntake(createCarbIntakeRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling NutritionApi.NutritionCreateCarbIntake: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the NutritionCreateCarbIntakeWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Create a new carb intake
    ApiResponse<CarbIntake> response = apiInstance.NutritionCreateCarbIntakeWithHttpInfo(createCarbIntakeRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling NutritionApi.NutritionCreateCarbIntakeWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **createCarbIntakeRequest** | [**CreateCarbIntakeRequest**](CreateCarbIntakeRequest.md) |  |  |

### Return type

[**CarbIntake**](CarbIntake.md)

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

<a id="nutritioncreatemeal"></a>
# **NutritionCreateMeal**
> CreateMealResponse NutritionCreateMeal (CreateMealRequest createMealRequest)

Atomically create a correlated Bolus + CarbIntake for a meal event. Both records share a single CorrelationId and are persisted within a single transaction. When an existing row matches on (DataSource, SyncIdentifier), the idempotent upsert applies and the response returns 200 instead of 201.

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
    public class NutritionCreateMealExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new NutritionApi(httpClient, config, httpClientHandler);
            var createMealRequest = new CreateMealRequest(); // CreateMealRequest | 

            try
            {
                // Atomically create a correlated Bolus + CarbIntake for a meal event. Both records share a single CorrelationId and are persisted within a single transaction. When an existing row matches on (DataSource, SyncIdentifier), the idempotent upsert applies and the response returns 200 instead of 201.
                CreateMealResponse result = apiInstance.NutritionCreateMeal(createMealRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling NutritionApi.NutritionCreateMeal: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the NutritionCreateMealWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Atomically create a correlated Bolus + CarbIntake for a meal event. Both records share a single CorrelationId and are persisted within a single transaction. When an existing row matches on (DataSource, SyncIdentifier), the idempotent upsert applies and the response returns 200 instead of 201.
    ApiResponse<CreateMealResponse> response = apiInstance.NutritionCreateMealWithHttpInfo(createMealRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling NutritionApi.NutritionCreateMealWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **createMealRequest** | [**CreateMealRequest**](CreateMealRequest.md) |  |  |

### Return type

[**CreateMealResponse**](CreateMealResponse.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** |  |  -  |
| **201** |  |  -  |
| **400** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="nutritiondeletecarbintake"></a>
# **NutritionDeleteCarbIntake**
> void NutritionDeleteCarbIntake (string id)

Delete a carb intake

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
    public class NutritionDeleteCarbIntakeExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new NutritionApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 

            try
            {
                // Delete a carb intake
                apiInstance.NutritionDeleteCarbIntake(id);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling NutritionApi.NutritionDeleteCarbIntake: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the NutritionDeleteCarbIntakeWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Delete a carb intake
    apiInstance.NutritionDeleteCarbIntakeWithHttpInfo(id);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling NutritionApi.NutritionDeleteCarbIntakeWithHttpInfo: " + e.Message);
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
| **204** |  |  -  |
| **404** |  |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="nutritiondeletecarbintakebysyncidentifier"></a>
# **NutritionDeleteCarbIntakeBySyncIdentifier**
> void NutritionDeleteCarbIntakeBySyncIdentifier (string? dataSource = null, string? syncIdentifier = null)

Delete a carb intake by its external sync identifier (dataSource + syncIdentifier pair).

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
    public class NutritionDeleteCarbIntakeBySyncIdentifierExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new NutritionApi(httpClient, config, httpClientHandler);
            var dataSource = "dataSource_example";  // string? |  (optional) 
            var syncIdentifier = "syncIdentifier_example";  // string? |  (optional) 

            try
            {
                // Delete a carb intake by its external sync identifier (dataSource + syncIdentifier pair).
                apiInstance.NutritionDeleteCarbIntakeBySyncIdentifier(dataSource, syncIdentifier);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling NutritionApi.NutritionDeleteCarbIntakeBySyncIdentifier: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the NutritionDeleteCarbIntakeBySyncIdentifierWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Delete a carb intake by its external sync identifier (dataSource + syncIdentifier pair).
    apiInstance.NutritionDeleteCarbIntakeBySyncIdentifierWithHttpInfo(dataSource, syncIdentifier);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling NutritionApi.NutritionDeleteCarbIntakeBySyncIdentifierWithHttpInfo: " + e.Message);
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

<a id="nutritiondeletecarbintakefood"></a>
# **NutritionDeleteCarbIntakeFood**
> TreatmentFoodBreakdown NutritionDeleteCarbIntakeFood (string id, string foodEntryId)

Remove a food breakdown entry.

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
    public class NutritionDeleteCarbIntakeFoodExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new NutritionApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 
            var foodEntryId = "foodEntryId_example";  // string | 

            try
            {
                // Remove a food breakdown entry.
                TreatmentFoodBreakdown result = apiInstance.NutritionDeleteCarbIntakeFood(id, foodEntryId);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling NutritionApi.NutritionDeleteCarbIntakeFood: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the NutritionDeleteCarbIntakeFoodWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Remove a food breakdown entry.
    ApiResponse<TreatmentFoodBreakdown> response = apiInstance.NutritionDeleteCarbIntakeFoodWithHttpInfo(id, foodEntryId);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling NutritionApi.NutritionDeleteCarbIntakeFoodWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |
| **foodEntryId** | **string** |  |  |

### Return type

[**TreatmentFoodBreakdown**](TreatmentFoodBreakdown.md)

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

<a id="nutritiongetcarbintakebyid"></a>
# **NutritionGetCarbIntakeById**
> CarbIntake NutritionGetCarbIntakeById (string id)

Get a carb intake by ID

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
    public class NutritionGetCarbIntakeByIdExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new NutritionApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 

            try
            {
                // Get a carb intake by ID
                CarbIntake result = apiInstance.NutritionGetCarbIntakeById(id);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling NutritionApi.NutritionGetCarbIntakeById: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the NutritionGetCarbIntakeByIdWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get a carb intake by ID
    ApiResponse<CarbIntake> response = apiInstance.NutritionGetCarbIntakeByIdWithHttpInfo(id);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling NutritionApi.NutritionGetCarbIntakeByIdWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |

### Return type

[**CarbIntake**](CarbIntake.md)

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

<a id="nutritiongetcarbintakefoods"></a>
# **NutritionGetCarbIntakeFoods**
> TreatmentFoodBreakdown NutritionGetCarbIntakeFoods (string id)

Get food breakdown for a carb intake record.

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
    public class NutritionGetCarbIntakeFoodsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new NutritionApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 

            try
            {
                // Get food breakdown for a carb intake record.
                TreatmentFoodBreakdown result = apiInstance.NutritionGetCarbIntakeFoods(id);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling NutritionApi.NutritionGetCarbIntakeFoods: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the NutritionGetCarbIntakeFoodsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get food breakdown for a carb intake record.
    ApiResponse<TreatmentFoodBreakdown> response = apiInstance.NutritionGetCarbIntakeFoodsWithHttpInfo(id);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling NutritionApi.NutritionGetCarbIntakeFoodsWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |

### Return type

[**TreatmentFoodBreakdown**](TreatmentFoodBreakdown.md)

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

<a id="nutritiongetcarbintakes"></a>
# **NutritionGetCarbIntakes**
> PaginatedResponseOfCarbIntake NutritionGetCarbIntakes (DateTimeOffset? from = null, DateTimeOffset? to = null, int? limit = null, int? offset = null, string? sort = null, string? device = null, string? source = null)

Get carb intakes with optional filtering

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
    public class NutritionGetCarbIntakesExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new NutritionApi(httpClient, config, httpClientHandler);
            var from = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? |  (optional) 
            var to = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? |  (optional) 
            var limit = 100;  // int? |  (optional)  (default to 100)
            var offset = 0;  // int? |  (optional)  (default to 0)
            var sort = "\"timestamp_desc\"";  // string? |  (optional)  (default to "timestamp_desc")
            var device = "device_example";  // string? |  (optional) 
            var source = "source_example";  // string? |  (optional) 

            try
            {
                // Get carb intakes with optional filtering
                PaginatedResponseOfCarbIntake result = apiInstance.NutritionGetCarbIntakes(from, to, limit, offset, sort, device, source);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling NutritionApi.NutritionGetCarbIntakes: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the NutritionGetCarbIntakesWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get carb intakes with optional filtering
    ApiResponse<PaginatedResponseOfCarbIntake> response = apiInstance.NutritionGetCarbIntakesWithHttpInfo(from, to, limit, offset, sort, device, source);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling NutritionApi.NutritionGetCarbIntakesWithHttpInfo: " + e.Message);
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
| **sort** | **string?** |  | [optional] [default to &quot;timestamp_desc&quot;] |
| **device** | **string?** |  | [optional]  |
| **source** | **string?** |  | [optional]  |

### Return type

[**PaginatedResponseOfCarbIntake**](PaginatedResponseOfCarbIntake.md)

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

<a id="nutritiongetmeals"></a>
# **NutritionGetMeals**
> List&lt;MealEvent&gt; NutritionGetMeals (DateTimeOffset? from = null, DateTimeOffset? to = null, bool? attributed = null)

Get meal events grouped by CorrelationId. Each event carries its own carb intakes, correlated boluses, food attribution rows, and aggregated totals. Carb intakes with a null CorrelationId become single-member events on their own (they are NOT collapsed together).

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
    public class NutritionGetMealsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new NutritionApi(httpClient, config, httpClientHandler);
            var from = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? |  (optional) 
            var to = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? |  (optional) 
            var attributed = true;  // bool? |  (optional) 

            try
            {
                // Get meal events grouped by CorrelationId. Each event carries its own carb intakes, correlated boluses, food attribution rows, and aggregated totals. Carb intakes with a null CorrelationId become single-member events on their own (they are NOT collapsed together).
                List<MealEvent> result = apiInstance.NutritionGetMeals(from, to, attributed);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling NutritionApi.NutritionGetMeals: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the NutritionGetMealsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get meal events grouped by CorrelationId. Each event carries its own carb intakes, correlated boluses, food attribution rows, and aggregated totals. Carb intakes with a null CorrelationId become single-member events on their own (they are NOT collapsed together).
    ApiResponse<List<MealEvent>> response = apiInstance.NutritionGetMealsWithHttpInfo(from, to, attributed);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling NutritionApi.NutritionGetMealsWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **from** | **DateTimeOffset?** |  | [optional]  |
| **to** | **DateTimeOffset?** |  | [optional]  |
| **attributed** | **bool?** |  | [optional]  |

### Return type

[**List&lt;MealEvent&gt;**](MealEvent.md)

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

<a id="nutritionupdatecarbintake"></a>
# **NutritionUpdateCarbIntake**
> CarbIntake NutritionUpdateCarbIntake (string id, UpdateCarbIntakeRequest updateCarbIntakeRequest)

Update an existing carb intake

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
    public class NutritionUpdateCarbIntakeExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new NutritionApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 
            var updateCarbIntakeRequest = new UpdateCarbIntakeRequest(); // UpdateCarbIntakeRequest | 

            try
            {
                // Update an existing carb intake
                CarbIntake result = apiInstance.NutritionUpdateCarbIntake(id, updateCarbIntakeRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling NutritionApi.NutritionUpdateCarbIntake: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the NutritionUpdateCarbIntakeWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Update an existing carb intake
    ApiResponse<CarbIntake> response = apiInstance.NutritionUpdateCarbIntakeWithHttpInfo(id, updateCarbIntakeRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling NutritionApi.NutritionUpdateCarbIntakeWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |
| **updateCarbIntakeRequest** | [**UpdateCarbIntakeRequest**](UpdateCarbIntakeRequest.md) |  |  |

### Return type

[**CarbIntake**](CarbIntake.md)

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

<a id="nutritionupdatecarbintakefood"></a>
# **NutritionUpdateCarbIntakeFood**
> TreatmentFoodBreakdown NutritionUpdateCarbIntakeFood (string id, string foodEntryId, CarbIntakeFoodRequest carbIntakeFoodRequest)

Update a food breakdown entry.

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
    public class NutritionUpdateCarbIntakeFoodExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new NutritionApi(httpClient, config, httpClientHandler);
            var id = "id_example";  // string | 
            var foodEntryId = "foodEntryId_example";  // string | 
            var carbIntakeFoodRequest = new CarbIntakeFoodRequest(); // CarbIntakeFoodRequest | 

            try
            {
                // Update a food breakdown entry.
                TreatmentFoodBreakdown result = apiInstance.NutritionUpdateCarbIntakeFood(id, foodEntryId, carbIntakeFoodRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling NutritionApi.NutritionUpdateCarbIntakeFood: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the NutritionUpdateCarbIntakeFoodWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Update a food breakdown entry.
    ApiResponse<TreatmentFoodBreakdown> response = apiInstance.NutritionUpdateCarbIntakeFoodWithHttpInfo(id, foodEntryId, carbIntakeFoodRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling NutritionApi.NutritionUpdateCarbIntakeFoodWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **id** | **string** |  |  |
| **foodEntryId** | **string** |  |  |
| **carbIntakeFoodRequest** | [**CarbIntakeFoodRequest**](CarbIntakeFoodRequest.md) |  |  |

### Return type

[**TreatmentFoodBreakdown**](TreatmentFoodBreakdown.md)

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

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

