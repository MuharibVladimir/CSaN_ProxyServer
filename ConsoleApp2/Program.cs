using System;

namespace ConsoleApp2
{
    internal static class Program
    {
        public static void Main()
        {
            var proxyServer = new ProxyServer("127.0.0.1", 9998, Show);
            proxyServer.Start();
        }

        private static void Show(string message)
        {
            Console.Write(message);
        }
    }  
}
