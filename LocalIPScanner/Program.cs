using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace LocalIPScanner
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await new Program().BeginScanning();
        }

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
            Console.WriteLine("Please select the adapter you wish to modify/revert by typing the corresponding number.");
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
            for (var i = 0; i <= 255; i++)
            {
                if (!IPAddress.TryParse($"{baseIp}{i}", out var builtIp))
                {
                    Console.WriteLine($"Failed to parse address {baseIp}{i}");
                }
            }
            await GetNicInformation(possibleAddresses);
        }

        private async Task GetNicInformation(List<IPAddress> possibleAddresses)
        {
            foreach (var ip in possibleAddresses)
            {
                var basicAdapterInfo = GetBasicAdapterInfo(ip);
            }
        }

        private async Task<NetworkAdapter> GetBasicAdapterInfo(IPAddress ip)
        {
            var adapter = new NetworkAdapter { Ip = ip };

            var ping = new Ping();
            var response = await ping.SendPingAsync(ip, 2000);
            adapter.InUse = response.Status == IPStatus.Success;
            ping.Dispose();

            var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_NetworkAdapterConfiguration");
            var searcherGet = searcher.Get();

            foreach (ManagementObject objMo in searcherGet)
            {
                var ipEnabled = bool.Parse(objMo["IPEnabled"].ToString() ?? throw new InvalidOperationException());
                if (!ipEnabled) continue;
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
            }

            return adapter;
        }
    }
}
