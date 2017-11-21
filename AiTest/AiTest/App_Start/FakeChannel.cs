using System;
using Microsoft.ApplicationInsights.Channel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace AiTest
{
    internal class FakeChannel : ITelemetryChannel
    {
      


        public bool? DeveloperMode { get; set; }
        public string EndpointAddress { get; set; }

        public void Dispose()
        {
        }

        public void Flush()
        {
        }

        public void Send(ITelemetry item)
        {
            var set = new ITelemetry[] { item }; 
            var serialized = Microsoft.ApplicationInsights.Extensibility.Implementation.JsonSerializer.Serialize(set, false);

            Debug.WriteLine("Serializing item type " + item.GetType().Name + " into:");
            Debug.WriteLine("-------------------------------------------------------");
            Debug.WriteLine(Encoding.UTF8.GetString(serialized));

             
            // TODO
        }
    }
}