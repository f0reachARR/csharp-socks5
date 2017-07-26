using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace ProxyServer
{
    public class Socks5
    {
        public enum Command : byte
        {
            CONNECT = 0x01,
            BIND = 0x02,
            UDP_ASSOCIATE = 0x03
        }

        public enum AddressType : byte
        {
            IPV4 = 0x01,
            DNS = 0x03,
            IPV6 = 0x04
        }

        public enum ReplyType : byte
        {
            SUCCESS = 0x00,
            FAILED = 0x01,
            UNREACHED = 0x03,
            NO_SUCH_HOST = 0x04,
            CONNECT_DENIED = 0x05,
            TTL_OVER = 0x06,
            UNSUPPORTED_COMMAND = 0x07,
            UNSUPPORTED_ADDRESS = 0x08
        }

        public struct RequestDetails
        {
            public Command CmdType;
            public AddressType AddrType;
            public IPAddress Addr;
            public string DnsAddr;
            public int Port;
        }

        public struct ReplyDetails
        {
            public ReplyType Type;
            public AddressType AddrType;
            public IPAddress BindAddr;
            public int BindPort;
        }

        public static List<byte> ReadMethodSelection(Stream Input)
        {
            if (Input.ReadByte() != 0x05)
                throw new InvalidDataException("Version != 0x05");
            var MethodCount = Input.ReadByte();
            if (MethodCount < 1)
                throw new InvalidDataException("No methods");
            List<byte> Methods = new List<byte>();
            for (var i = 0; i < MethodCount; i++)
                Methods.Add((byte)Input.ReadByte());
            return Methods;
        }

        public static void SendMethodSelection(Stream Output, byte Method)
        {
            Output.Write(new byte[] { 0x05, Method }, 0, 2);
        }

        public static RequestDetails ReadRequest(Stream Input)
        {
            var Details = new RequestDetails();
            if (Input.ReadByte() != 0x05)
                throw new InvalidDataException("Version != 0x05");
            Details.CmdType = (Command)Enum.ToObject(typeof(Command), Input.ReadByte());
            Input.ReadByte(); // RSV
            Details.AddrType = (AddressType)Enum.ToObject(typeof(AddressType), Input.ReadByte());
            byte[] Addr;
            switch (Details.AddrType)
            {
                case AddressType.DNS:
                    var DnsNameLength = (int)Input.ReadByte();
                    var DnsName = new byte[DnsNameLength];
                    if (Input.Read(DnsName, 0, DnsNameLength) != DnsNameLength)
                        throw new InvalidDataException("Invalid address");
                    Details.DnsAddr = Encoding.ASCII.GetString(DnsName);
                    break;
                case AddressType.IPV4:
                    Addr = new byte[4];
                    if (Input.Read(Addr, 0, 4) != 4)
                        throw new InvalidDataException("Invalid IPv4 address");
                    Details.Addr = new IPAddress(Addr);
                    break;
                case AddressType.IPV6:
                    Addr = new byte[16];
                    if (Input.Read(Addr, 0, 16) != 16)
                        throw new InvalidDataException("Invalid IPv6 address");
                    Details.Addr = new IPAddress(Addr);
                    break;
            }
            var Port = new byte[2];
            if (Input.Read(Port, 0, 2) != 2)
                throw new InvalidDataException("Invalid port");
            Details.Port = Port[0] << 8 | Port[1];
            return Details;
        }

        public static void SendReply(Stream Output, ReplyDetails Reply)
        {
            Output.WriteByte(0x05);
            Output.WriteByte((byte)Reply.Type);
            Output.WriteByte(0x00);
            Output.WriteByte((byte)Reply.AddrType);
            var Addr = Reply.BindAddr.GetAddressBytes();
            Output.Write(Addr, 0, Addr.Length);
            Output.Write(new byte[] { (byte)(Reply.BindPort >> 8), (byte)(Reply.BindPort & 0xFF) }, 0, 2);
        }
    }
}
