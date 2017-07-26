using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProxyServer
{
    public class Server
    {
        private class StreamPipeState
        {
            public StreamPipeState(Stream FromStream, Stream ToStream)
            {
                this.FromStream = FromStream;
                this.ToStream = ToStream;
            }
            public async void StreamPipe()
            {
                try
                {
                    var Buffer = new byte[1024 * 1024];
                    while (true)
                    {
                        var Size = await this.FromStream.ReadAsync(Buffer, 0, Buffer.Length);
                        if (Size > 0)
                            await ToStream.WriteAsync(Buffer, 0, Size);
                        else
                            await Task.Delay(1);
                    }
                }
                catch (Exception)
                {
                    this.OnError?.Invoke(this, null);
                }
            }
            public event EventHandler OnError;
            public Stream FromStream;
            public Stream ToStream;
        }
        private TcpListener _Listener = null;
        private bool _Running = false;
        public Server(int Port)
        {
            _Listener = new TcpListener(IPAddress.Loopback, Port);
        }

        public void Start()
        {
            if (_Running) return;
            _Running = true;
            _Listener.Start();
            _Listener.BeginAcceptSocket(OnAccept, _Listener);
        }

        public void OnAccept(IAsyncResult Result)
        {
            try
            {
                var Client = _Listener.EndAcceptTcpClient(Result);
                _Listener.BeginAcceptTcpClient(OnAccept, _Listener);

                //Client.ReceiveTimeout = 1000;
                var ClientStream = Client.GetStream();
                Client.SendBufferSize = 1024 * 512;
                //Stream.ReadTimeout = 1000;

                var Methods = Socks5.ReadMethodSelection(ClientStream);
                Console.WriteLine("{0} RECV: Method Selection", Thread.CurrentThread.ManagedThreadId);
                if (!Methods.Any(b => b == 0x00))
                {
                    Socks5.SendMethodSelection(ClientStream, 0xff);
                    Client.Close();
                    return;
                }
                else
                    Socks5.SendMethodSelection(ClientStream, 0x00);
                Console.WriteLine("{0} SEND: Method Selection", Thread.CurrentThread.ManagedThreadId);
                var Request = Socks5.ReadRequest(ClientStream);
                Console.WriteLine("{0} RECV: Request", Thread.CurrentThread.ManagedThreadId);
                if (Request.AddrType == Socks5.AddressType.DNS)
                {
                    Console.WriteLine("{0} Resolve address {1}", Thread.CurrentThread.ManagedThreadId, Request.DnsAddr);
                    try
                    {
                        var Entry = Dns.GetHostEntry(Request.DnsAddr);
                        Request.Addr = Entry?.AddressList?[0];
                        Console.WriteLine("{0} Resolved {1}", Thread.CurrentThread.ManagedThreadId, Request.Addr.ToString());
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("{0} Failed to resolve {1}", Thread.CurrentThread.ManagedThreadId, Request.DnsAddr);
                        Client.Close();
                        return;
                    }
                }

                var Conn = new TcpClient();
                Conn.Connect(new IPEndPoint(Request.Addr, Request.Port));

                Console.WriteLine("{0} Connected", Thread.CurrentThread.ManagedThreadId);
                var Reply = new Socks5.ReplyDetails();
                Reply.AddrType = Socks5.AddressType.IPV4;
                Reply.BindAddr = IPAddress.Loopback;
                Reply.Type = Socks5.ReplyType.SUCCESS;
                Reply.BindPort = 1234;
                Socks5.SendReply(ClientStream, Reply);

                var ConnStream = Conn.GetStream();
                ConnStream.ReadTimeout = 2000;
                Conn.ReceiveBufferSize = 1024 * 1024;
                var StopHandler = new EventHandler((a, b) =>
                 {
                     Client.Close();
                     Conn.Close();
                     Console.WriteLine("Closed");
                 });

                // TODO
                var InfoSv = new StreamPipeState(ConnStream, ClientStream);
                InfoSv.OnError += StopHandler;
                InfoSv.StreamPipe();
                var InfoCl = new StreamPipeState(ClientStream, ConnStream);
                InfoCl.OnError += StopHandler;
                InfoCl.StreamPipe();
            }
            catch (Exception) { }
        }


        public void Stop()
        {
            if (_Running)
                _Listener.Stop();
            _Running = false;
        }
    }
}
