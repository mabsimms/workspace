using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Threading;
using Common;
using System.Threading.Tasks;
using Google.Protobuf;

using NATS.Client;
using System.Linq;
using System.Diagnostics;
using AppInsights.Test;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks.Dataflow;

namespace IpcTest
{
    class Program
    {
        private static readonly Random rand = new Random();

        private static long itemsSent = 0;

        private static long batchesSent = 0;

        private static long bytesSent = 0;

        static void Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();
            var testRunConfiguration = TestRunConfiguration.FromConfiguration(config);

            var loggerFactory = new LoggerFactory()
                .AddConsole();
            var logger = loggerFactory.CreateLogger("default");
            var process = System.Diagnostics.Process.GetCurrentProcess();

            logger.LogInformation("Testing speed against pid {pid} with {processorCount} procs", 
                process.Id, Environment.ProcessorCount);


            var cancellationTokenSource = new CancellationTokenSource();
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            var monitorTask = Task.Factory.StartNew( async () => 
            { 
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));

                    var cpuMeasure = process.TotalProcessorTime.Ticks 
                        / (double)Environment.ProcessorCount 
                        * (DateTime.Now - process.StartTime).Ticks 
                        * 100
                        ;

                    var items = Interlocked.Exchange(ref itemsSent, 0);
                    var bytes = Interlocked.Exchange(ref bytesSent, 0);

