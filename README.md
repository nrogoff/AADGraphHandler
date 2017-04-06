 # AADGraphHandler
Azure Active Directory Graph Client Handler or Wrapper. This is a simple wrapper to make using the SDK easier.

## NuGet
You can find the NuGet Package here https://www.nuget.org/packages/hms.Common.Azure.AADGraphHandler/

## How to use AADGraphHandler
You can read a more detailed explanation of AADGraphHandler on my blog at https://blog.nicholasrogoff.com/2017/04/06/azure-active-directory-graph-api-wrapper-to-help-make-it-a-bit-easier/

### 1. Install the NuGet package
Search for AADGraphHandler in the NuGet package manager and install the latest version.

### 2. Add the following application settings into you web.config or app.config

    <appSettings>
        <!-- START Azure AD For Integration Tests -->
        <!-- REPLACE VALUES BELOW WITH YOUR DOMAIN VALUES -->
        <add key="aad:TenantId" value="{12345678-1234-1234-1234-012345678912}" />
        <add key="aad:TenantDisplayName" value="yourdomain.com" />
        <add key="aad:TenantDefaultDomain" value="yourdomain.com" />
        <add key="aad:TenantInitialDomainName" value="yourtenantname.onmicrosoft.com" />
        <add key="aad:ApiAppClientId" value="12345678-1234-1234-1234-012345678912" />
        <add key="aad:ApiAppClientSecret" value="1234567891234567891234567981234567891234567=" />
        <add key="aad:NativeAppClientId" value="12345678-1234-1234-1234-012345678912" />
        <add key="aad:GraphServiceRootUri" value="https://graph.windows.net" />
        <add key="aad:AuthorityServiceRootUri" value="https://login.microsoftonline.com/" />
        <add key="aad:RedirectUri" value="https://localhost" />
        <add key="aad:IntegrationUserAdminUserName" value="IntegrationTest.Admin@yourdomain.com" />
        <!-- END Azure AD -->
    </appSettings>

You will need to reference these when instantiating the AADGraphHandler.

### 3. Create the AADGraphHandler Configuration object
There are two different configuration objects. One for use as an application and one for use as a user.


    var appConfig = new AADGraphHandler.AADGraphHandlerConfigurationForApp
    {
        TenantId = TenantId,
        GraphServiceRootUri = new Uri(GraphServiceRootUri),
        AuthorityServiceRootUri = new Uri(AuthorityServiceRootUri),
        TenantDisplayName = TenantDisplayName,
        AppClientId = ApiAppClientId,
        AppClientSecret = ApiAppClientSecret
    };
    var graphHandlerAsApp = new AADGraphHandler(appConfig);


or as a user


    var userConfig = new AADGraphHandler.AADGraphHandlerConfigurationForUser
    {
        TenantId = TenantId,
        GraphServiceRootUri = new Uri(GraphServiceRootUri),
        AuthorityServiceRootUri = new Uri(AuthorityServiceRootUri),
        TenantDisplayName = TenantDisplayName,
        RedirectUri = new Uri(RedirectUri),
        AppClientId = NativeAppClientId,
        Username = username
    };
    var graphHandlerAsUser = new AADGraphHandler(userConfig);


