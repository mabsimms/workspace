using Microsoft.Extensions.Configuration;

namespace Common 
{ 
    public class ListenerConfiguration { 
        public string Subject { get; set; }
        public int PublishBatchSize { get; set; }
        public int TransformBuffer { get; set; }
        public int TransformConcurrency { get; set; }
        public int BatchBuffer { get; set; }
        public int PublishBuffer { get; set; }
        public int PublishConcurrency { get; set; }

        public static ListenerConfiguration FromConfiguration(
            IConfiguration configuration) 
        {
            var config = new ListenerConfiguration();
            config.Subject = configuration.GetValue<string>("Subject", "telemetry");
            config.PublishBatchSize = configuration.GetValue<int>("PublishBatchSize", 1000);
            config.TransformBuffer = configuration.GetValue<int>("TransformBuffer", 1024);
            config.TransformConcurrency = configuration.GetValue<int>("TransformConcurrency", 1);
            config.BatchBuffer = configuration.GetValue<int>("BatchBuffer", 1);
            config.PublishBuffer = configuration.GetValue<int>("PublishBuffer", 8);
            config.PublishConcurrency = configuration.GetValue<int>("PublishConcurrency", 1);
            config.PublishBatchSize = configuration.GetValue<int>("PublishBatchSize", 1024);
            return config; 
        }
    }
}