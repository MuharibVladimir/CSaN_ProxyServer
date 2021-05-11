using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ConsoleApp2
{
    public delegate void OutputMessageHandler(string message);

    public struct Host
    {
        public string Ip;
        public int Port;
    }

    public class ProxyServer
    {
        private readonly IPAddress proxyIp;
        private readonly int proxyPort;
        private readonly Settings settings;
        private readonly OutputMessageHandler output;
        private const int HttpPort = 80;
        private const int BufferSize = 1024;

        public ProxyServer(string ip, int port, OutputMessageHandler Output)
        {
            proxyIp = IPAddress.Parse(ip);
            proxyPort = port;
            output = Output;
            settings = new Settings();
        }

        public void Start()
        {
            try
            {
                var tcpListener = new TcpListener(proxyIp, proxyPort);
                tcpListener.Start();

                output("Proxy is turned on...\n");

                while (true)
                {
                    var socket = tcpListener.AcceptSocket();
                    Task.Factory.StartNew(() => Listen(socket));
                }
            }
            catch
            {
                output("Proxy is turned off...\n");
            }
        }

        private void Listen(Socket client)
        {
            try
            {
                using var clientStream = new NetworkStream(client);
                string request = ReceiveMessage(clientStream);

                while (true)
                {
                    var host = DefineHost(request);

                    if (settings.BlockedSite(host.Ip))
                    {
                        byte[] wrongPageBytes = GetWrongPage();
                        clientStream.Write(wrongPageBytes, 0, wrongPageBytes.Length);

                        output($"Host: {host.Ip}; Code: 403 Forbidden.\n");

                        return;
                    }

                    using var server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    var ipHost = Dns.GetHostEntry(host.Ip);

                    var remoteEp = new IPEndPoint(ipHost.AddressList[0], host.Port);
                    server.Connect(remoteEp);

                    using var serverStream = new NetworkStream(server);
                    request = ConvertingAdress(request);
                    byte[] bytesRequest = Encoding.UTF8.GetBytes(request);
                    serverStream.Write(bytesRequest, 0, bytesRequest.Length);

                    string response = ReceiveMessage(serverStream);
                    byte[] bytesResponse = Encoding.UTF8.GetBytes(response);
                    clientStream.Write(bytesResponse, 0, bytesResponse.Length);

                    var code = ExtractResponseCode(response);
                    output($"Host: {host.Ip}; Code: {code}.\n");

                    serverStream.CopyToAsync(clientStream);

                    request = ReceiveMessage(clientStream);
                    if (string.IsNullOrEmpty(request))
                        return;
                }
            }
            finally
            {
                client.Close();
            }
        }

        private static string ReceiveMessage(NetworkStream stream)
        {
            byte[] data = new byte[BufferSize];
            var message = new StringBuilder();

            do
            {
                int size = stream.Read(data, 0, data.Length);
                message.Append(Encoding.UTF8.GetString(data, 0, size));
            } while (stream.DataAvailable);

            return message.ToString();
        }

        private static Host DefineHost(string data)
        {
            string[] dataArray = data.Split('\r', '\n');
            string hostString = dataArray.FirstOrDefault(x => x.Contains("Host"));

            if (hostString == null)
                throw new ArgumentNullException();

            int indexHost = hostString.IndexOf(" ", StringComparison.Ordinal) + 1;
            hostString = hostString.Substring(indexHost);
            string[] endPoint = hostString.Split(':');

            Host host;
            host.Ip = endPoint[0];
            host.Port = endPoint.Length == 2 ? Convert.ToInt32(endPoint[1]) : HttpPort;

            return host;
        }

        private static string ConvertingAdress(string input)
        {
            if (input == null) return null;

            const string reg = @"http:\/\/[a-z0-9а-яё\:\.]*";
            var regex = new Regex(reg);

            var matches = regex.Matches(input);
            string host = matches[0].Value;
            string result = input.Replace(host, "");

            return result;
        }

        private static string ExtractResponseCode(string data)
        {
            string[] dataArray = data.Split('\r', '\n');
            int indexCode = dataArray[0].IndexOf(" ", StringComparison.Ordinal) + 1;
            string code = dataArray[0].Substring(indexCode);

            return code;
        }

        private byte[] GetWrongPage()
        {
            using var fs = new FileStream(settings.ErrorPageAdress, FileMode.Open);

            string header = "HTTP/1.1 403 Forbidden\r\nContent-Type: text/html\r\nContent-Length: "
                            + fs.Length + "\r\n\r\n";
            byte[] pageHeader = Encoding.UTF8.GetBytes(header);

            var data = new byte[header.Length + fs.Length];
            Buffer.BlockCopy(pageHeader, 0, data, 0, pageHeader.Length);
            fs.Read(data, pageHeader.Length, (int) fs.Length);

            return data;
        }
    }
}