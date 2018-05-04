// using System;
// using System.Diagnostics;
// using System.IO;
// using System.IO.Pipes;
// using System.Net.Sockets;
// using System.Threading;
// using System.Threading.Tasks;
// using AppInsights.Test;
// using Google.Protobuf;

// namespace Common
// {
//     public class NetworkStreamReader
//     {
//         public static readonly TelemetryItemBatch FakeBatch;
//         public static readonly MemoryStream SerializedFakeBatch;

//         private static readonly Random rand = new Random();

//         static NetworkStreamReader()
//         {
//             var items = new TelemetryItem[Common.BatchSizeInTelemetryItems]);

//             for (int i = 0; i < items.Length; i++)
//             {
//                 items[i] =
//                     new TelemetryItem()
//                     {
//                         Id = rand.NextDouble() > 0.5 ? 42 : 43,
//                         Name = new string(rand.NextDouble() > 0.5 ? 'D' : 'E', Common.TelemetryItemSizeInBytes)
//                     };
//             }

//             FakeBatch = new TelemetryItemBatch();
//             FakeBatch.Items.AddRange(items);
            

//             SerializedFakeBatch = new MemoryStream();
//             FakeBatch.WriteTo(SerializedFakeBatch);            
//         }

//         /// <summary>
//         /// All reads are sequential, so maintain a single buffer for incoming raw items
//         /// Reallocate whenever a larger item comes in
//         /// </summary>
//         static byte[] incomingItemDataRaw = new byte[0];

//         private static byte[] incomingItemLengthRaw = new byte[4];

//         public static async Task<TelemetryItemBatch> ReadSocket(NetworkStream stream, Action<int> bytesRead)
//         {
//             await ReadSocket(stream, incomingItemLengthRaw, incomingItemLengthRaw.Length, bytesRead).ConfigureAwait(false);


//             if (!Serializer.TryReadLengthPrefix(incomingItemLengthRaw, 0, incomingItemLengthRaw.Length, PrefixStyle.Fixed32,
//                 out int itemLength))
//             {
//                 throw new ArgumentException("Could not read the length prefix from the socket");
//             }

//             if (itemLength > incomingItemDataRaw.Length)
//             {
//                 incomingItemDataRaw = new byte[itemLength];
//             }

//             await ReadSocket(stream, incomingItemDataRaw, itemLength, bytesRead).ConfigureAwait(false);

//             using (var ms = new MemoryStream(incomingItemDataRaw, 0, itemLength))
//             {
//                 return Serializer.Deserialize<TelemetryItemBatch>(ms);
//             }
//         }

//         public static async Task<TelemetryItemBatch> ReadPipe(NamedPipeServerStream stream, Action<int> bytesRead)
//         {
//             await ReadPipe(stream, incomingItemLengthRaw, incomingItemLengthRaw.Length, null).ConfigureAwait(false);

//             if (!Serializer.TryReadLengthPrefix(incomingItemLengthRaw, 0, incomingItemLengthRaw.Length, PrefixStyle.Fixed32,
//                 out int itemLength))
//             {
//                 throw new ArgumentException("Could not read the length prefix from the pipe");
//             }
//             //int itemLength = 1032000;

//             if (itemLength > incomingItemDataRaw.Length)
//             {
//                 incomingItemDataRaw = new byte[itemLength];
//             }

//             await ReadPipe(stream, incomingItemDataRaw, itemLength, bytesRead).ConfigureAwait(false);

//             //SerializedFakeBatch.Seek(0, SeekOrigin.Begin);
//             //return Serializer.Deserialize<TelemetryItemBatch>(SerializedFakeBatch);
//             using (var ms = new MemoryStream(incomingItemDataRaw, 0, itemLength))
//             {
//                 return Serializer.Deserialize<TelemetryItemBatch>(ms);
//                 //return FakeBatch;
//             }
//         }

//         public static async Task ReadSocket(NetworkStream stream, byte[] bytes, int count, Action<int> bytesRead)
//         {
//             int bytesReadSoFar = 0;
//             while (bytesReadSoFar < count)
//             {
//                 int chunkSize;
//                 try
//                 {
//                     // this is a TCP socket, so ReadAsync will return 0 only when the connection has been terminated from the other side
//                     var ct = new CancellationTokenSource(TimeSpan.FromSeconds(3));
//                     chunkSize = await stream.ReadAsync(bytes, bytesReadSoFar, count - bytesReadSoFar, ct.Token).ConfigureAwait(false);
//                 }
//                 catch (Exception e)
//                 {
//                     //Console.WriteLine(
//                     //    $"ReadAsync failed. BytesReadOverall: {bytesReadSoFar} {e.GetType().FullName} {e.Message}, {e.InnerException?.Message}");
//                     throw new InvalidOperationException("stream.ReadAsync failed", e);
//                 }

//                 bytesReadSoFar += chunkSize;

//                 bytesRead?.Invoke(chunkSize);

//                 if (chunkSize == 0)
//                 {
//                     // the connection was closed before we received the expected number of bytes
//                     throw new InvalidOperationException(
//                         "Connection was closed before all expected bytes were received");
//                 }
//             }
//         }

//         public static async Task ReadPipe(NamedPipeServerStream stream, byte[] bytes, int count, Action<int> bytesRead)
//         {
//             int bytesReadSoFar = 0;
//             while (bytesReadSoFar < count)
//             {
//                 int chunkSize;
//                 try
//                 {
//                     // this is a pipe, so ReadAsync will return 0 only when the pipe has been closed from the other end
//                     var ct = new CancellationTokenSource(TimeSpan.FromSeconds(3));
//                     chunkSize = await stream.ReadAsync(bytes, bytesReadSoFar, count - bytesReadSoFar, ct.Token).ConfigureAwait(false);
//                     //chunkSize = stream.Read(bytes, bytesReadSoFar, count - bytesReadSoFar);
//                 }
//                 catch (Exception e)
//                 {
//                     //Console.WriteLine(
//                     //    $"ReadAsync failed. BytesReadOverall: {bytesReadSoFar} {e.GetType().FullName} {e.Message}, {e.InnerException?.Message}");
//                     throw new InvalidOperationException("stream.ReadAsync failed", e);
//                 }

//                 bytesReadSoFar += chunkSize;

//                 bytesRead?.Invoke(chunkSize);

//                 if (chunkSize == 0)
//                 {
//                     // the connection was closed before we received the expected number of bytes
//                     throw new InvalidOperationException(
//                         "Connection was closed before all expected bytes were received");
//                 }
//             }
//         }
//     }
// }