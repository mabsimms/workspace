using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using AppInsights.Test;
using Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NATS.Client;

namespace Processor
{
    class Program
    {
        private static long clientsProcessed = 0;
        private static long exceptionCount = 0;
        private static long bytesRead = 0;
        private static long telemetryItemsRead = 0;
        private static long telemetryItemBatchesRead = 0;
        private static long bytesSerializedBack = 0;
        private static long readOperationsCount = 0;
        private static long serializationOperationsCount = 0;
        
        private static Stopwatch overallStopwatch = new Stopwatch();
        private static Stopwatch serializationStopwatch = new Stopwatch();
        private static Stopwatch readingStopwatch = new Stopwatch();
        private static Stopwatch readPipeStopwatch = new Stopwatch();
        private static Stopwatch validationStopwatch = new Stopwatch();

        private static ILogger logger;

        static void Main(string[] args)
        {


            //var threadStates = proc.Threads.OfType<ProcessThread>().Select(p => p.ThreadState).ToArray();
            //var threadIds = proc.Threads.OfType<ProcessThread>().Select(p => p.Id).ToArray();
            //Console.WriteLine(
            //    $@"Threads: {proc.Threads.Count}");
            //Console.WriteLine("Thread states: " + string.Join(", ", threadStates) + ", thread ids: " + string.Join(", ", threadIds));

            //ProcessThread mainThread = proc.Threads.OfType<ProcessThread>()
            //    .Single(thread => thread.ThreadState == System.Diagnostics.ThreadState.Running);

            // TODO - recreate monitor task
            // Task.Run(() =>
            // {
              
            // });


            var config = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();
            var listenerConfiguration = ListenerConfiguration.FromConfiguration(config);

            var loggerFactory = new LoggerFactory()
                .AddConsole();
            logger = loggerFactory.CreateLogger("default");
            var process = System.Diagnostics.Process.GetCurrentProcess();

            logger.LogInformation("Testing consumer speed against pid {pid} with {processorCount} procs", 
                process.Id, Environment.ProcessorCount);

            var cancellationTokenSource = new CancellationTokenSource();
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            var listenerTask = Task.Factory.StartNew(
                async () =>
                {
                    try
                    {
                        await RunConsumerNats(listenerConfiguration, cancellationTokenSource.Token);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("Consumer blew up");
                    }
                },
                TaskCreationOptions.LongRunning
            );
            listenerTask.GetAwaiter().GetResult();

            Console.WriteLine("Press <enter> to stop listening.. mebbe");
            Console.ReadLine();

            Console.WriteLine("Cancelling and closing..");

            cancellationTokenSource.Cancel();
            listenerTask.GetAwaiter().GetResult();
        }

