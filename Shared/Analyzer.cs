using Microsoft.Azure.Cosmos;

namespace Shared
{
    public static class Analyzer
    {
        public static void AnalyzeResponse<T>(ItemResponse<T>? response)
        {
            if (response is null) return;

            Console.WriteLine($"Request Charge: {response.RequestCharge} RU");

            if (response.Diagnostics is CosmosDiagnostics cosmosDiagnostics)
            {
                AnalyzeDiDiagnostics(cosmosDiagnostics);
            }
            if (response.Headers is Headers headers)
            {
                Console.WriteLine($"Activity Id: {headers.ActivityId}");

                Console.WriteLine($"Request Charge: {headers.RequestCharge} RU");

                Console.WriteLine("Response Headers:");
                foreach (string headerKey in headers.AllKeys())
                {
                    Console.WriteLine($"{headerKey}: {headers[headerKey]}");
                }

                Console.WriteLine($"Continuation Token: {headers.ContinuationToken}");

                Console.WriteLine($"Content Length: {headers.ContentLength}");

                Console.WriteLine($"Content Type: {headers.ContentType}");

                Console.WriteLine($"Location: {headers.Location}");

                Console.WriteLine($"Session Token: {headers.Session}");

                Console.WriteLine($"ETag: {headers.ETag}");
            }
            if (response.ETag is string etag)
            {
                Console.WriteLine($"ETag: {etag}");
            }
        }

        public static void AnalyzeDiDiagnostics(CosmosDiagnostics cosmosDiagnostics)
        {
            Console.WriteLine($"Start Time: {cosmosDiagnostics.GetStartTimeUtc}");
            Console.WriteLine($"End Time: {cosmosDiagnostics.GetClientElapsedTime}");

            Console.WriteLine($"Failed Request Count: {cosmosDiagnostics.GetFailedRequestCount}");

            Console.WriteLine($"Contacted Regions:");
            foreach ((string regionName, Uri uri) in cosmosDiagnostics.GetContactedRegions())
            {
                Console.WriteLine($"Region Name: {regionName}");
                Console.WriteLine($"Uri: {uri}");
            }

            Console.WriteLine("Server Side Partitioned Metrics:");
            ServerSideCumulativeMetrics queryMetric = cosmosDiagnostics.GetQueryMetrics();

            foreach (ServerSidePartitionedMetrics metric in queryMetric.PartitionedMetrics)
            {
                Console.WriteLine($"FeedRange: {metric.FeedRange}");
                Console.WriteLine($"PartitionKeyRangeId: {metric.PartitionKeyRangeId}");
                Console.WriteLine($"Request Charge: {metric.RequestCharge}");
                Console.WriteLine($"Query Preparing Time: {metric.ServerSideMetrics.QueryPreparationTime}");

                Console.WriteLine($"Index Hit Ratio: {metric.ServerSideMetrics.IndexHitRatio}");
                Console.WriteLine($"Index Lookup Time: {metric.ServerSideMetrics.IndexLookupTime}");

                Console.WriteLine($"Document Write Time: {metric.ServerSideMetrics.DocumentWriteTime}");
                Console.WriteLine($"Document Load Time: {metric.ServerSideMetrics.DocumentLoadTime}");

                Console.WriteLine($"Runtime Execution Time: {metric.ServerSideMetrics.RuntimeExecutionTime}");
                Console.WriteLine($"VM Execution Time: {metric.ServerSideMetrics.VMExecutionTime}");

                Console.WriteLine($"Total Time: {metric.ServerSideMetrics.TotalTime}");

                Console.WriteLine($"Retrieved Document Size: {metric.ServerSideMetrics.RetrievedDocumentSize}");
                Console.WriteLine($"Retrieved Document Count: {metric.ServerSideMetrics.RetrievedDocumentCount}");

                Console.WriteLine($"Output Document Size: {metric.ServerSideMetrics.OutputDocumentSize}");
                Console.WriteLine($"Output Document Count: {metric.ServerSideMetrics.OutputDocumentCount}");
            }

            Console.WriteLine($"Cumulative Metrics:");
            Console.WriteLine($"Query Preparing Time: {queryMetric.CumulativeMetrics.QueryPreparationTime}");

            Console.WriteLine($"Index Hit Ratio: {queryMetric.CumulativeMetrics.IndexHitRatio}");
            Console.WriteLine($"Index Lookup Time: {queryMetric.CumulativeMetrics.IndexLookupTime}");

            Console.WriteLine($"Document Write Time: {queryMetric.CumulativeMetrics.DocumentWriteTime}");
            Console.WriteLine($"Document Load Time: {queryMetric.CumulativeMetrics.DocumentLoadTime}");

            Console.WriteLine($"Runtime Execution Time: {queryMetric.CumulativeMetrics.RuntimeExecutionTime}");
            Console.WriteLine($"VM Execution Time: {queryMetric.CumulativeMetrics.VMExecutionTime}");

            Console.WriteLine($"Total Time: {queryMetric.CumulativeMetrics.TotalTime}");

            Console.WriteLine($"Retrieved Document Size: {queryMetric.CumulativeMetrics.RetrievedDocumentSize}");
            Console.WriteLine($"Retrieved Document Count: {queryMetric.CumulativeMetrics.RetrievedDocumentCount}");

            Console.WriteLine($"Output Document Size: {queryMetric.CumulativeMetrics.OutputDocumentSize}");
            Console.WriteLine($"Output Document Count: {queryMetric.CumulativeMetrics.OutputDocumentCount}");
        }
    }
}
