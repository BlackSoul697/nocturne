# NightscoutFoundation.Nocturne.Api.StatisticsApi

All URIs are relative to *http://localhost*

| Method | HTTP request | Description |
|--------|--------------|-------------|
| [**StatisticsAnalyzeGlucoseData**](StatisticsApi.md#statisticsanalyzeglucosedata) | **POST** /api/v4/Statistics/comprehensive-analytics | Master glucose analytics function that calculates comprehensive metrics |
| [**StatisticsAnalyzeGlucoseDataExtended**](StatisticsApi.md#statisticsanalyzeglucosedataextended) | **POST** /api/v4/Statistics/extended-analytics | Extended glucose analytics including GMI, GRI, and clinical target assessment |
| [**StatisticsAssessAgainstTargets**](StatisticsApi.md#statisticsassessagainsttargets) | **POST** /api/v4/Statistics/clinical-assessment | Assess glucose data against clinical targets for a specific population |
| [**StatisticsAssessDataSufficiency**](StatisticsApi.md#statisticsassessdatasufficiency) | **POST** /api/v4/Statistics/data-sufficiency | Assess data sufficiency for a valid clinical report |
| [**StatisticsCalculateAveragedStats**](StatisticsApi.md#statisticscalculateaveragedstats) | **POST** /api/v4/Statistics/averaged-stats | Calculate averaged statistics for each hour of the day (0-23) |
| [**StatisticsCalculateBasicStats**](StatisticsApi.md#statisticscalculatebasicstats) | **POST** /api/v4/Statistics/basic-stats | Calculate basic glucose statistics from provided glucose values |
| [**StatisticsCalculateEstimatedA1C**](StatisticsApi.md#statisticscalculateestimateda1c) | **GET** /api/v4/Statistics/estimated-a1c/{averageGlucose} | Calculate estimated A1C from average glucose |
| [**StatisticsCalculateGMI**](StatisticsApi.md#statisticscalculategmi) | **GET** /api/v4/Statistics/gmi/{meanGlucose} | Calculate Glucose Management Indicator (GMI) |
| [**StatisticsCalculateGRI**](StatisticsApi.md#statisticscalculategri) | **POST** /api/v4/Statistics/gri | Calculate Glycemic Risk Index (GRI) from time in range metrics |
| [**StatisticsCalculateGlucoseDistribution**](StatisticsApi.md#statisticscalculateglucosedistribution) | **POST** /api/v4/Statistics/glucose-distribution | Calculate glucose distribution across configurable bins |
| [**StatisticsCalculateGlycemicVariability**](StatisticsApi.md#statisticscalculateglycemicvariability) | **POST** /api/v4/Statistics/glycemic-variability | Calculate comprehensive glycemic variability metrics |
| [**StatisticsCalculateOverallAverages**](StatisticsApi.md#statisticscalculateoverallaverages) | **POST** /api/v4/Statistics/overall-averages | Calculate overall averages across multiple days |
| [**StatisticsCalculateSiteChangeImpact**](StatisticsApi.md#statisticscalculatesitechangeimpact) | **POST** /api/v4/Statistics/site-change-impact | Analyze glucose patterns around site changes to identify impact of site age on control |
| [**StatisticsCalculateTimeInRange**](StatisticsApi.md#statisticscalculatetimeinrange) | **POST** /api/v4/Statistics/time-in-range | Calculate time in range metrics |
| [**StatisticsCalculateTreatmentSummary**](StatisticsApi.md#statisticscalculatetreatmentsummary) | **POST** /api/v4/Statistics/treatment-summary | Calculate treatment summary for a collection of boluses and carb intakes |
| [**StatisticsCleanTreatmentData**](StatisticsApi.md#statisticscleantreatmentdata) | **POST** /api/v4/Statistics/clean/treatments | Clean and filter treatment data |
| [**StatisticsFormatCarbDisplay**](StatisticsApi.md#statisticsformatcarbdisplay) | **GET** /api/v4/Statistics/format/carb/{value} | Format carb value for display |
| [**StatisticsFormatInsulinDisplay**](StatisticsApi.md#statisticsformatinsulindisplay) | **GET** /api/v4/Statistics/format/insulin/{value} | Format insulin value for display |
| [**StatisticsGetAidSystemMetrics**](StatisticsApi.md#statisticsgetaidsystemmetrics) | **GET** /api/v4/Statistics/aid-system-metrics | Calculates AID (Automated Insulin Delivery) system metrics for a date range. Uses patient device records to segment the period by algorithm and compute time-weighted metrics via IAidMetricsService. |
| [**StatisticsGetBasalAnalysis**](StatisticsApi.md#statisticsgetbasalanalysis) | **GET** /api/v4/Statistics/basal-analysis | Calculate comprehensive basal analysis statistics for a date range |
| [**StatisticsGetClinicalTargets**](StatisticsApi.md#statisticsgetclinicaltargets) | **GET** /api/v4/Statistics/clinical-targets/{population} | Get clinical targets for a specific diabetes population |
| [**StatisticsGetDailyBasalBolusRatios**](StatisticsApi.md#statisticsgetdailybasalbolusratios) | **GET** /api/v4/Statistics/daily-basal-bolus-ratios | Calculate daily basal/bolus ratio statistics for a date range |
| [**StatisticsGetInsulinDeliveryStatistics**](StatisticsApi.md#statisticsgetinsulindeliverystatistics) | **GET** /api/v4/Statistics/insulin-delivery-stats | Calculate comprehensive insulin delivery statistics for a date range |
| [**StatisticsGetMultiPeriodStatistics**](StatisticsApi.md#statisticsgetmultiperiodstatistics) | **GET** /api/v4/Statistics/periods | Gets comprehensive statistics for multiple time periods (1, 3, 7, 30, and 90 days). Fetches sensor glucose, bolus, carb, and temp-basal data from the database for each period, computes GlucoseAnalytics, TreatmentSummary, and InsulinDeliveryStatistics, and caches the result for 5 minutes. |
| [**StatisticsMgdlToMMOL**](StatisticsApi.md#statisticsmgdltommol) | **GET** /api/v4/Statistics/convert/mgdl-to-mmol/{mgdl} | Convert mg/dL to mmol/L |
| [**StatisticsMmolToMGDL**](StatisticsApi.md#statisticsmmoltomgdl) | **GET** /api/v4/Statistics/convert/mmol-to-mgdl/{mmol} | Convert mmol/L to mg/dL |
| [**StatisticsValidateTreatmentData**](StatisticsApi.md#statisticsvalidatetreatmentdata) | **POST** /api/v4/Statistics/validate/treatment | Validate treatment data for completeness and consistency |

<a id="statisticsanalyzeglucosedata"></a>
# **StatisticsAnalyzeGlucoseData**
> GlucoseAnalytics StatisticsAnalyzeGlucoseData (GlucoseAnalyticsRequest glucoseAnalyticsRequest)

Master glucose analytics function that calculates comprehensive metrics

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
    public class StatisticsAnalyzeGlucoseDataExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var glucoseAnalyticsRequest = new GlucoseAnalyticsRequest(); // GlucoseAnalyticsRequest | Request containing sensor glucose readings, boluses, carb intakes, and configuration

            try
            {
                // Master glucose analytics function that calculates comprehensive metrics
                GlucoseAnalytics result = apiInstance.StatisticsAnalyzeGlucoseData(glucoseAnalyticsRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsAnalyzeGlucoseData: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsAnalyzeGlucoseDataWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Master glucose analytics function that calculates comprehensive metrics
    ApiResponse<GlucoseAnalytics> response = apiInstance.StatisticsAnalyzeGlucoseDataWithHttpInfo(glucoseAnalyticsRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsAnalyzeGlucoseDataWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **glucoseAnalyticsRequest** | [**GlucoseAnalyticsRequest**](GlucoseAnalyticsRequest.md) | Request containing sensor glucose readings, boluses, carb intakes, and configuration |  |

### Return type

[**GlucoseAnalytics**](GlucoseAnalytics.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | Comprehensive glucose analytics |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticsanalyzeglucosedataextended"></a>
# **StatisticsAnalyzeGlucoseDataExtended**
> ExtendedGlucoseAnalytics StatisticsAnalyzeGlucoseDataExtended (ExtendedGlucoseAnalyticsRequest extendedGlucoseAnalyticsRequest)

Extended glucose analytics including GMI, GRI, and clinical target assessment

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
    public class StatisticsAnalyzeGlucoseDataExtendedExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var extendedGlucoseAnalyticsRequest = new ExtendedGlucoseAnalyticsRequest(); // ExtendedGlucoseAnalyticsRequest | Request containing sensor glucose readings, boluses, carb intakes, population type, and configuration

            try
            {
                // Extended glucose analytics including GMI, GRI, and clinical target assessment
                ExtendedGlucoseAnalytics result = apiInstance.StatisticsAnalyzeGlucoseDataExtended(extendedGlucoseAnalyticsRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsAnalyzeGlucoseDataExtended: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsAnalyzeGlucoseDataExtendedWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Extended glucose analytics including GMI, GRI, and clinical target assessment
    ApiResponse<ExtendedGlucoseAnalytics> response = apiInstance.StatisticsAnalyzeGlucoseDataExtendedWithHttpInfo(extendedGlucoseAnalyticsRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsAnalyzeGlucoseDataExtendedWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **extendedGlucoseAnalyticsRequest** | [**ExtendedGlucoseAnalyticsRequest**](ExtendedGlucoseAnalyticsRequest.md) | Request containing sensor glucose readings, boluses, carb intakes, population type, and configuration |  |

### Return type

[**ExtendedGlucoseAnalytics**](ExtendedGlucoseAnalytics.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | Extended glucose analytics with modern clinical metrics |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticsassessagainsttargets"></a>
# **StatisticsAssessAgainstTargets**
> ClinicalTargetAssessment StatisticsAssessAgainstTargets (ClinicalAssessmentRequest clinicalAssessmentRequest)

Assess glucose data against clinical targets for a specific population

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
    public class StatisticsAssessAgainstTargetsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var clinicalAssessmentRequest = new ClinicalAssessmentRequest(); // ClinicalAssessmentRequest | Request containing analytics and population type

            try
            {
                // Assess glucose data against clinical targets for a specific population
                ClinicalTargetAssessment result = apiInstance.StatisticsAssessAgainstTargets(clinicalAssessmentRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsAssessAgainstTargets: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsAssessAgainstTargetsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Assess glucose data against clinical targets for a specific population
    ApiResponse<ClinicalTargetAssessment> response = apiInstance.StatisticsAssessAgainstTargetsWithHttpInfo(clinicalAssessmentRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsAssessAgainstTargetsWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **clinicalAssessmentRequest** | [**ClinicalAssessmentRequest**](ClinicalAssessmentRequest.md) | Request containing analytics and population type |  |

### Return type

[**ClinicalTargetAssessment**](ClinicalTargetAssessment.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | Clinical target assessment with actionable insights |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticsassessdatasufficiency"></a>
# **StatisticsAssessDataSufficiency**
> DataSufficiencyAssessment StatisticsAssessDataSufficiency (DataSufficiencyRequest dataSufficiencyRequest)

Assess data sufficiency for a valid clinical report

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
    public class StatisticsAssessDataSufficiencyExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var dataSufficiencyRequest = new DataSufficiencyRequest(); // DataSufficiencyRequest | Request containing entries and optional period settings

            try
            {
                // Assess data sufficiency for a valid clinical report
                DataSufficiencyAssessment result = apiInstance.StatisticsAssessDataSufficiency(dataSufficiencyRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsAssessDataSufficiency: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsAssessDataSufficiencyWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Assess data sufficiency for a valid clinical report
    ApiResponse<DataSufficiencyAssessment> response = apiInstance.StatisticsAssessDataSufficiencyWithHttpInfo(dataSufficiencyRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsAssessDataSufficiencyWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **dataSufficiencyRequest** | [**DataSufficiencyRequest**](DataSufficiencyRequest.md) | Request containing entries and optional period settings |  |

### Return type

[**DataSufficiencyAssessment**](DataSufficiencyAssessment.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | Data sufficiency assessment |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticscalculateaveragedstats"></a>
# **StatisticsCalculateAveragedStats**
> List&lt;AveragedStats&gt; StatisticsCalculateAveragedStats (List<SensorGlucose> sensorGlucose)

Calculate averaged statistics for each hour of the day (0-23)

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
    public class StatisticsCalculateAveragedStatsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var sensorGlucose = new List<SensorGlucose>(); // List<SensorGlucose> | Array of sensor glucose readings

            try
            {
                // Calculate averaged statistics for each hour of the day (0-23)
                List<AveragedStats> result = apiInstance.StatisticsCalculateAveragedStats(sensorGlucose);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsCalculateAveragedStats: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsCalculateAveragedStatsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Calculate averaged statistics for each hour of the day (0-23)
    ApiResponse<List<AveragedStats>> response = apiInstance.StatisticsCalculateAveragedStatsWithHttpInfo(sensorGlucose);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsCalculateAveragedStatsWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **sensorGlucose** | [**List&lt;SensorGlucose&gt;**](SensorGlucose.md) | Array of sensor glucose readings |  |

### Return type

[**List&lt;AveragedStats&gt;**](AveragedStats.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | Collection of averaged statistics for each hour |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticscalculatebasicstats"></a>
# **StatisticsCalculateBasicStats**
> BasicGlucoseStats StatisticsCalculateBasicStats (List<double> requestBody)

Calculate basic glucose statistics from provided glucose values

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
    public class StatisticsCalculateBasicStatsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var requestBody = new List<double>(); // List<double> | Array of glucose values in mg/dL

            try
            {
                // Calculate basic glucose statistics from provided glucose values
                BasicGlucoseStats result = apiInstance.StatisticsCalculateBasicStats(requestBody);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsCalculateBasicStats: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsCalculateBasicStatsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Calculate basic glucose statistics from provided glucose values
    ApiResponse<BasicGlucoseStats> response = apiInstance.StatisticsCalculateBasicStatsWithHttpInfo(requestBody);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsCalculateBasicStatsWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **requestBody** | [**List&lt;double&gt;**](double.md) | Array of glucose values in mg/dL |  |

### Return type

[**BasicGlucoseStats**](BasicGlucoseStats.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | Basic glucose statistics including mean, median, percentiles, etc. |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticscalculateestimateda1c"></a>
# **StatisticsCalculateEstimatedA1C**
> double StatisticsCalculateEstimatedA1C (double averageGlucose)

Calculate estimated A1C from average glucose

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
    public class StatisticsCalculateEstimatedA1CExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var averageGlucose = 1.2D;  // double | Average glucose in mg/dL

            try
            {
                // Calculate estimated A1C from average glucose
                double result = apiInstance.StatisticsCalculateEstimatedA1C(averageGlucose);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsCalculateEstimatedA1C: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsCalculateEstimatedA1CWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Calculate estimated A1C from average glucose
    ApiResponse<double> response = apiInstance.StatisticsCalculateEstimatedA1CWithHttpInfo(averageGlucose);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsCalculateEstimatedA1CWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **averageGlucose** | **double** | Average glucose in mg/dL |  |

### Return type

**double**

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | Estimated A1C percentage |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticscalculategmi"></a>
# **StatisticsCalculateGMI**
> GlucoseManagementIndicator StatisticsCalculateGMI (double meanGlucose)

Calculate Glucose Management Indicator (GMI)

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
    public class StatisticsCalculateGMIExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var meanGlucose = 1.2D;  // double | Mean glucose in mg/dL

            try
            {
                // Calculate Glucose Management Indicator (GMI)
                GlucoseManagementIndicator result = apiInstance.StatisticsCalculateGMI(meanGlucose);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsCalculateGMI: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsCalculateGMIWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Calculate Glucose Management Indicator (GMI)
    ApiResponse<GlucoseManagementIndicator> response = apiInstance.StatisticsCalculateGMIWithHttpInfo(meanGlucose);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsCalculateGMIWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **meanGlucose** | **double** | Mean glucose in mg/dL |  |

### Return type

[**GlucoseManagementIndicator**](GlucoseManagementIndicator.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | GMI with value and interpretation |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticscalculategri"></a>
# **StatisticsCalculateGRI**
> GlycemicRiskIndex StatisticsCalculateGRI (TimeInRangeMetrics timeInRangeMetrics)

Calculate Glycemic Risk Index (GRI) from time in range metrics

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
    public class StatisticsCalculateGRIExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var timeInRangeMetrics = new TimeInRangeMetrics(); // TimeInRangeMetrics | Time in range metrics

            try
            {
                // Calculate Glycemic Risk Index (GRI) from time in range metrics
                GlycemicRiskIndex result = apiInstance.StatisticsCalculateGRI(timeInRangeMetrics);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsCalculateGRI: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsCalculateGRIWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Calculate Glycemic Risk Index (GRI) from time in range metrics
    ApiResponse<GlycemicRiskIndex> response = apiInstance.StatisticsCalculateGRIWithHttpInfo(timeInRangeMetrics);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsCalculateGRIWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **timeInRangeMetrics** | [**TimeInRangeMetrics**](TimeInRangeMetrics.md) | Time in range metrics |  |

### Return type

[**GlycemicRiskIndex**](GlycemicRiskIndex.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | GRI with score, zone, and interpretation |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticscalculateglucosedistribution"></a>
# **StatisticsCalculateGlucoseDistribution**
> List&lt;DistributionDataPoint&gt; StatisticsCalculateGlucoseDistribution (GlucoseDistributionRequest glucoseDistributionRequest)

Calculate glucose distribution across configurable bins

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
    public class StatisticsCalculateGlucoseDistributionExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var glucoseDistributionRequest = new GlucoseDistributionRequest(); // GlucoseDistributionRequest | Request containing entries and optional bins

            try
            {
                // Calculate glucose distribution across configurable bins
                List<DistributionDataPoint> result = apiInstance.StatisticsCalculateGlucoseDistribution(glucoseDistributionRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsCalculateGlucoseDistribution: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsCalculateGlucoseDistributionWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Calculate glucose distribution across configurable bins
    ApiResponse<List<DistributionDataPoint>> response = apiInstance.StatisticsCalculateGlucoseDistributionWithHttpInfo(glucoseDistributionRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsCalculateGlucoseDistributionWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **glucoseDistributionRequest** | [**GlucoseDistributionRequest**](GlucoseDistributionRequest.md) | Request containing entries and optional bins |  |

### Return type

[**List&lt;DistributionDataPoint&gt;**](DistributionDataPoint.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | Collection of distribution data points |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticscalculateglycemicvariability"></a>
# **StatisticsCalculateGlycemicVariability**
> GlycemicVariability StatisticsCalculateGlycemicVariability (GlycemicVariabilityRequest glycemicVariabilityRequest)

Calculate comprehensive glycemic variability metrics

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
    public class StatisticsCalculateGlycemicVariabilityExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var glycemicVariabilityRequest = new GlycemicVariabilityRequest(); // GlycemicVariabilityRequest | Request containing glucose values and entries

            try
            {
                // Calculate comprehensive glycemic variability metrics
                GlycemicVariability result = apiInstance.StatisticsCalculateGlycemicVariability(glycemicVariabilityRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsCalculateGlycemicVariability: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsCalculateGlycemicVariabilityWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Calculate comprehensive glycemic variability metrics
    ApiResponse<GlycemicVariability> response = apiInstance.StatisticsCalculateGlycemicVariabilityWithHttpInfo(glycemicVariabilityRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsCalculateGlycemicVariabilityWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **glycemicVariabilityRequest** | [**GlycemicVariabilityRequest**](GlycemicVariabilityRequest.md) | Request containing glucose values and entries |  |

### Return type

[**GlycemicVariability**](GlycemicVariability.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | Comprehensive glycemic variability metrics |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticscalculateoverallaverages"></a>
# **StatisticsCalculateOverallAverages**
> OverallAverages StatisticsCalculateOverallAverages (List<DayData> dayData)

Calculate overall averages across multiple days

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
    public class StatisticsCalculateOverallAveragesExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var dayData = new List<DayData>(); // List<DayData> | Array of daily data points

            try
            {
                // Calculate overall averages across multiple days
                OverallAverages result = apiInstance.StatisticsCalculateOverallAverages(dayData);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsCalculateOverallAverages: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsCalculateOverallAveragesWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Calculate overall averages across multiple days
    ApiResponse<OverallAverages> response = apiInstance.StatisticsCalculateOverallAveragesWithHttpInfo(dayData);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsCalculateOverallAveragesWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **dayData** | [**List&lt;DayData&gt;**](DayData.md) | Array of daily data points |  |

### Return type

[**OverallAverages**](OverallAverages.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | Overall averages or null if no data |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticscalculatesitechangeimpact"></a>
# **StatisticsCalculateSiteChangeImpact**
> SiteChangeImpactAnalysis StatisticsCalculateSiteChangeImpact (SiteChangeImpactRequest siteChangeImpactRequest)

Analyze glucose patterns around site changes to identify impact of site age on control

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
    public class StatisticsCalculateSiteChangeImpactExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var siteChangeImpactRequest = new SiteChangeImpactRequest(); // SiteChangeImpactRequest | Request containing sensor glucose readings, device events, and analysis parameters

            try
            {
                // Analyze glucose patterns around site changes to identify impact of site age on control
                SiteChangeImpactAnalysis result = apiInstance.StatisticsCalculateSiteChangeImpact(siteChangeImpactRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsCalculateSiteChangeImpact: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsCalculateSiteChangeImpactWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Analyze glucose patterns around site changes to identify impact of site age on control
    ApiResponse<SiteChangeImpactAnalysis> response = apiInstance.StatisticsCalculateSiteChangeImpactWithHttpInfo(siteChangeImpactRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsCalculateSiteChangeImpactWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **siteChangeImpactRequest** | [**SiteChangeImpactRequest**](SiteChangeImpactRequest.md) | Request containing sensor glucose readings, device events, and analysis parameters |  |

### Return type

[**SiteChangeImpactAnalysis**](SiteChangeImpactAnalysis.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | Site change impact analysis with averaged glucose patterns |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticscalculatetimeinrange"></a>
# **StatisticsCalculateTimeInRange**
> TimeInRangeMetrics StatisticsCalculateTimeInRange (TimeInRangeRequest timeInRangeRequest)

Calculate time in range metrics

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
    public class StatisticsCalculateTimeInRangeExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var timeInRangeRequest = new TimeInRangeRequest(); // TimeInRangeRequest | Request containing entries and optional thresholds

            try
            {
                // Calculate time in range metrics
                TimeInRangeMetrics result = apiInstance.StatisticsCalculateTimeInRange(timeInRangeRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsCalculateTimeInRange: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsCalculateTimeInRangeWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Calculate time in range metrics
    ApiResponse<TimeInRangeMetrics> response = apiInstance.StatisticsCalculateTimeInRangeWithHttpInfo(timeInRangeRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsCalculateTimeInRangeWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **timeInRangeRequest** | [**TimeInRangeRequest**](TimeInRangeRequest.md) | Request containing entries and optional thresholds |  |

### Return type

[**TimeInRangeMetrics**](TimeInRangeMetrics.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | Time in range metrics including percentages, durations, and episodes |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticscalculatetreatmentsummary"></a>
# **StatisticsCalculateTreatmentSummary**
> TreatmentSummary StatisticsCalculateTreatmentSummary (TreatmentSummaryRequest treatmentSummaryRequest)

Calculate treatment summary for a collection of boluses and carb intakes

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
    public class StatisticsCalculateTreatmentSummaryExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var treatmentSummaryRequest = new TreatmentSummaryRequest(); // TreatmentSummaryRequest | Request containing boluses and carb intakes

            try
            {
                // Calculate treatment summary for a collection of boluses and carb intakes
                TreatmentSummary result = apiInstance.StatisticsCalculateTreatmentSummary(treatmentSummaryRequest);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsCalculateTreatmentSummary: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsCalculateTreatmentSummaryWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Calculate treatment summary for a collection of boluses and carb intakes
    ApiResponse<TreatmentSummary> response = apiInstance.StatisticsCalculateTreatmentSummaryWithHttpInfo(treatmentSummaryRequest);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsCalculateTreatmentSummaryWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **treatmentSummaryRequest** | [**TreatmentSummaryRequest**](TreatmentSummaryRequest.md) | Request containing boluses and carb intakes |  |

### Return type

[**TreatmentSummary**](TreatmentSummary.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | Treatment summary with totals and counts |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticscleantreatmentdata"></a>
# **StatisticsCleanTreatmentData**
> List&lt;Treatment&gt; StatisticsCleanTreatmentData (List<Treatment> treatment)

Clean and filter treatment data

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
    public class StatisticsCleanTreatmentDataExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var treatment = new List<Treatment>(); // List<Treatment> | Array of treatments to clean

            try
            {
                // Clean and filter treatment data
                List<Treatment> result = apiInstance.StatisticsCleanTreatmentData(treatment);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsCleanTreatmentData: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsCleanTreatmentDataWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Clean and filter treatment data
    ApiResponse<List<Treatment>> response = apiInstance.StatisticsCleanTreatmentDataWithHttpInfo(treatment);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsCleanTreatmentDataWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **treatment** | [**List&lt;Treatment&gt;**](Treatment.md) | Array of treatments to clean |  |

### Return type

[**List&lt;Treatment&gt;**](Treatment.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | Cleaned collection of treatments |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticsformatcarbdisplay"></a>
# **StatisticsFormatCarbDisplay**
> string StatisticsFormatCarbDisplay (double value)

Format carb value for display

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
    public class StatisticsFormatCarbDisplayExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var value = 1.2D;  // double | Carb value

            try
            {
                // Format carb value for display
                string result = apiInstance.StatisticsFormatCarbDisplay(value);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsFormatCarbDisplay: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsFormatCarbDisplayWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Format carb value for display
    ApiResponse<string> response = apiInstance.StatisticsFormatCarbDisplayWithHttpInfo(value);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsFormatCarbDisplayWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **value** | **double** | Carb value |  |

### Return type

**string**

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | Formatted carb string |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticsformatinsulindisplay"></a>
# **StatisticsFormatInsulinDisplay**
> string StatisticsFormatInsulinDisplay (double value)

Format insulin value for display

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
    public class StatisticsFormatInsulinDisplayExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var value = 1.2D;  // double | Insulin value

            try
            {
                // Format insulin value for display
                string result = apiInstance.StatisticsFormatInsulinDisplay(value);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsFormatInsulinDisplay: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsFormatInsulinDisplayWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Format insulin value for display
    ApiResponse<string> response = apiInstance.StatisticsFormatInsulinDisplayWithHttpInfo(value);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsFormatInsulinDisplayWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **value** | **double** | Insulin value |  |

### Return type

**string**

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | Formatted insulin string |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticsgetaidsystemmetrics"></a>
# **StatisticsGetAidSystemMetrics**
> AidSystemMetrics StatisticsGetAidSystemMetrics (DateTimeOffset? startDate = null, DateTimeOffset? endDate = null)

Calculates AID (Automated Insulin Delivery) system metrics for a date range. Uses patient device records to segment the period by algorithm and compute time-weighted metrics via IAidMetricsService.

Fetches APS snapshots, temp basals, device events, glucose readings, and target-range schedules from their respective repositories. CGM metrics are derived from AnalyzeGlucoseData. Target range is optional; the method continues without it if the repository throws.

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
    public class StatisticsGetAidSystemMetricsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var startDate = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? | Inclusive start of the analysis period (UTC). (optional) 
            var endDate = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? | Inclusive end of the analysis period (UTC). (optional) 

            try
            {
                // Calculates AID (Automated Insulin Delivery) system metrics for a date range. Uses patient device records to segment the period by algorithm and compute time-weighted metrics via IAidMetricsService.
                AidSystemMetrics result = apiInstance.StatisticsGetAidSystemMetrics(startDate, endDate);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsGetAidSystemMetrics: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsGetAidSystemMetricsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Calculates AID (Automated Insulin Delivery) system metrics for a date range. Uses patient device records to segment the period by algorithm and compute time-weighted metrics via IAidMetricsService.
    ApiResponse<AidSystemMetrics> response = apiInstance.StatisticsGetAidSystemMetricsWithHttpInfo(startDate, endDate);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsGetAidSystemMetricsWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **startDate** | **DateTimeOffset?** | Inclusive start of the analysis period (UTC). | [optional]  |
| **endDate** | **DateTimeOffset?** | Inclusive end of the analysis period (UTC). | [optional]  |

### Return type

[**AidSystemMetrics**](AidSystemMetrics.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | An AidSystemMetrics object containing loop-on time, site-change counts,             CGM active percent, and per-algorithm segment breakdowns. |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticsgetbasalanalysis"></a>
# **StatisticsGetBasalAnalysis**
> BasalAnalysisResponse StatisticsGetBasalAnalysis (DateTimeOffset? startDate = null, DateTimeOffset? endDate = null)

Calculate comprehensive basal analysis statistics for a date range

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
    public class StatisticsGetBasalAnalysisExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var startDate = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? | Start date of the analysis period (optional) 
            var endDate = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? | End date of the analysis period (optional) 

            try
            {
                // Calculate comprehensive basal analysis statistics for a date range
                BasalAnalysisResponse result = apiInstance.StatisticsGetBasalAnalysis(startDate, endDate);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsGetBasalAnalysis: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsGetBasalAnalysisWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Calculate comprehensive basal analysis statistics for a date range
    ApiResponse<BasalAnalysisResponse> response = apiInstance.StatisticsGetBasalAnalysisWithHttpInfo(startDate, endDate);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsGetBasalAnalysisWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **startDate** | **DateTimeOffset?** | Start date of the analysis period | [optional]  |
| **endDate** | **DateTimeOffset?** | End date of the analysis period | [optional]  |

### Return type

[**BasalAnalysisResponse**](BasalAnalysisResponse.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | Comprehensive basal analysis with stats, temp basal info, and hourly percentiles |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticsgetclinicaltargets"></a>
# **StatisticsGetClinicalTargets**
> ClinicalTargets StatisticsGetClinicalTargets (DiabetesPopulation population)

Get clinical targets for a specific diabetes population

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
    public class StatisticsGetClinicalTargetsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var population = (DiabetesPopulation) "0";  // DiabetesPopulation | Population type (Type1Adult, Type2Adult, Elderly, Pregnancy, etc.)

            try
            {
                // Get clinical targets for a specific diabetes population
                ClinicalTargets result = apiInstance.StatisticsGetClinicalTargets(population);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsGetClinicalTargets: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsGetClinicalTargetsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Get clinical targets for a specific diabetes population
    ApiResponse<ClinicalTargets> response = apiInstance.StatisticsGetClinicalTargetsWithHttpInfo(population);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsGetClinicalTargetsWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **population** | **DiabetesPopulation** | Population type (Type1Adult, Type2Adult, Elderly, Pregnancy, etc.) |  |

### Return type

[**ClinicalTargets**](ClinicalTargets.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | Clinical targets for the specified population |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticsgetdailybasalbolusratios"></a>
# **StatisticsGetDailyBasalBolusRatios**
> DailyBasalBolusRatioResponse StatisticsGetDailyBasalBolusRatios (DateTimeOffset? startDate = null, DateTimeOffset? endDate = null)

Calculate daily basal/bolus ratio statistics for a date range

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
    public class StatisticsGetDailyBasalBolusRatiosExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var startDate = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? | Start date of the analysis period (optional) 
            var endDate = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? | End date of the analysis period (optional) 

            try
            {
                // Calculate daily basal/bolus ratio statistics for a date range
                DailyBasalBolusRatioResponse result = apiInstance.StatisticsGetDailyBasalBolusRatios(startDate, endDate);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsGetDailyBasalBolusRatios: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsGetDailyBasalBolusRatiosWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Calculate daily basal/bolus ratio statistics for a date range
    ApiResponse<DailyBasalBolusRatioResponse> response = apiInstance.StatisticsGetDailyBasalBolusRatiosWithHttpInfo(startDate, endDate);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsGetDailyBasalBolusRatiosWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **startDate** | **DateTimeOffset?** | Start date of the analysis period | [optional]  |
| **endDate** | **DateTimeOffset?** | End date of the analysis period | [optional]  |

### Return type

[**DailyBasalBolusRatioResponse**](DailyBasalBolusRatioResponse.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | Daily basal/bolus ratio breakdown with averages |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticsgetinsulindeliverystatistics"></a>
# **StatisticsGetInsulinDeliveryStatistics**
> InsulinDeliveryStatistics StatisticsGetInsulinDeliveryStatistics (DateTimeOffset? startDate = null, DateTimeOffset? endDate = null)

Calculate comprehensive insulin delivery statistics for a date range

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
    public class StatisticsGetInsulinDeliveryStatisticsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var startDate = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? | Start date of the analysis period (optional) 
            var endDate = DateTimeOffset.Parse("2013-10-20T19:20:30+01:00");  // DateTimeOffset? | End date of the analysis period (optional) 

            try
            {
                // Calculate comprehensive insulin delivery statistics for a date range
                InsulinDeliveryStatistics result = apiInstance.StatisticsGetInsulinDeliveryStatistics(startDate, endDate);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsGetInsulinDeliveryStatistics: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsGetInsulinDeliveryStatisticsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Calculate comprehensive insulin delivery statistics for a date range
    ApiResponse<InsulinDeliveryStatistics> response = apiInstance.StatisticsGetInsulinDeliveryStatisticsWithHttpInfo(startDate, endDate);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsGetInsulinDeliveryStatisticsWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **startDate** | **DateTimeOffset?** | Start date of the analysis period | [optional]  |
| **endDate** | **DateTimeOffset?** | End date of the analysis period | [optional]  |

### Return type

[**InsulinDeliveryStatistics**](InsulinDeliveryStatistics.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | Comprehensive insulin delivery statistics |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticsgetmultiperiodstatistics"></a>
# **StatisticsGetMultiPeriodStatistics**
> MultiPeriodStatistics StatisticsGetMultiPeriodStatistics ()

Gets comprehensive statistics for multiple time periods (1, 3, 7, 30, and 90 days). Fetches sensor glucose, bolus, carb, and temp-basal data from the database for each period, computes GlucoseAnalytics, TreatmentSummary, and InsulinDeliveryStatistics, and caches the result for 5 minutes.

When no TempBasal or algorithm bolus records are found but a profile is loaded, the method falls back to computing scheduled basal from the active profile schedule via IBasalRateResolver. GMI reliability is assessed per-period using context-appropriate recommended-day minimums (e.g., 1-day periods cannot require 14 days of data).

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
    public class StatisticsGetMultiPeriodStatisticsExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);

            try
            {
                // Gets comprehensive statistics for multiple time periods (1, 3, 7, 30, and 90 days). Fetches sensor glucose, bolus, carb, and temp-basal data from the database for each period, computes GlucoseAnalytics, TreatmentSummary, and InsulinDeliveryStatistics, and caches the result for 5 minutes.
                MultiPeriodStatistics result = apiInstance.StatisticsGetMultiPeriodStatistics();
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsGetMultiPeriodStatistics: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsGetMultiPeriodStatisticsWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Gets comprehensive statistics for multiple time periods (1, 3, 7, 30, and 90 days). Fetches sensor glucose, bolus, carb, and temp-basal data from the database for each period, computes GlucoseAnalytics, TreatmentSummary, and InsulinDeliveryStatistics, and caches the result for 5 minutes.
    ApiResponse<MultiPeriodStatistics> response = apiInstance.StatisticsGetMultiPeriodStatisticsWithHttpInfo();
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsGetMultiPeriodStatisticsWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters
This endpoint does not need any parameter.
### Return type

[**MultiPeriodStatistics**](MultiPeriodStatistics.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | A MultiPeriodStatistics containing a PeriodStatistics             entry for each of the five standard periods. |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticsmgdltommol"></a>
# **StatisticsMgdlToMMOL**
> double StatisticsMgdlToMMOL (double mgdl)

Convert mg/dL to mmol/L

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
    public class StatisticsMgdlToMMOLExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var mgdl = 1.2D;  // double | Glucose value in mg/dL

            try
            {
                // Convert mg/dL to mmol/L
                double result = apiInstance.StatisticsMgdlToMMOL(mgdl);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsMgdlToMMOL: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsMgdlToMMOLWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Convert mg/dL to mmol/L
    ApiResponse<double> response = apiInstance.StatisticsMgdlToMMOLWithHttpInfo(mgdl);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsMgdlToMMOLWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **mgdl** | **double** | Glucose value in mg/dL |  |

### Return type

**double**

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | Glucose value in mmol/L |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticsmmoltomgdl"></a>
# **StatisticsMmolToMGDL**
> double StatisticsMmolToMGDL (double mmol)

Convert mmol/L to mg/dL

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
    public class StatisticsMmolToMGDLExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var mmol = 1.2D;  // double | Glucose value in mmol/L

            try
            {
                // Convert mmol/L to mg/dL
                double result = apiInstance.StatisticsMmolToMGDL(mmol);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsMmolToMGDL: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsMmolToMGDLWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Convert mmol/L to mg/dL
    ApiResponse<double> response = apiInstance.StatisticsMmolToMGDLWithHttpInfo(mmol);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsMmolToMGDLWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **mmol** | **double** | Glucose value in mmol/L |  |

### Return type

**double**

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | Glucose value in mg/dL |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a id="statisticsvalidatetreatmentdata"></a>
# **StatisticsValidateTreatmentData**
> bool StatisticsValidateTreatmentData (Treatment treatment)

Validate treatment data for completeness and consistency

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
    public class StatisticsValidateTreatmentDataExample
    {
        public static void Main()
        {
            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // create instances of HttpClient, HttpClientHandler to be reused later with different Api classes
            HttpClient httpClient = new HttpClient();
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            var apiInstance = new StatisticsApi(httpClient, config, httpClientHandler);
            var treatment = new Treatment(); // Treatment | Treatment to validate

            try
            {
                // Validate treatment data for completeness and consistency
                bool result = apiInstance.StatisticsValidateTreatmentData(treatment);
                Debug.WriteLine(result);
            }
            catch (ApiException  e)
            {
                Debug.Print("Exception when calling StatisticsApi.StatisticsValidateTreatmentData: " + e.Message);
                Debug.Print("Status Code: " + e.ErrorCode);
                Debug.Print(e.StackTrace);
            }
        }
    }
}
```

#### Using the StatisticsValidateTreatmentDataWithHttpInfo variant
This returns an ApiResponse object which contains the response data, status code and headers.

```csharp
try
{
    // Validate treatment data for completeness and consistency
    ApiResponse<bool> response = apiInstance.StatisticsValidateTreatmentDataWithHttpInfo(treatment);
    Debug.Write("Status Code: " + response.StatusCode);
    Debug.Write("Response Headers: " + response.Headers);
    Debug.Write("Response Body: " + response.Data);
}
catch (ApiException e)
{
    Debug.Print("Exception when calling StatisticsApi.StatisticsValidateTreatmentDataWithHttpInfo: " + e.Message);
    Debug.Print("Status Code: " + e.ErrorCode);
    Debug.Print(e.StackTrace);
}
```

### Parameters

| Name | Type | Description | Notes |
|------|------|-------------|-------|
| **treatment** | [**Treatment**](Treatment.md) | Treatment to validate |  |

### Return type

**bool**

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json


### HTTP response details
| Status code | Description | Response headers |
|-------------|-------------|------------------|
| **200** | True if treatment data is valid |  -  |

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

