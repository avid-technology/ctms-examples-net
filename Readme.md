# Please Read Me #
* Implementation:
    * The examples are implemented with C# 6/.NET 4.7.2 (target framework) and Visual Studio 2017.
    * There are two categories of examples: those using the PlatformSDK (projects to be identified with the suffix "SDK") and those using REST and HATEOAS directly.
	* The examples apply a lot of code repetition to keep them self contained.
	* The non-PlatformSDK examples are configured to use a request timeout of 60s each.
	* There are some error checks but those are very general and might not cover all cases. Esp. timeouts or unreachable endpoints could happen at any time during the application of REST and HATEOAS.
    * No optimization and no parallelization (e.g. for requesting results from the platform) was implemented.
        * Esp. the examples use HATEOAS to get all links. Instead of HATEOAS all links could be used hard coded or being "bookmarked" without HATEOAS (resulting in faster execution time), but this is not the idea behind RESTful interfaces. Also mind, that those links could change in future so the only save way is to get the via HATEOAS. The examples do only use these URLs directly: https://$apidomain, https://$apidomain/auth and https://$apiDomain/apis/avid.ctms.registry other URLs are resolved using the CTMS Registry and HATEOAS!
    * For testing purposes, it was required to configure HTTP libraries to accept arbitrary SSL certificates. Please notice, that this may not be acceptable for productive code.
	* When running the examples it is required to pass an HTTP basic auth string via the command line. The HTTP basic auth string token can be obtained from Avid.
	* The realm, which is passed to each example is checked by the CTMS Registry. If the services needed by the example cannot be found in the specified realm, the example requests the CTMS Registry for an alternative target.

* Dependencies:
    * The projects using SDK examples make use of PlatformSDK.dll. This assembly, as well as a belonging to config file, reside in the folder lib.
    * Additionally, these 3rd-party libraries are used: Newtonsoft.Json 12.0.2, Tavis.UriTemplates 1.1.1 and WebApi.Hal 2.6.0 
    * Each project in the solution brings its own packages.config file, in which the required NuGet packages are listed.
    * The Visual Studio solution is self contained, dependent libraries can be resolved via NuGet:
		* The installation of required NuGet packages runs automatically, when the solution is opened. If that didn't happen, the packages can be reinstalled manually:
			* For the following steps a "NuGet Package Manager Console Host" of version >= 2.8.60723.765 is required.
			* Windows PowerShell script execution must be enabled: issue "set-executionpolicy remotesigned" in an admin PowerShell console.
			* Open Tools>NuGet Package Manager>Package Manager Console:
				* Make sure, that the Package Manager Console is configured to have the "Package source" set to "All".
				* Issue the command "update-package -reinstall".

* Running the examples:
    * => When running the executables on a terminal, make sure you have specified correct command line arguments: __ExampleApplicationName__ _apidomain_ _httpbasicauthstring_ _[servicetype]_ _[serviceversion]_ _[realm]_
    * The QueryServiceRegistry example needs no servicetype (always "avid.ctms.registry") and no realm (always "global"/"") argument.
        * QueryServiceRegistry.exe _apidomain_ _httpbasicauthstring_
        * Example: QueryServiceRegistry upstream UZ86FbYI76534
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