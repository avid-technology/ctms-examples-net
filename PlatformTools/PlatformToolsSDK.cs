using Avid.Platform.SDK;
using Avid.Platform.SDK.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
