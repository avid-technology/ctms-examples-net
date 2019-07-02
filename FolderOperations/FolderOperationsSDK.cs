using Avid.Platform.SDK;
using Avid.Platform.SDK.Authorization;
using Avid.Platform.SDK.Model.Location;
using System;

namespace FolderOperations
{
    public class FolderOperationsSDK
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Currently not supported!");
            if (6 != args.Length)
            {
                Console.WriteLine($"Usage: {System.Reflection.Assembly.GetEntryAssembly().ManifestModule.Name} <apidomain> <httpbasicauthstring> <servicetype> <realm>");
            }
            else
            {
//                string apiDomain = args[0];
//                string serviceType = args[1];
//                string realm = args[2];
//                string username = args[3];
//                string password = args[4];

//                Uri upstreamServerUrl = new Uri(string.Format("https://{0}", apiDomain));
//                AssetAccessClient assetAccessClient
//                    = PlatformTools.PlatformToolsSDK.CreateAssetAccessClient(upstreamServerUrl, serviceType
//                        , realm
//                        , username
//                        , password
//);

//                if (null != assetAccessClient)
//                {
//                    using (assetAccessClient)
//                    {
//                        Item actuallyCreatedFolder = null;
//                        try
//                        {
//                            string now = DateTime.Now.ToString("MM_dd_yy_H_mm_ss_fff");
//                            string newFolderName = "CTMS_SDK_Example_Folder_" + now;

//                            Item folderToCreate = new Item { Common = { Name = newFolderName } };
//                            // Create the folder at the root folder:
//                            actuallyCreatedFolder = assetAccessClient.CreateFolder("1", folderToCreate);

//                            // Rename the created folder:
//                            actuallyCreatedFolder.Common.Name += "_REN";
//                            Item renamedFolder = assetAccessClient.UpdateFolder(actuallyCreatedFolder.Base.Id, actuallyCreatedFolder);

//                            Item gottenFolder = assetAccessClient.GetItem(new Uri(renamedFolder.Href));
//                        }
//                        finally
//                        {
//                            // Delete the created folder:
//                            bool folderDeletionWasSuccessful = assetAccessClient.DeleteFolder(actuallyCreatedFolder.Base.Id);
//                        }
//                    } 
//                }
//                else
//                {
//                    Console.WriteLine("Couldn't create AssetAccessClient for serviceType: '{0}', realm: '{1}', username: '{2}', upstreamServerUrl: <{3}>"
//                        , serviceType, realm, username, upstreamServerUrl);
//                }
                Console.WriteLine("End");
            }
        }       
    }
}
