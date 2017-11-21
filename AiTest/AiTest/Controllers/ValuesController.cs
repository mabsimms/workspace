using Microsoft.ApplicationInsights;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace AiTest.Controllers
{
    public class ValuesController : ApiController
    {
        // GET api/values
        public async Task<IEnumerable<string>> Get()
        {
            var httpClient = new HttpClient();
            await httpClient.GetStringAsync("http://www.vocm.com/");

            var tc = new TelemetryClient();
            tc.TrackDependency(
                dependencyTypeName: "containerSvc",
                target: "instanceName",
                dependencyName: "containerName",
                data: "serviceCallName",
                startTime: DateTime.UtcNow.AddSeconds(-5),
                duration: TimeSpan.FromSeconds(5),
                resultCode: "200",
                success: true);
             
            return new string[] { "value1", "value2" };
        }

         
    }
}
