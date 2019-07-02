using Avid.Platform.SDK;
using Avid.Platform.SDK.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebApi.Hal;

namespace PlatformTools
{
    public static class PlatformToolsSDK
    {
        public static T FindInRegistry<T>(CtmsRegistryClient registryClient, string serviceType, string realm, string resourceName)
            where T : Representation
        {
            T searchesResource = default(T);
            if (registryClient.GetRegistryInfo().Resources.ContainsKey(resourceName))
            {
                var candidateSystemIds
                    = registryClient.GetRegistryInfo().Resources[resourceName]
                        .SelectMany(it => it.Systems)
                        .Select(it => it.SystemId);

                string effectiveRealm = realm;
                if (!candidateSystemIds.Contains(realm))
                {
                    effectiveRealm = candidateSystemIds.FirstOrDefault();
                    Console.WriteLine($"'{resourceName}' was not available on realm {realm}. Falling back to {effectiveRealm}.");

                }

                searchesResource = (null != effectiveRealm)
                                    ? registryClient.GetResourceForSystem<T>(resourceName, GetSystemType(serviceType), effectiveRealm)
                                    : default(T);
            }

            return searchesResource;
        }

        public static string GetSystemType(string serviceType)
        {
            if (AssetAccessClient.InterplayProductionServiceName.Equals(serviceType))
            {
                return "interplay-pam";
            }
            else if (AssetAccessClient.MAMServiceName.Equals(serviceType) || "avid.orchestration.ctc".Equals(serviceType))
            {
                return "interplay-mam";
            }

            throw new ArgumentException($"unknown serviceType '{serviceType}'", nameof(serviceType));
        }
    }
}
