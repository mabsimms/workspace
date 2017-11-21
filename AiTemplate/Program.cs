using System;
using System.Net.Http;

namespace AiTemplate
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            var httpClient = new HttpClient();
        }
    }
}

/*
    string SINGLE_INSTRUMENTATION_KEY = "ec241647-64cf-44b2-a1b9-4bf83983d59a";

            var TRACE_ID = Guid.NewGuid().ToString();


            //Devices initiates a logical operation
            var r = new RequestTelemetry(
                name: "DevicesAction", //this is the name of the operation that initiated the entire distributed trace
                startTime: DateTimeOffset.Now,
                duration: TimeSpan.FromSeconds(1),
                responseCode: "200",
                success: true)
            {
                Source = "", //no source specified
                Url = null, // you can omit it if you do not use it
            };
            r.Context.Operation.Id = TRACE_ID; // initiate the logical operation ID (trace id)
            r.Context.Operation.ParentId = null; // this is the first span in a trace
            r.Context.Cloud.RoleName = "Devices"; // this is the name of the node on app map

            new TelemetryClient() { InstrumentationKey = SINGLE_INSTRUMENTATION_KEY }.TrackRequest(r);

            // Devices calls into mas-shake. 
            var d = new DependencyTelemetry(
                dependencyTypeName: "Http",
                target: $"mas-shake.com", //host name
                dependencyName: "GET /search",
                data: "https://mas-shake.com/search/q='search query'",
                startTime: DateTimeOffset.Now,
                duration: TimeSpan.FromSeconds(1),
                resultCode: "200",
                success: true);
            d.Context.Operation.ParentId = r.Id;
            d.Context.Operation.Id = TRACE_ID;
            d.Context.Cloud.RoleName = "Devices"; // this is the name of the node on app map


            new TelemetryClient() { InstrumentationKey = SINGLE_INSTRUMENTATION_KEY }.TrackDependency(d);

            // The following headers got propagated with the http request
            //  Request-Id: |<r.Id>
            //  Request-Context: appId=cid-v1:{DEVICES_APP_ID}
            //


            //Rerquest got recieved by MAS_SHAKE:

            r = new RequestTelemetry(
               name: "GET /search", //this is the name of the operation that initiated the entire distributed trace
               startTime: DateTimeOffset.Now,
               duration: TimeSpan.FromSeconds(1),
               responseCode: "200",
               success: true)
            {
                Source = "", // not used
                Url = new Uri("https://mas-shake.com/search/q='search query'"), // you can omit it if you do not use it
            };
            r.Context.Operation.Id = TRACE_ID; // initiate the logical operation ID (trace id)
            r.Context.Operation.ParentId = d.Id; // this is the first span in a trace
            r.Context.Cloud.RoleName = "mas-shake"; // this is the name of the node on app map

            new TelemetryClient() { InstrumentationKey = SINGLE_INSTRUMENTATION_KEY }.TrackRequest(r);

            d = new DependencyTelemetry(
                dependencyTypeName: "http",
                target: $"api.twitter.com", 
                dependencyName: "POST /twit",
                data: "https://api.twitter.com/twit",
                startTime: DateTimeOffset.Now,
                duration: TimeSpan.FromSeconds(1),
                resultCode: "200",
                success: true);
            d.Context.Operation.ParentId = r.Id;
            d.Context.Operation.Id = TRACE_ID;
            d.Context.Cloud.RoleName = "mas-shake"; // this is the name of the node on app map

            new TelemetryClient() { InstrumentationKey = SINGLE_INSTRUMENTATION_KEY }.Track(d);

            TelemetryConfiguration.Active.TelemetryChannel.Flush();


            Console.WriteLine("Sleep for some time before exiting...");
            Thread.Sleep(TimeSpan.FromSeconds(10));
        }
*/
