using System;
using System.Net;

namespace LocalIPScanner
{
    public class NetworkAdapterInfo
    {
        public IPAddress Ip { get; set; }
        public int Index { get; set; }
        public int InterfaceIndex { get; set; }
        public string Caption { get; set; }
        public string Description { get; set; }
        public bool InUse { get; set; }
        public DhcpInfo DhcpInfo { get; set; }
        public DnsInfo DnsInfo { get; set; }
    }

    public class DhcpInfo
    {
        public DhcpInfo(bool enabled, string server, DateTime expires, DateTime obtained)
        {
            DhcpEnabled = enabled;
            DhcpServer = server;
            DhcpLeaseExpires = expires;
            DhcpLeaseObtained = obtained;
        }
        public bool DhcpEnabled { get; private set; }
        public string DhcpServer { get; private set; }
        public DateTime DhcpLeaseExpires { get; private set; }
        public DateTime DhcpLeaseObtained { get; private set; }
    }

    public class DnsInfo
    {
        public DnsInfo(string domain, string host, IPAddress[] searchOrder)
        {
            DnsDomain = domain;
            DnsHostName = host;
            DnsServerSearchOrder = searchOrder;
        }
        public string DnsDomain { get; private set; }
        public string DnsHostName { get; private set; }
        public IPAddress[] DnsServerSearchOrder { get; private set; }
    }
}
