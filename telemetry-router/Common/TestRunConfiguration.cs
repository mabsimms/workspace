using Microsoft.Extensions.Configuration;

namespace Common { 
    public class TestRunConfiguration 
    {
        public int TelemetryItemSizeInBytes { get; set; }
        public int BatchSizeItems { get; set; }
        public int TargetRatePerSecond { get; set; }
        public int TargetItemCount { get; set; } 
        public string Subject { get; set; }
        public int ProducerConcurrency { get; set; }

        public static TestRunConfiguration FromConfiguration(
            IConfiguration configuration) 
        {
            var trc = new TestRunConfiguration();
            trc.BatchSizeItems = configuration.GetValue<int>("batchSizeItems", 100);
            trc.TargetItemCount = configuration.GetValue<int>("targetItemCount", 1000000);
            trc.TargetRatePerSecond = configuration.GetValue<int>("targetRatePerSecond", 50000);
            trc.TelemetryItemSizeInBytes = configuration.GetValue<int>("telemetryItemSizeInBytes", 1024);
            trc.Subject = configuration.GetValue<string>("subject", "telemetry");
            trc.ProducerConcurrency = configuration.GetValue<int>("producerConcurrency", 1);
            return trc;            
        }   
    }
}