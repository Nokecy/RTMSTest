using System;

namespace Anub.Abp.RtmpServers.CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            new RtmpServer(1935)
                .Start();

            Console.ReadLine();
        }
    }
}
