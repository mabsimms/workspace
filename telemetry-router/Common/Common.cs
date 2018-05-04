using System;

namespace Common
{
    public enum IpcMechanism
    {
        Pipe,
        Socket
    }

    public static class Common
    {
        public static int TelemetryItemSizeInBytes = 0;

        public static int BatchSizeInTelemetryItems = 0;

        public static int TelemetryItemsPerSecond = 0;

        public const int TelemetryItemCount = 100000000;

        public static IpcMechanism IpcMechanism;

        public static string TargetMachine;

        public static void ParseArguments(string[] args)
        {
            if (args.Length < 5)
            {
                throw new ArgumentException("Arguments not specified");
            }

            TelemetryItemSizeInBytes = ParseTelemetryItemSize(args[0]);

            BatchSizeInTelemetryItems = ParseTelemetryItemSize(args[1]);

            TelemetryItemsPerSecond = ParseTelemetryItemSize(args[2]);

            if (!Enum.TryParse(typeof(IpcMechanism), args[3], true, out object ipcMechanism))
            {
                throw new ArgumentException("Ipc mechanism is not specified");
            }

            IpcMechanism = (IpcMechanism) ipcMechanism;

            TargetMachine = args[4];
        }

        private static int ParseTelemetryItemSize(string telemetryItemSizeInput)
        {
            telemetryItemSizeInput = telemetryItemSizeInput.ToUpperInvariant();

            if (int.TryParse(telemetryItemSizeInput.Substring(0, telemetryItemSizeInput.Length - 2),
                out int itemSize))
            {
                if (telemetryItemSizeInput.EndsWith("KB"))
                {
                    return itemSize * 1024;
                }
                else if (telemetryItemSizeInput.EndsWith("MB"))
                {
                    return itemSize * 1024 * 1024;
                }
                else
                {
                    return itemSize;
                }
            }
            else
            {
                throw new ArgumentException($"Could not parse item size: {telemetryItemSizeInput}");
            }
        }
    }
}