using System;
using System.Net;

namespace LocalIPScanner
{
    public class NetworkAdapter
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
        protected bool DHCPEnabled { get; set; }
        protected string DHCPServer { get; set; }
        protected DateTime DHCPLeaseExpires { get; set; }
        protected DateTime DHCPLeaseObtained { get; set; }
    }

    public class DnsInfo
    {
        protected string DnsDomain { get; set; }
        protected string DnsHostName { get; set; }
        protected IPAddress DnsServerSearchOrder { get; set; }
    }
}
