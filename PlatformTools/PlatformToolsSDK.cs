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
        /// <summary>
        /// Tries to create an AssetAccessClient instance (using the MCUXAuthorizationStrategy) from the passed data, esp. service type. 
        /// </summary>
        /// <param name="apiDomainUri">The address of the server, on which the API's entry point/upstream service is running, e.g. "http://upstream:8080/".</param>
        /// <param name="serviceType">The type of the service in question.</param>
        /// <param name="realm">The realm/systemId, on which that service in running.</param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns>A suitable AssetAccessClient or null, if no AssetAccessClient could be created.</returns>
        public static AssetAccessClient CreateAssetAccessClient(Uri apiDomainUri, string serviceType, string realm, string username, string password)
        {
            return CreateAssetAccessClient(apiDomainUri, serviceType, 0, realm, username, password);
        }

        /// <summary>
        /// Tries to create an AssetAccessClient instance (using the MCUXAuthorizationStrategy) from the passed data, esp. service type. 
        /// </summary>
        /// <param name="apiDomainUri">The address of the server, on which the API's entry point/upstream service is running, e.g. "http://upstream:8080/".</param>
        /// <param name="serviceType">The type of the service in question.</param>
        /// <param name="serviceVersion">The version of the service in question.</param>
        /// <param name="realm">The realm/systemId, on which that service in running.</param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns>A suitable AssetAccessClient or null, if no AssetAccessClient could be created.</returns>
        public static AssetAccessClient CreateAssetAccessClient(Uri apiDomainUri, string serviceType, int serviceVersion, string realm, string username, string password)
        {
            return new AssetAccessClient(new MCUXAuthorizationConnection(apiDomainUri, username, password), serviceType, serviceVersion, realm);        
        }

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

                string effectiveRealm = null;
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