        private static async Task MonitorAsync() { 
              try
                {
                             Process proc = Process.GetCurrentProcess();

                    TimeSpan previousTimestamp = TimeSpan.FromSeconds((double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);
                    long previousTelemetryItemsRead = 0;
                    long previousReadOperationsCount = 0;
                    long previousBytesRead = 0;
                    long previousSerializationOperationsCount = 0;

                    TimeSpan previousInfoCpuTime = TimeSpan.Zero;
                    TimeSpan previousInfoUserCpuTime = TimeSpan.Zero;
                    TimeSpan previousInfoPrivilegedCpuTime = TimeSpan.Zero;

                    TimeSpan lastCpuTime = TimeSpan.Zero;
                    TimeSpan lastUserCpuTime = TimeSpan.Zero;
                    TimeSpan lastPrivilegedCpuTime = TimeSpan.Zero;

                    Thread.Sleep(TimeSpan.FromSeconds(1));

                    while (true)
                    {
                        TimeSpan currentTimestamp = TimeSpan.FromSeconds((double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);

                        TimeSpan totalTimeSinceLastInfo = currentTimestamp - previousTimestamp;

                        long telemetryItemsReadLocal = Interlocked.Read(ref telemetryItemsRead);
                        long telemetryItemsCount = telemetryItemsReadLocal - previousTelemetryItemsRead;

                        long readOperationsCountLocal = Interlocked.Read(ref readOperationsCount);
                        long readOperationsCountSinceLastInfo = readOperationsCountLocal - previousReadOperationsCount;

                        long bytesReadLocal = Interlocked.Read(ref bytesRead);
                        long bytesReadSinceLastInfo = bytesReadLocal - previousBytesRead;

                        long serializationOperationsCountLocal = Interlocked.Read(ref serializationOperationsCount);
                        long serializationOperationsCountSinceLastInfo = serializationOperationsCountLocal - previousSerializationOperationsCount;

                        proc.Refresh();
                        bool procTicksUpdated = proc.TotalProcessorTime != lastCpuTime ||
                                                proc.UserProcessorTime != lastUserCpuTime ||
                                                proc.PrivilegedProcessorTime != lastPrivilegedCpuTime;

                        if (procTicksUpdated && totalTimeSinceLastInfo.TotalSeconds > 10 && telemetryItemsCount > 0)
                        {
                            Console.WriteLine(
                                $@"Time: {DateTime.Now.ToLongTimeString()}, Change: {
                                        String.Join("",
                                            new[]
                                            {
                                                proc.TotalProcessorTime != previousInfoCpuTime ? 1 : 0,
                                                proc.UserProcessorTime != previousInfoUserCpuTime ? 1 : 0,
                                                proc.PrivilegedProcessorTime != previousInfoPrivilegedCpuTime ? 1 : 0
                                            })
                                    }, Exceptions: {exceptionCount}, Batches: {telemetryItemBatchesRead}, Items: {
                                        telemetryItemsCount
                                    }, Items/s: {telemetryItemsCount / totalTimeSinceLastInfo.TotalSeconds:F0}, Mb/s: {
                                        bytesRead / totalTimeSinceLastInfo.TotalSeconds / 1024 / 1024
                                    :F0}, CPU (core): {
                                        (proc.TotalProcessorTime.Ticks - previousInfoCpuTime.Ticks) /
                                        ((double) totalTimeSinceLastInfo.Ticks) *
                                        100
                                    :F2}%, Proc ticks/item: {
                                        (proc.TotalProcessorTime.Ticks - previousInfoCpuTime.Ticks) / telemetryItemsCount
                                    :F0}(u: {
                                        (proc.UserProcessorTime.Ticks - previousInfoUserCpuTime.Ticks) / telemetryItemsCount
                                    :F0} p: {
                                        (proc.PrivilegedProcessorTime.Ticks - previousInfoPrivilegedCpuTime.Ticks) /
                                        telemetryItemsCount
                                    :F0}), Reads/sec: {
                                        readOperationsCountSinceLastInfo / totalTimeSinceLastInfo.TotalSeconds
                                    :F1}, Avg chunk size: {
                                        (double) bytesReadSinceLastInfo / readOperationsCountSinceLastInfo / 1024 / 1024
                                    :F1}Mb, Serializations/s: {serializationOperationsCountSinceLastInfo / totalTimeSinceLastInfo.TotalSeconds:F0}, RAM: {proc.WorkingSet64 / 1024 / 1024}Mb, Time/item: {
                                        totalTimeSinceLastInfo.Ticks / telemetryItemsCount
                                    }, Serialization time/item: {(double)serializationStopwatch.Elapsed.Ticks / telemetryItemsReadLocal:F0}, Wired: {bytesSerializedBack / 1024 / 1024}Mb");

                            previousTimestamp = currentTimestamp;
                            previousTelemetryItemsRead = telemetryItemsReadLocal;
                            previousBytesRead = bytesReadLocal;
                            previousReadOperationsCount = readOperationsCountLocal;
                            previousSerializationOperationsCount = serializationOperationsCountLocal;

                            previousInfoCpuTime = proc.TotalProcessorTime;
                            previousInfoUserCpuTime = proc.UserProcessorTime;
                            previousInfoPrivilegedCpuTime = proc.PrivilegedProcessorTime;
                        }

                        if (procTicksUpdated)
                        {
                            lastCpuTime = proc.TotalProcessorTime;
                            lastUserCpuTime = proc.UserProcessorTime;
                            lastPrivilegedCpuTime = proc.PrivilegedProcessorTime;
                        }

                        Thread.Sleep(TimeSpan.FromMilliseconds(100));
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Info thread crashed. " + e.ToString());
                    throw;
                }
        }

        private static async Task RunConsumerNats(ListenerConfiguration configuration,
            CancellationToken token)  
        {
            var cf = new ConnectionFactory();
            var conn = cf.CreateConnection();

            var transformBlock = new TransformBlock<byte[], TelemetryItem>(
                transform: (data) => DeserializeTelemetryItem(data), 
                dataflowBlockOptions: new ExecutionDataflowBlockOptions() {
                    BoundedCapacity = configuration.TransformBuffer,
                    MaxDegreeOfParallelism = configuration.TransformConcurrency
                }
            );

            var batchBlock = new BatchBlock<TelemetryItem>(
                configuration.PublishBatchSize, 
                new GroupingDataflowBlockOptions() { 
                    BoundedCapacity = configuration.PublishBatchSize * 2,                    
                    Greedy = true
                }
            );
            var publishBlock = new ActionBlock<TelemetryItem[]>(
                action: PublishTelemetry, 
                dataflowBlockOptions: new ExecutionDataflowBlockOptions() { 
                    BoundedCapacity = configuration.PublishBuffer,
                    MaxDegreeOfParallelism = configuration.PublishConcurrency
                }
            );

            var disposables = new IDisposable[] { 
                transformBlock.LinkTo(batchBlock),
                batchBlock.LinkTo(publishBlock)
            };
            
            EventHandler<MsgHandlerEventArgs> handler = async (sender, args) => { 
                await transformBlock.SendAsync(args.Message.Data);
            };

            logger.LogInformation("Creating subscription on subject {subject}", configuration.Subject);
            var subscription = conn.SubscribeAsync(
                 configuration.Subject, handler);
            subscription.Start();
            
            while (!token.IsCancellationRequested)
            {
                var rate = Interlocked.Exchange(ref telemetryItemsRead, 0);
                logger.LogInformation("Received {messageCount} messages / second", rate);
                batchBlock.TriggerBatch();
                await Task.Delay(1000);
            }

            // TODO: better as using
            foreach (var d in disposables)
                d.Dispose();
            subscription.Dispose();
            conn.Dispose();            
        }

        private static async Task PublishTelemetry(TelemetryItem[] items)
        {
            // Just increment counters for now
            Interlocked.Add(ref telemetryItemsRead, items.Length);                                

            if (Interlocked.Read(ref telemetryItemsRead) % 10000 == 0) {
                Console.Write(".");            
            }
        }

        private static TelemetryItem DeserializeTelemetryItem(byte[] arg)
        {            
            return TelemetryItem.Parser.ParseFrom(arg);
        }

        // private static async Task StartServerSocket()
        // {
        //     TcpListener server = null;
        //     try
        //     {
        //         var port = 49152;
        //         var localAddr = IPAddress.Parse(Common.Common.TargetMachine);

        //         try
        //         {
        //             server = new TcpListener(localAddr, port);

        //             // Start listening for client requests.
        //             server.Start();
        //         }
        //         catch (Exception e)
        //         {
        //             throw new InvalidOperationException("Could not initialize the TcpListener", e);
        //         }

        //         // Buffer for reading data
        //         //Byte[] bytes = new Byte[Common.Common.TelemetryItemSizeInBytes];

        //         // Enter the listening loop.
        //         while (true)
        //         {
        //             //Console.Write("Waiting for a connection... ");
        //             TcpClient client = null;
        //             try
        //             {
        //                 // Perform a blocking call to accept requests.
        //                 // You could also user server.AcceptSocket() here.
        //                 try
        //                 {
        //                     client = await server.AcceptTcpClientAsync().ConfigureAwait(false);
        //                 }
        //                 catch (Exception e)
        //                 {
        //                     Console.WriteLine($"AcceptTcpClientAsync failed. {e.Message}, {e.InnerException?.Message}");
        //                     throw;
        //                 }

        //                 if (!overallStopwatch.IsRunning)
        //                 {
        //                     overallStopwatch.Start();
        //                 }

        //                 // Get a stream object for reading and writing
        //                 NetworkStream stream;
        //                 try
        //                 {
        //                     stream = client.GetStream();
        //                 }
        //                 catch (Exception e)
        //                 {
        //                     Console.WriteLine($"GetStream failed. {e.Message}, {e.InnerException?.Message}");
        //                     throw;
        //                 }
                        
        //                 Action<int> bytesReadProgressCallback = chunkByteRead => bytesRead += chunkByteRead;
                        
        //                 try
        //                 {
        //                     for (int i = 0; i < Math.Ceiling((double)Common.Common.TelemetryItemCount / Common.Common.BatchSizeInTelemetryItems); i++)
        //                     {
        //                         TelemetryItemBatch telemetryItemBatch = await NetworkStreamReader
        //                             .ReadSocket(stream,
        //                                 bytesReadProgressCallback)
        //                             .ConfigureAwait(false);

        //                         Interlocked.Add(ref telemetryItemsRead, telemetryItemBatch.Items.Length);
        //                         telemetryItemBatchesRead++;

        //                         if (telemetryItemBatch.Items.Length != Common.Common.BatchSizeInTelemetryItems ||
        //                             telemetryItemBatch.Items.Any(telemetryItem => telemetryItem.Id != 42 && telemetryItem.Id != 43 ||
        //                                                                           telemetryItem.Name.Length !=
        //                                                                           Common.Common
        //                                                                               .TelemetryItemSizeInBytes ||
        //                                                                           telemetryItem.Name[0] != 'D' && telemetryItem.Name[0] != 'E'))
        //                         {
        //                             throw new ArgumentException($"Invalid payload");
        //                         }

        //                         // serialize again (to send over the wire)
        //                         var fakeData = new byte[10 * Common.Common.TelemetryItemSizeInBytes];
        //                         var fakeStream = new MemoryStream(fakeData);
        //                         foreach (var item in telemetryItemBatch.Items)
        //                         {
        //                             fakeStream.Seek(0, SeekOrigin.Begin);
        //                             Serializer.Serialize(fakeStream, item);
        //                             bytesSerializedBack += fakeStream.Position;
        //                         }
        //                     }
        //                 }
        //                 catch (Exception e)
        //                 {
        //                     Console.WriteLine(
        //                         $"NetworkStreamReader.ReadSocket failed. {e.GetType().FullName} {e.Message} {e.InnerException?.Message}");
        //                     throw;
        //                 }

        //                 byte[] msg = { 0x01, 0xFF };//System.Text.Encoding.ASCII.GetBytes( /*data*/"OK");

        //                 // Send back a response.
        //                 await stream.WriteAsync(msg, 0, msg.Length).ConfigureAwait(false);
        //             }
        //             catch (Exception e)
        //             {
        //                 exceptionCount++;
        //                 Console.WriteLine($"EXCEPTION while processing a client's request: {e}");
        //             }
        //             finally
        //             {
        //                 client?.Close();

        //                 clientsProcessed++;
        //             }
        //         }
        //     }
        //     catch (Exception e)
        //     {
        //         throw new InvalidOperationException("Unknown exception initializing TCP server", e);
        //     }
        //     finally
        //     {
        //         // Stop listening for new clients.
        //         server?.Stop();
        //     }
        // }

        // private static async Task StartServerPipe()
        // {
        //     NamedPipeServerStream pipe = null;
        //     try
        //     {
        //         try
        //         {
        //             pipe = new NamedPipeServerStream("LocalServerPipe", PipeDirection.In, 1, PipeTransmissionMode.Byte);
        //             await pipe.WaitForConnectionAsync().ConfigureAwait(false);
        //         }
        //         catch (Exception e)
        //         {
        //             throw new InvalidOperationException("Could not initialize the pipe", e);
        //         }

        //         Console.WriteLine("Client connected");

        //         // assuming we allocate once for the largest batch possible
        //         var pooledMemoryBuffer = new byte[2 * Common.Common.TelemetryItemSizeInBytes * Common.Common.BatchSizeInTelemetryItems];

        //         while (true)
        //         {
        //             try
        //             {
        //                 Action<int> bytesReadProgressCallback =
        //                     chunkByteRead =>
        //                     {
        //                         Interlocked.Add(ref bytesRead, chunkByteRead);
        //                         Interlocked.Increment(ref readOperationsCount);
        //                     };

        //                 try
        //                 {
        //                     TelemetryItemBatch telemetryItemBatch = await NetworkStreamReader.ReadPipe(pipe,
        //                         bytesReadProgressCallback).ConfigureAwait(false);

        //                     Interlocked.Add(ref telemetryItemsRead, telemetryItemBatch.Items.Length);
        //                     telemetryItemBatchesRead++;

        //                     if (telemetryItemBatch.Items.Length != Common.Common.BatchSizeInTelemetryItems ||
        //                         telemetryItemBatch.Items.Any(telemetryItem => telemetryItem.Id != 42 && telemetryItem.Id != 43 ||
        //                                                                       telemetryItem.Name.Length !=
        //                                                                       Common.Common.TelemetryItemSizeInBytes ||
        //                                                                       telemetryItem.Name[0] != 'D' && telemetryItem.Name[0] != 'E'))
        //                     {
        //                         throw new ArgumentException($"Invalid payload");
        //                     }

        //                     // serialize again (to send over the wire to the backend)
        //                     // the assumption here is that some sort of memory pool is in place to avoid allocating a new memory block for every batch
        //                     using (var fakeStream = new MemoryStream(pooledMemoryBuffer))
        //                     {
        //                         foreach (var item in telemetryItemBatch.Items)
        //                         {
        //                             Serializer.Serialize(fakeStream, item);

        //                             bytesSerializedBack += fakeStream.Position;
        //                         }
        //                     }
        //                 }
        //                 catch (Exception e)
        //                 {
        //                     Console.WriteLine(
        //                         $"NetworkStreamReader.ReadPipe failed. {e.GetType().FullName} {e.Message} {e.InnerException?.Message}");
        //                     throw;
        //                 }
        //             }
        //             catch (Exception e)
        //             {
        //                 exceptionCount++;
        //                 Console.WriteLine($"EXCEPTION while processing a client's request: {e.Message}");
        //             }
        //             finally
        //             {
        //             }
        //         }
        //     }
        //     catch (Exception e)
        //     {
        //         throw new InvalidOperationException("Unknown exception initializing TCP server", e);
        //     }
        //     finally
        //     {
        //         pipe?.Dispose();
        //     }
        // }
    }
}