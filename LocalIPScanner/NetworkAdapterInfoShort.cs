namespace LocalIPScanner
{
    public class NetworkAdapterInfoShort : BaseNetworkInfo
    {
        //public IPAddress Ip { get; set; }
        public string Ip { get; set; } // What a pain Json.net couldn't parse System.Net.IPAddress, oh well.
        public bool InUse { get; set; }
    }
}
