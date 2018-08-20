//
// Copyright 2016 by Avid Technology, Inc.
//

using Avid.Platform.SDK;
using Avid.Platform.SDK.Authorization;
using Avid.Platform.SDK.Hal;
using Avid.Platform.SDK.Model.Orchestration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tavis.UriTemplates;
using WebApi.Hal;

namespace OrchestrationStartProcessSDK
{
    /// <summary>
    /// This example starts a process and monitors its progress.
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            if (5 != args.Length)
            {
                Console.WriteLine($"Usage: {System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name} <apidomain> <realm> <oauth2token> <username> <password>");
            }
            else
            {
                string apiDomain = args[0];
                string realm = args[1];
                string oauth2token = args[2];
                string username = args[3];
                string password = args[4];

                Uri upstreamServerUrl = new Uri($"https://{apiDomain}");
                using (CtmsRegistryClient registryClient = new CtmsRegistryClient(new OAuth2AuthorizationConnection(upstreamServerUrl, oauth2token, username, password)))
                {
                    const string registeredLinkRelOrchestrationRoot = "orchestration:orchestration";
                    const string orchestrationServiceType = "avid.orchestration.ctc";
                    OrchestrationRoot orchestrationRootResource = PlatformTools.PlatformToolsSDK.FindInRegistry<OrchestrationRoot>(registryClient, orchestrationServiceType, realm, registeredLinkRelOrchestrationRoot);

                    if (null != orchestrationRootResource)
                    {
                        const string registeredLinkRelOrchestrationProcessQuery = "orchestration:start-process";
                        Link orchestrationProcessQueryLink = orchestrationRootResource.DiscoverLink(registeredLinkRelOrchestrationProcessQuery);
                        if (null != orchestrationProcessQueryLink)
                        {
                            Tavis.UriTemplates.UriTemplate orchestrationProcessQueryUriTemplate = new Tavis.UriTemplates.UriTemplate(orchestrationProcessQueryLink.Href);
                            orchestrationProcessQueryUriTemplate.SetParameter("offset", 0);
                            orchestrationProcessQueryUriTemplate.SetParameter("limit", 50);

                            string orchestrationProcessQueryUri = orchestrationProcessQueryUriTemplate.Resolve();
                            
                            /// Create and start an export process with attachments:
                            string now = DateTime.Now.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffK");
                            const string itemToExport = "2016050410152760101291561460050569B02260000003692B00000D0D000005";
                            string newProcessName = string.Format("New process as to {0}", DateTime.Now).Replace(" ", "_").Replace(":", "_").Replace("-", "_");
                            string newProcessId = Guid.NewGuid().ToString();
                            JObject processDescription
                                = new JObject(
                                    new JProperty("base",
                                        new JObject(
                                            new JProperty("id", newProcessId)
                                            , new JProperty("type", "MAM_EXPORT_FILE")
                                            , new JProperty("systemType", "interplay-mam")
                                            , new JProperty("systemID", realm)
                                        )
                                    )
                                    , new JProperty("common",
                                        new JObject(
                                            new JProperty("name", newProcessName)
                                            , new JProperty("creator", ".NET_Example")
                                            , new JProperty("created", now)
                                            , new JProperty("modifier", "Service-WorkflowEngine")
                                            , new JProperty("modified", now)
                                        )
                                    )
                                    , new JProperty("attachments",
                                        new JArray(
                                            new JObject(
                                                new JProperty("base",
                                                    new JObject(
                                                        new JProperty("id", itemToExport)
                                                        , new JProperty("type", "Asset")
                                                        , new JProperty("systemType", "interplay-mam")
                                                        , new JProperty("systemID", realm)
                                                    )
                                                )
                                            )
                                        )
                                    )
                                );

                            Process process = registryClient.SendHal<Process>(HttpMethod.Post, new Uri(orchestrationProcessQueryUri), processDescription);
                            Console.WriteLine("Process: '{0}' - start initiated", newProcessName);
                            Console.WriteLine("Lifecycle: {0}", process.LifeCycle);
                            /// Monitor the running process:                                
                            while ("running".Equals(process.LifeCycle) || "pending".Equals(process.LifeCycle))
                            {
                                Thread.Sleep(500);
                                // Directly get the process instance via its id:
                                const string registeredLinkRelOrchestrationGetProcess = "orchestration:process-by-id";
                                Link orchestrationGetProcessLink = orchestrationRootResource.DiscoverLink(registeredLinkRelOrchestrationGetProcess);
                                Tavis.UriTemplates.UriTemplate orchestrationGetProcessUriTemplate = new Tavis.UriTemplates.UriTemplate(orchestrationGetProcessLink.Href);
                                orchestrationGetProcessUriTemplate.SetParameter("id", newProcessId);
                                process = registryClient.GetHalResource<Process>(new Uri(orchestrationGetProcessUriTemplate.Resolve()));
                                Console.WriteLine("Lifecycle: {0}", process.LifeCycle);
                            }
                        }
                    }
                }
                Console.WriteLine("End");
            }
        }
    }
}
