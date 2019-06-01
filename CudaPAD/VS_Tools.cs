using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Setup.Configuration;
using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Win32;

namespace CudaPAD
{
    static class VS_Tools
    {
        public static string GetVSPath(string specificVersion = "", bool avoidPrereleases = true, string requiredWorkload = "")
        {
            string vsPath = "";
            // Method 1 - use "Microsoft.VisualStudio.Setup.Configuration.SetupConfiguration" method.

            // Note: This code has is a heavily modified version of Heath Stewart's code.
            // original source: (Heath Stewart, May 2016) https://github.com/microsoft/vs-setup-samples/blob/80426ad4ba10b7901c69ac0fc914317eb65deabf/Setup.Configuration.CS/Program.cs 
            try
            {
                var e = new SetupConfiguration().EnumAllInstances();

                int fetched;
                var instances = new ISetupInstance[1];
                do
                {
                    e.Next(1, instances, out fetched);
                    if (fetched > 0)
                    {
                        var instance2 = (ISetupInstance2)instances[0];
                        var state = instance2.GetState();

                        // Lets make sure this install is complete.
                        if (state != InstanceState.Complete)
                            continue;

                        // If we have a version to match lets make sure to match it.
                        if (!string.IsNullOrWhiteSpace(specificVersion))
                            if (!instances[0].GetInstallationVersion().StartsWith(specificVersion))
                                continue;

                        // If instances[0] is null then skip
                        var catalog = instances[0] as ISetupInstanceCatalog;
                        if (catalog == null)
                            continue;

                        // If there is not installation path lets skip
                        if ((state & InstanceState.Local) != InstanceState.Local)
                            continue;

                        // Lets make sure it has the required workload - if one was given.
                        if (!string.IsNullOrWhiteSpace(requiredWorkload))
                        {
                            if ((state & InstanceState.Registered) == InstanceState.Registered)
                            {
                                if (!(from package in instance2.GetPackages()
                                      where string.Equals(package.GetType(), "Workload", StringComparison.OrdinalIgnoreCase)
                                      where package.GetId().Contains(requiredWorkload)
                                      orderby package.GetId()
                                      select package).Any())
                                {
                                    continue;
                                }
                            }
                            else
                            {
                                continue;
                            }
                        }

                        // Lets save the installation path and make sure it has a value.
                        vsPath = instance2.GetInstallationPath();
                        if (string.IsNullOrWhiteSpace(vsPath))
                            continue;

                        // If specified, avoid Pre-release if possible 
                        if (avoidPrereleases && catalog.IsPrerelease())
                            continue;

                        // We found the one we need - lets get out of here
                        return vsPath;
                    }
                }
                while (fetched > 0);
            }
            catch (Exception){ }

            if (string.IsNullOrWhiteSpace(vsPath))
                return vsPath;

            // Method 2 - Find the location of visual studio (%VS90COMNTOOLS%\..\..\vc\vcvarsall.bat)
            // Note: This code has is a heavily modified version of Kevin Kibler's code.            
            // source: (Kevin Kibler, 2014) http://stackoverflow.com/questions/30504/programmatically-retrieve-visual-studio-install-directory 
            List<Version> vsVersions = new List<Version>() { new Version("15.0"), new Version("14.0"),
                new Version("13.0"), new Version("12.0"), new Version("11.0") };
            foreach (var version in vsVersions)
            {
                foreach (var isExpress in new bool[] { false, true })
                {
                    RegistryKey registryBase32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                    RegistryKey vsVersionRegistryKey = registryBase32.OpenSubKey(
                        string.Format(@"{0}\{1}.{2}",
                        (isExpress) ? @"SOFTWARE\Microsoft\VCSExpress" : @"SOFTWARE\Microsoft\VisualStudio",
                        version.Major, version.Minor));
                    if (vsVersionRegistryKey == null) { continue; }
                    string path = vsVersionRegistryKey.GetValue("InstallDir", string.Empty).ToString();
                    if (!string.IsNullOrEmpty(path))
                    {
                        path = Directory.GetParent(path).Parent.Parent.FullName;
                        if (File.Exists(path + @"\VC\bin\cl.exe") && File.Exists(path + @"\VC\vcvarsall.bat"))
                        {
                            vsPath = path;
                            break;
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(vsPath))
                    break;
            }
            return vsPath;
        }


        public static string GetVSPathInfo()
        {
            string info = "";

            // This is a slightly modified version of Heath Stewart's code.
            // source: (Heath Stewart, May 2016) https://github.com/microsoft/vs-setup-samples/blob/80426ad4ba10b7901c69ac0fc914317eb65deabf/Setup.Configuration.CS/Program.cs 
            try
            {
                var query = new SetupConfiguration();
                var query2 = (ISetupConfiguration2)query;
                var e = query2.EnumAllInstances();

                var helper = (ISetupHelper)query;

                int fetched;
                var instances = new ISetupInstance[1];
                do
                {
                    e.Next(1, instances, out fetched);
                    if (fetched > 0)
                    {
                        var instance2 = (ISetupInstance2)instances[0];
                        var state = instance2.GetState();
                        info += $"InstanceId: {instance2.GetInstanceId()} ({(state == InstanceState.Complete ? "Complete" : "Incomplete")})\r\n";

                        var installationVersion = instances[0].GetInstallationVersion();
                        var version = helper.ParseVersion(installationVersion);

                        info += $"InstallationVersion: {installationVersion} ({version})\r\n";

                        if ((state & InstanceState.Local) == InstanceState.Local)
                        {
                            info += $"InstallationPath: {instance2.GetInstallationPath()}\r\n";
                        }

                        var catalog = instances[0] as ISetupInstanceCatalog;
                        if (catalog != null)
                        {
                            info += $"IsPrerelease: {catalog.IsPrerelease()}\r\n";
                        }

                        if ((state & InstanceState.Registered) == InstanceState.Registered)
                        {
                            info += $"Product: {instance2.GetProduct().GetId()}\r\n" + "Workloads:";
                            var workloads = from package in instance2.GetPackages()
                                            where string.Equals(package.GetType(), "Workload", StringComparison.OrdinalIgnoreCase)
                                            orderby package.GetId()
                                            select package;

                            foreach (var workload in workloads)
                            {
                                info += $"    {workload.GetId()}\r\n";
                            }
                        }

                        var catalogProps1 = instance2.GetProperties();
                        if (catalogProps1 != null)
                        {
                            info += "Custom properties:\r\n";
                            var catalogNames1 = from name in catalogProps1.GetNames()
                                                orderby name
                                                select new { Name = name, Value = catalogProps1.GetValue(name) };

                            foreach (var prop in catalogNames1)
                            {
                                info += $"    {prop.Name}: {prop.Value}\r\n";
                            }
                        }

                        var catalogProps2 = catalog?.GetCatalogInfo();
                        if (catalogProps2 != null)
                        {
                            Console.WriteLine("Catalog properties:");
                            var catalogNames2 = from name in catalogProps2.GetNames()
                                                orderby name
                                                select new { Name = name, Value = catalogProps2.GetValue(name) };

                            foreach (var prop in catalogNames2)
                            {
                                info += $"    {prop.Name}: {prop.Value}\r\n";
                            }
                        }
                    }
                }
                while (fetched > 0);

            }
            catch (COMException ex) when (Marshal.GetHRForException(ex) == unchecked((int)0x80040154)) // check if REGDB_E_CLASSNOTREG
            {
                info = "The query API is not registered. Assuming no instances are installed.\r\n"; ;
            }
            catch (Exception ex)
            {
                info = $"Error 0x{Marshal.GetHRForException(ex):x8}: {ex.Message}\r\n";
            }
            return info;
        }
    }
}
