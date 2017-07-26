using ProxyServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Socks5Server
{
    class Program
    {
        static void Main(string[] args)
        {
            var Sv = new Server(1000);
            Sv.Start();
            Console.ReadLine();
            Sv.Stop();
        }
    }
}
