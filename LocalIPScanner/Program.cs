using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LocalIPScanner
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await new Program().BeginScanning();
        }

        private Stopwatch _sw = new Stopwatch();

        public async Task BeginScanning()
        {
            Console.WriteLine("Select your active/desired NIC");

            var nics = new Dictionary<int, NetworkInterface>();
            var allNics = NetworkInterface.GetAllNetworkInterfaces();
            for (var i = 0; i < allNics.Length; i++)
            {
                var nic = allNics[i];
                nics.Add(i, nic);
            }
            Console.WriteLine("Please select the adapter you wish to use as the base of the scan by typing the corresponding number.");
            foreach (var possibleNic in nics)
            {
                Console.WriteLine($"{possibleNic.Key}: {possibleNic.Value.Name}");
            }

            NetworkInterface selectedNic;
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("> ");
                Console.ResetColor();
                if (!int.TryParse(Console.ReadLine(), out var nicInput) || !nics.ContainsKey(nicInput))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Invalid input!");
                    Console.ResetColor();
                    continue;
                }
                selectedNic = nics[nicInput];
                break;
            }

            Console.WriteLine("\nGetting NIC local ip");
            IPAddress nicIpv4 = default;
            foreach (var ip in selectedNic.GetIPProperties().UnicastAddresses)
            {
                if (ip.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                nicIpv4 = ip.Address;
            }

            if (nicIpv4 == null || nicIpv4.Equals(default))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Nic ipv4 failed to parse.");
                Console.ResetColor();
                Environment.Exit(-1);
            }

            Console.WriteLine($"Nic IpV4: {nicIpv4}\n");

            await GetAllDhcpAddresses(nicIpv4);
        }

        private async Task GetAllDhcpAddresses(IPAddress localIpv4)
        {
            Console.WriteLine("Building possible DHCP addresses");
            var baseIpSplit = localIpv4.ToString().Split('.').Take(3).ToList();
            var baseIp = $"{baseIpSplit[0]}.{baseIpSplit[1]}.{baseIpSplit[2]}.";
            Console.WriteLine($"Base address: {baseIp}");
            var possibleAddresses = new List<IPAddress>();
            for (var i = 1; i <= 255; i++)
            {
                if (!IPAddress.TryParse($"{baseIp}{i}", out var builtIp))
                {
                    Console.WriteLine($"Failed to parse address {baseIp}{i}");
                }
                possibleAddresses.Add(builtIp);
            }
            Console.WriteLine("Possible DHCP addresses built\n");
            var jsonOutput = await GetNicInformation(possibleAddresses);
            OutputInformation(jsonOutput);

        }

        private async Task<string> GetNicInformation(IEnumerable<IPAddress> possibleAddresses)
        {
            Console.WriteLine("Scanning and compiling network information...");
            _sw.Start();
            var compiledNetworkInfo = await Task.WhenAll(possibleAddresses.Select(GetBasicAdapterInfo).ToArray());
            _sw.Stop();
            Console.WriteLine($"All network information compiled. Count: {compiledNetworkInfo.Length} Elapsed Time: {_sw.ElapsedMilliseconds}ms");
            _sw.Reset();
            return JsonConvert.SerializeObject(compiledNetworkInfo, Formatting.Indented);
        }

        private async Task<NetworkAdapterInfo> GetBasicAdapterInfo(IPAddress ip)
        {
            var adapter = new NetworkAdapterInfo { Ip = ip.ToString() };
            var ping = new Ping();
            var response = await ping.SendPingAsync(ip, 400);
            adapter.InUse = response.Status == IPStatus.Success;
            ping.Dispose();

            var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_NetworkAdapterConfiguration");
            var searcherGet = searcher.Get();

            foreach (ManagementObject objMo in searcherGet)
            {
                if (!bool.Parse(objMo["IPEnabled"].ToString()))
                    continue;
                if (!objMo["IPAddress"].ToString().Contains(ip.ToString())) continue;

                var objIndex = objMo["Index"].ToString();
                if (!int.TryParse(objIndex, out var index))
                {
                    Console.WriteLine($"Failed to parse index for ip {ip}");
                    continue;
                }
                adapter.Index = index;

                var objInterfaceIndex = objMo["InterfaceIndex"].ToString();
                if (!int.TryParse(objInterfaceIndex, out var interfaceIndex))
                {
                    Console.WriteLine($"Failed to parse InterfaceIndex for ip {ip}");
                    continue;
                }
                adapter.InterfaceIndex = interfaceIndex;

                adapter.Caption = objMo["Caption"].ToString();
                adapter.Description = objMo["Description"].ToString();
                SetDhcpInfo(adapter, ip);
                SetDnsInfo(adapter, ip);
                return adapter;
            }
            return adapter;
        }

        private void SetDhcpInfo(NetworkAdapterInfo adapter, IPAddress ip)
        {
            var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_NetworkAdapterConfiguration");
            var searcherGet = searcher.Get();

            foreach (ManagementObject objMo in searcherGet)
            {
                if (!bool.Parse(objMo["IPEnabled"].ToString()))
                    continue;
                if (!objMo["IPAddress"].ToString().Contains(ip.ToString())) continue;

                var enabled = bool.Parse(objMo["DHCPEnabled"].ToString());
                var server = objMo["DHCPServer"].ToString();
                var leaseExpires = ManagementDateTimeConverter.ToDateTime(objMo["DHCPLeaseExpires"].ToString());
                var leaseObtained = ManagementDateTimeConverter.ToDateTime(objMo["DHCPLeaseObtained"].ToString());
                adapter.DhcpInfo = new DhcpInfo(enabled, server, leaseExpires, leaseObtained);
                break;
            }
        }

        private void SetDnsInfo(NetworkAdapterInfo adapter, IPAddress ip)
        {
            var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_NetworkAdapterConfiguration");
            var searcherGet = searcher.Get();

            foreach (ManagementObject objMo in searcherGet)
            {
                if (!bool.Parse(objMo["IPEnabled"].ToString()))
                    continue;
                if (!objMo["IPAddress"].ToString().Contains(ip.ToString())) continue;

                var domain = objMo["DNSDomain"].ToString();
                var host = objMo["DNSHostName"].ToString();
                var searchOrder = (string[])objMo["DNSServerSearchOrder"];
                var ipOne = IPAddress.Parse(searchOrder[0]);
                var ipTwo = IPAddress.Parse(searchOrder[1]);
                adapter.DnsInfo = new DnsInfo(domain, host, new[] { ipOne, ipTwo });
                break;
            }
        }

        private void OutputInformation(string jsonOutput)
        {
            Console.WriteLine("Output to text? y/n");
            var input = Console.ReadLine().ToLowerInvariant();
            while (true)
            {
                if (input.Equals("y"))
                {
                    var date = DateTimeOffset.Now.ToString("s");
                    date = date.Replace(':', '.');
                    var path = $@"NIOutput{date}.json";
                    File.WriteAllText(path, jsonOutput);
                    Console.WriteLine($@"Outputted json result to {Environment.CurrentDirectory}\{path}");
                    Console.WriteLine("Press any key to exit.");
                    Console.ReadKey();
                    Console.WriteLine("Exiting in 3 seconds...");
                    Thread.Sleep(3000);
                    Environment.Exit(-1);
                    break;
                }

                if (input.Equals("n"))
                {
                    Console.WriteLine(jsonOutput);
                    Console.WriteLine("Press any key to exit.");
                    Console.ReadKey();
                    Console.WriteLine("Exiting in 3 seconds...");
                    Thread.Sleep(3000);
                    Environment.Exit(-1);
                }

                Console.WriteLine("Invalid input. Please enter y or n");
                Thread.Sleep(1);
            }
        }
    }
}
