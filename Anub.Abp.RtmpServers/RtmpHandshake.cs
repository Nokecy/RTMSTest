using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Anub.Abp.RtmpServers
{
    public static class RtmpHandshake
    {
        public static byte[] GetS01Async()
        {
            var random = new Random(Environment.TickCount);
            var randomBytes = new byte[1528];
            random.NextBytes(randomBytes);
            var Version = 3;
            var Time = (uint)Environment.TickCount;
            var Time2 = 0;

            using (var memoryStream = new MemoryStream(1536))
            using (var writer = new StreamWriter(memoryStream, Encoding.UTF8))
            {
                writer.AutoFlush = true;
                writer.Write(Version);
                writer.Write(Time);
                writer.Write(Time2);
                writer.Write(randomBytes);
                return memoryStream.GetBuffer();
            }
        }

        public static byte[] GetS2Async()
        {
            var random = new Random(Environment.TickCount);
            var randomBytes = new byte[1528];
            random.NextBytes(randomBytes);

            var Time = (uint)Environment.TickCount;
            var Time2 = 0;

            using (var memoryStream = new MemoryStream(1536))
            using (var writer = new StreamWriter(memoryStream, Encoding.UTF8))
            {
                writer.AutoFlush = true;
                writer.Write(Time);
                writer.Write(Time2);
                writer.Write(randomBytes);
                return memoryStream.GetBuffer();
            }
        }
    }
}
