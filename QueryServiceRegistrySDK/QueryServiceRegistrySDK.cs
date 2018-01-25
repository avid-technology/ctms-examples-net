  using Avid.Platform.SDK;
using Avid.Platform.SDK.Authorization;
using Avid.Platform.SDK.Model.CtmsRegistry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QueryServiceRegistrySDK
{
    public class QueryServiceRegistrySDK
    {
        /// <summary>
        /// This example enumerates the entries in the service registry and writes the results to stdout using the registry.
        /// </summary>
        static void Main(string[] args)
        {
            if (3 != args.Length)
            {
                Console.WriteLine($"Usage: {System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name} <apidomain> <username> <password>");
            }
            else
            {
                string apiDomain = args[0];
                string username = args[1];
                string password = args[2];

                Uri upstreamServerUrl = new Uri($"https://{apiDomain}");
                using (CtmsRegistryClient registryClient = new CtmsRegistryClient(new MCUXAuthorizationConnection(upstreamServerUrl, username, password)))
                {
                    CtmsRegistryInfo registryInfo = registryClient.GetRegistryInfo();
                    StringBuilder sb = new StringBuilder();
                    foreach (KeyValuePair<string, CtmsResourceInfo[]> resourceInfo in registryInfo.Resources)
                    {
                        sb.AppendLine($"Resource name: '{resourceInfo.Key}'");
                        int index = 0;
                        foreach (CtmsResourceInfo resourceInfoSpecificSystem in resourceInfo.Value)
                        {
                            ++index;
                            sb.AppendLine($"\t {index}. <{resourceInfoSpecificSystem.Href}>")
                                .AppendLine($"\t\t Description: {resourceInfoSpecificSystem.Description}");

                            if (null != resourceInfoSpecificSystem.Properties)
                            {
                                sb.AppendLine($"\t\t Properties:");

                                foreach (var item in resourceInfoSpecificSystem.Properties)
                                {
                                    sb.AppendLine($"\t\tName: {item.Key}, Value: {item.Value}");
                                }
                            }
                                
                            if (null != resourceInfoSpecificSystem.TemplateParameters)
                            {
                                sb.AppendLine($"\t\t TemplateParameters:");
                                foreach (var item in resourceInfoSpecificSystem.TemplateParameters)
                                {
                                    sb.AppendLine($"\t\t\tName: {item.Key}, Type: {item.Value?.Type}, Description: {item.Value?.Description}");
                                }
                            }
                        }
                    }

                    Console.WriteLine(sb);
                    Console.WriteLine("End");
                }
            }
        }
    }
}