                    logger.LogInformation(
                        "Items sent {itemsSent} bytes sent {bytesSent} cpu {cpuTime}",
                        items, bytes, cpuMeasure                        
                    );
                }
            }, 
            TaskCreationOptions.LongRunning);
  
            var parallelOptions = new ParallelOptions() { 
                MaxDegreeOfParallelism = testRunConfiguration.ProducerConcurrency
            };

            // TODO - concurrent producers.  Not really a great pattern; better
            // to use DataFlow to consolidate streams down to a single producer
            var producerTask = Task.Factory.StartNew( async () => { 
                await RunProducerNats(testRunConfiguration);
            }, 
            TaskCreationOptions.LongRunning);
            producerTask.GetAwaiter().GetResult();

            logger.LogInformation("Producer complete; shutting down");

            cancellationTokenSource.Cancel();
            monitorTask.GetAwaiter().GetResult();
        }

        private static async Task RunProducerNats(
            TestRunConfiguration runConfiguration) 
        { 
            var cf = new ConnectionFactory();
            var conn = cf.CreateConnection();

            var referenceSet = GetReferenceSet(runConfiguration)
                .Select(e => e.ToByteArray());

            // Send individual serialized messages, use an adaptive technique for 
            // rate levelling
            // TODO - adapt this approach for concurrent sends
            using (var ms = new MemoryStream(1024 * 1024)) 
            { 
                var sw = Stopwatch.StartNew();
                int itemsSent = 0;

                foreach (var item in referenceSet) 
                { 
                    // No batching here.  this could be way more efficient.
                    conn.Publish(runConfiguration.Subject, item);    
                    itemsSent++;

                    // TODO - adaptive rate sending

                    // TODO; adapt to update in batches to remove CAS overhead
                    Interlocked.Increment(ref itemsSent);
                    Interlocked.Add(ref bytesSent, item.Length);
                }
            }
        }

        private static TelemetryItem[] GetReferenceSet(
            TestRunConfiguration runConfiguration)
        {
            var telemetryItems = Enumerable
                .Range(0, runConfiguration.TargetItemCount)
                .Select(e => new TelemetryItem()
                {
                    Id = rand.NextDouble() > 0.5 ? 42 : 43,
                    Name = new string(rand.NextDouble() > 0.5 ? 'D' : 'E', runConfiguration.TelemetryItemSizeInBytes)
                })
                .ToArray();

            return telemetryItems;
        }

        // private static async Task StartProducerSocket()
        // {
        //     TcpClient client = null;
        //     try
        //     {
        //         try
        //         {
        //             client = new TcpClient();
        //             await client.ConnectAsync(Common.Common.TargetMachine, 49152).ConfigureAwait(false);
        //         }
        //         catch (Exception e)
        //         {
        //             throw new InvalidOperationException("Error initializing socket", e);
        //         }

        //         NetworkStream stream = null;
        //         try
        //         {
        //             stream = client.GetStream();

        //             // Send the message to the connected TcpServer. 
        //             int numBatches = (int)Math.Ceiling((double)Common.Common.TelemetryItemCount / Common.Common.BatchSizeInTelemetryItems);
        //             byte[] batchData = new byte[10 * Common.Common.TelemetryItemSizeInBytes * Common.Common.BatchSizeInTelemetryItems];

        //             var startTime = DateTimeOffset.Now;
        //             TimeSpan expectedTimeToSendEverything =
        //                 TimeSpan.FromSeconds((double) numBatches * Common.Common.BatchSizeInTelemetryItems /
        //                                      Common.Common.TelemetryItemsPerSecond);
        //             var expectedEndTime = startTime + expectedTimeToSendEverything;

        //             for (int batch = 0; batch < numBatches; batch++)
        //             {
        //                 // pause if we're ahead of schedule
        //                 int telemetryItemsLeft = (numBatches - batch) * Common.Common.BatchSizeInTelemetryItems;
        //                 TimeSpan expectedTimeLeftBasedOnProgress =
        //                     TimeSpan.FromSeconds((double) telemetryItemsLeft / Common.Common.TelemetryItemsPerSecond);
        //                 TimeSpan expectedTimeLeftBasedOnOriginalSchedule = expectedEndTime - DateTimeOffset.Now;

        //                 if (expectedTimeLeftBasedOnProgress < expectedTimeLeftBasedOnOriginalSchedule)
        //                 {
        //                     Thread.Sleep(expectedTimeLeftBasedOnOriginalSchedule - expectedTimeLeftBasedOnProgress);
        //                 }

        //                 using (var ms = new MemoryStream(batchData))
        //                 {
        //                     var items = new List<TelemetryItem>();
        //                     for (int item = 0; item < Common.Common.BatchSizeInTelemetryItems; item++)
        //                     {
        //                         var telemetryItem = new TelemetryItem()
        //                         {
        //                             Id = rand.NextDouble() > 0.5 ? 42 : 43,
        //                             Name = new string(rand.NextDouble() > 0.5 ? 'D' : 'E', Common.Common.TelemetryItemSizeInBytes)
        //                         };

        //                         items.Add(telemetryItem);
        //                     }

        //                     var telemetryItemBatch = new TelemetryItemBatch();
        //                     telemetryItemBatch.Items.AddRange(items);
        //                     telemetryItemBatch.WriteTo(ms);                           
 
        //                     Interlocked.Add(ref bytesSerialized, ms.Position);

        //                     await ms.FlushAsync().ConfigureAwait(false);
        //                     await stream.WriteAsync(batchData, 0, (int) ms.Position).ConfigureAwait(false);

        //                     batchesSent++;
        //                     itemsSent += Common.Common.BatchSizeInTelemetryItems;
        //                 }
        //             }
                    
        //             // Buffer to store the response bytes.
        //             var data = new Byte[2];

        //             // String to store the response ASCII representation.

        //             // read response
        //             try
        //             {
        //                 await NetworkStreamReader.ReadSocket(stream, data, 2, null).ConfigureAwait(false);
        //             }
        //             catch (Exception e)
        //             {
        //                 throw new InvalidOperationException("Could not read response from the server", e);
        //             }

        //             if (data[0] != 0x01 || data[1] != 0xFF)
        //             {
        //                 throw new InvalidOperationException("Incorrect response received from server");
        //             }
        //         }
        //         catch (Exception e)
        //         {
        //             throw new InvalidOperationException("Error sending a packet", e);
        //         }
        //         finally
        //         {
        //             stream?.Dispose();
        //         }
        //     }
        //     finally
        //     {
        //         client?.Close();
        //     }
        // }

        // private static async Task StartProducerPipe()
        // {
        //     try
        //     {
        //         NamedPipeClientStream pipe;
        //         try
        //         {
        //             pipe = new NamedPipeClientStream(Common.Common.TargetMachine, "LocalServerPipe",
        //                 PipeDirection.Out, PipeOptions.Asynchronous);
        //             var ct = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        //             await pipe.ConnectAsync(ct.Token).ConfigureAwait(false);
        //         }
        //         catch (Exception e)
        //         {
        //             throw new InvalidOperationException("Error initializing the pipe. " + e.Message, e);
        //         }

        //         try
        //         {
        //             // Send the message to the connected pipe.
        //             int numBatches = (int)Math.Ceiling((double)Common.Common.TelemetryItemCount / Common.Common.BatchSizeInTelemetryItems);
        //             byte[] batchData = new byte[10 * Common.Common.TelemetryItemSizeInBytes * Common.Common.BatchSizeInTelemetryItems];

        //             var startTime = DateTimeOffset.Now;
        //             TimeSpan expectedTimeToSendEverything =
        //                 TimeSpan.FromSeconds((double)numBatches * Common.Common.BatchSizeInTelemetryItems /
        //                                      Common.Common.TelemetryItemsPerSecond);
        //             var expectedEndTime = startTime + expectedTimeToSendEverything;

        //             for (int batch = 0; batch < numBatches; batch++)
        //             {
        //                 // pause if we're ahead of schedule
        //                 int telemetryItemsLeft = (numBatches - batch) * Common.Common.BatchSizeInTelemetryItems;
        //                 TimeSpan expectedTimeLeftBasedOnProgress =
        //                     TimeSpan.FromSeconds((double)telemetryItemsLeft / Common.Common.TelemetryItemsPerSecond);
        //                 TimeSpan expectedTimeLeftBasedOnOriginalSchedule = expectedEndTime - DateTimeOffset.Now;

        //                 if (expectedTimeLeftBasedOnProgress < expectedTimeLeftBasedOnOriginalSchedule)
        //                 {
        //                     Thread.Sleep(expectedTimeLeftBasedOnOriginalSchedule - expectedTimeLeftBasedOnProgress);
        //                 }

        //                 using (var ms = new MemoryStream(batchData))
        //                 {
        //                     var telemetryItemBatch = new TelemetryItemBatch();
        //                     var items = new List<TelemetryItem>();
        //                     for (int item = 0; item < Common.Common.BatchSizeInTelemetryItems; item++)
        //                     {
        //                         var telemetryItem = new TelemetryItem()
        //                         {
        //                             Id = rand.NextDouble() > 0.5 ? 42 : 43,
        //                             Name = new string(rand.NextDouble() > 0.5 ? 'D' : 'E', Common.Common.TelemetryItemSizeInBytes)
        //                         };

        //                         items.Add(telemetryItem);
        //                     }

        //                     telemetryItemBatch.Items = items.ToArray();
        //                     Serializer.SerializeWithLengthPrefix(ms, telemetryItemBatch, PrefixStyle.Fixed32);

        //                     Interlocked.Add(ref bytesSerialized, ms.Position);

        //                     await ms.FlushAsync().ConfigureAwait(false);

        //                     var ct = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        //                     await pipe.WriteAsync(batchData, 0, (int) ms.Position, ct.Token).ConfigureAwait(false);
        //                 }

        //                 batchesSent++;
        //                 itemsSent += Common.Common.BatchSizeInTelemetryItems;
        //             }
        //         }
        //         catch (Exception e)
        //         {
        //             throw new InvalidOperationException("Error sending a packet", e);
        //         }
        //         finally
        //         {
        //             pipe?.WaitForPipeDrain();
        //             pipe?.Dispose();
        //         }
        //     }
        //     finally
        //     {
        //     }
        // }
    }
}