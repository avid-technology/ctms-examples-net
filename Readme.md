# Please Read Me #
* Implementation:
    * The examples are implemented with C# 6/.NET 4.6.2 (target framework) and Visual Studio 2015.
    * There are two categories of examples: those using the PlatformSDK (projects to be identified with the suffix "SDK") and those using REST and HATEOAS.
    * The example SimpleSearchAsync shows, how asynchronous HTTP requests can be applied to use CTMS.
	* The non-PlatformSDK examples are configured to use a request timeout of 60s each.
	* There are some error checks but those are very general and might not cover all cases. Esp. timeouts or unreachable endpoints could happen at any time during the application of REST and HATEOAS.
    * No optimization and no parallelization (e.g. for requesting results from the platform) was implemented.
        * Esp. the examples use HATEOAS to get all links. Instead of HATEOAS all links could be used hard coded or being "bookmarked" without HATEOAS (resulting in faster execution time), but this is not the idea behind RESTful interfaces. Also mind, that those links could change in future so the only save way is to get the via HATEOAS. The examples do only use these URLs directly: https://$apidomain/auth, https://$apidomain/api/middleware/service/ping, https://$apidomain/apis/$servicetype;version=0;realm=$realm/locations and https://$apidomain/apis/$servicetype;version=0;realm=$realm/searches other URLs are resolved via HATEOAS!
    * For testing purposes, it was required to configure the WebRequestHandlers to accept arbitrary SSL certificates. Please notice, that this may not be acceptable for productive code.

* Dependencies:
    * The projects using SDK examples make use of PlatformSDK.dll. This assembly, as well as a belonging to config file, reside in the folder lib.
    * additionally, these 3rd-party libraries are used: Json.NET Newtonsoft.Json.10.0.3, Tavis.UriTemplates 1.1.0 and WebApi.Hal 2.6.0 
    * Each project in the solution brings its own packages.config file, in which the required NuGet packages are listed.
    * The Visual Studio solution is self contained, dependent libraries can be resolved via NuGet:
    * The installation of required NuGet packages runs automatically, when the solution is opened. If that didn't happen, the packages can be reinstalled manually:
        * For the following steps a "NuGet Package Manager Console Host" of version >= 2.8.60723.765 is required.
        * Windows PowerShell script execution must be enabled: issue "set-executionpolicy remotesigned" in an admin PowerShell console.
        * Open Tools>NuGet Package Manager>Package Manager Console:
            * Make sure, that the Package Manager Console is configured to have the "Package source" set to "All".
            * Issue the command "update-package -reinstall".

* Running the examples:
    * => When running the executables on a terminal, make sure you have specified correct command line arguments: __ExampleApplicationName__ _apidomain_ _[servicetype]_ _[serviceversion]_ _[realm]_ _username_ _password_ _['searchexpression']_ _[advancedsearchdescriptionfilename]_
    * The SimpleSearch/SimpleSearchAsync examples await the searchexpression in single quotes as last argument:
        * SimpleSearch.exe _apidomain_ _servicetype_ _realm_ _username_ _password_ '_searchexpression_'
        * Example: SimpleSearch upstream avid.mam.assets.access BEEF Administrator ABRAXAS '*'
		* SimpleSearchAsync.exe _apidomain_ _servicetype_ _realm_ _username_ _password_ '_searchexpression_'
        * Example: SimpleSearchAsync upstream avid.mam.assets.access BEEF Administrator ABRAXAS '*'
    * The AdvancedSearch example awaits the file name of a file containing the advanced search description as last argument:
        * AdvancedSearch.exe _apidomain_ _servicetype_ _realm_ _username_ _password_ _advancedsearchdescriptionfilename_
        * Example: AdvancedSearch upstream avid.mam.assets.access BEEF Administrator ABRAXAS Resources\MAMAdvancedSearchDescription.txt
    * The SimpleSearchSDK and OrchestrationXXXSDK examples awaits the searchexpression in single quotes as last argument:
        * SimpleSearchSDK|OrchestrationXXXSDK _apidomain_ _servicetype_ _realm_ _username_ _password_ '_searchexpression_'
        * Example: SimpleSearchSDK upstream BEEF Administrator ABRAXAS '*'
    * The QueryServiceRegistry example needs no servicetype (always "avid.ctms.registry") and no realm (always "global"/"") argument.
        * QueryServiceRegistry.exe _apidomain_ _username_ _password_
        * Example: QueryServiceRegistry upstream Administrator Avid123
    * Optionally, e.g. for debugging purposes, the assembly can be started with the app.config being configured for a proxy server:
     ```
    <configuration>
        <!-- ... -->

        <system.net>
            <defaultProxy enabled = "true" useDefaultCredentials = "true">
                <proxy autoDetect="false" bypassonlocal="false" proxyaddress="http://127.0.0.1:8888" usesystemdefault="false" />
            </defaultProxy>
        </system.net>
    </configuration>
     ```

     * Notice, that using a proxy can reduce the performance of HTTP requests.
     * Notice also, that having set proxy options as shown above while *no proxy* is configured can reduce the performance of HTTP requests by an order of magnitude!