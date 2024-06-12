using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml;
using Windows.Management.Deployment;
using Windows.System.UserProfile;

interface IProduct
{
    string Title { get; }

    string AppCategoryId { get; }
}

file struct Product(string title, string appCategoryId) : IProduct
{
    public readonly string Title => title;

    public readonly string AppCategoryId => appCategoryId;
}

interface IUpdateIdentity
{
    string PackageFamilyName { get; }

    string UpdateId { get; }

    bool MainPackage { get; }

    bool Updatable { get; }
}

file struct UpdateIdentity(string packageFamilyName, string updateId, bool mainPackage, bool updateable) : IUpdateIdentity
{
    public readonly string PackageFamilyName => packageFamilyName;

    public readonly string UpdateId => updateId;

    public readonly bool MainPackage => mainPackage;

    public readonly bool Updatable => updateable;
}

file class Update
{
    internal string Id;

    internal DateTime Modified;

    internal bool MainPackage;
}

file static class Resources
{
    static readonly Assembly assembly = Assembly.GetExecutingAssembly();

    internal readonly static string GetCookie = ToString("GetCookie.xml");

    internal readonly static string GetExtendedUpdateInfo2 = ToString("GetExtendedUpdateInfo2.xml");

    internal readonly static string SyncUpdates = ToString("SyncUpdates.xml");

    static string ToString(string name)
    {
        using StreamReader stream = new(assembly.GetManifestResourceStream(name));
        return stream.ReadToEnd();
    }
}

file struct SynchronizationContextRemover : INotifyCompletion
{
    internal readonly bool IsCompleted => SynchronizationContext.Current == null;

    internal readonly SynchronizationContextRemover GetAwaiter() => this;

    internal readonly void GetResult() { }

    public readonly void OnCompleted(Action continuation)
    {
        var syncContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(null);
            continuation();
        }
        finally { SynchronizationContext.SetSynchronizationContext(syncContext); }
    }
}

class Store
{
    readonly string syncUpdates;

    static readonly JavaScriptSerializer javaScriptSerializer = new();

    static readonly string requestUri = $"https://storeedgefd.dsx.mp.microsoft.com/v9.0/products/{{0}}?market={GlobalizationPreferences.HomeGeographicRegion}&locale=iv&deviceFamily=Windows.Desktop";

    static readonly HttpClient httpClient = new() { BaseAddress = new("https://fe3.delivery.mp.microsoft.com/ClientWebService/client.asmx/") };

    static readonly PackageManager packageManager = new();

    static readonly string architecture = RuntimeInformation.OSArchitecture.ToString().ToLower();

    internal async Task<IProduct> GetProductAsync(string productId)
    {
        await default(SynchronizationContextRemover);

        using var response = await httpClient.GetAsync(string.Format(requestUri, productId));
        response.EnsureSuccessStatusCode();

        var payload = (Dictionary<string, object>)javaScriptSerializer.Deserialize<Dictionary<string, object>>(await response.Content.ReadAsStringAsync())["Payload"];
        payload.TryGetValue("ShortTitle", out object value);

        return new Product(
            (string)(string.IsNullOrEmpty((string)value) ? payload["Title"] : value),
            javaScriptSerializer.Deserialize<Dictionary<string, string>>(
                (string)((Dictionary<string, object>)((ArrayList)payload["Skus"])[0])["FulfillmentData"])["WuCategoryId"]);
    }

    static async Task<XmlDocument> PostAsSoapAsync(string content, bool secured = false)
    {
        using var response = await httpClient.PostAsync(secured ? "secured" : null, new StringContent(content, Encoding.UTF8, "application/soap+xml"));
        response.EnsureSuccessStatusCode();

        XmlDocument xmlDocument = new();
        xmlDocument.LoadXml((await response.Content.ReadAsStringAsync()).Replace("&lt;", "<").Replace("&gt;", ">"));
        return xmlDocument;
    }

    Store(string encryptedData) { syncUpdates = Resources.SyncUpdates.Replace("{1}", encryptedData); }

    internal static async Task<Store> CreateAsync()
    {
        await default(SynchronizationContextRemover);

        return new((await PostAsSoapAsync(Resources.GetCookie)).GetElementsByTagName("EncryptedData")[0].InnerText);
    }

    internal async Task<string> GetUrlAsync(IUpdateIdentity update)
    {
        await default(SynchronizationContextRemover);

        return (await PostAsSoapAsync(Resources.GetExtendedUpdateInfo2.Replace("{1}", update.UpdateId), true)).GetElementsByTagName("Url").Cast<XmlNode>().First(
            xmlNode => xmlNode.InnerText.StartsWith("http://tlu.dl.delivery.mp.microsoft.com")).InnerText;
    }

    static bool CheckUpdateAvailability(string packageFullName)
    {
        var packageIdentity = packageFullName.Split('_');
        var package = packageManager.FindPackagesForUser(string.Empty, $"{packageIdentity.First()}_{packageIdentity.Last()}").FirstOrDefault();

        return package == null || new Version(packageIdentity[1]) > new Version(package.Id.Version.Major, package.Id.Version.Minor, package.Id.Version.Build, package.Id.Version.Revision);
    }

    internal async Task<IEnumerable<IUpdateIdentity>> SyncUpdatesAsync(IProduct product)
    {
        await default(SynchronizationContextRemover);

        var syncUpdatesResult = (XmlElement)(await PostAsSoapAsync(syncUpdates.Replace("{2}", product.AppCategoryId))).GetElementsByTagName("SyncUpdatesResult")[0];

        Dictionary<string, Update> updates = [];
        foreach (XmlNode xmlNode in syncUpdatesResult.GetElementsByTagName("AppxPackageInstallData"))
        {
            var xmlElement = (XmlElement)xmlNode.ParentNode.ParentNode.ParentNode;
            var file = xmlElement.GetElementsByTagName("File")[0];

            var packageIdentity = file.Attributes["InstallerSpecificIdentifier"].InnerText.Split('_');
            if (!packageIdentity[2].Equals(architecture) && !packageIdentity[2].Equals("neutral")) continue;

            var packageFamilyName = $"{packageIdentity.First()}_{packageIdentity.Last()}";
            if (!updates.ContainsKey(packageFamilyName)) updates.Add(packageFamilyName, new());

            var modified = Convert.ToDateTime(file.Attributes["Modified"].InnerText);
            if (updates[packageFamilyName].Modified < modified)
            {
                updates[packageFamilyName].Id = xmlElement["ID"].InnerText;
                updates[packageFamilyName].Modified = modified;
                updates[packageFamilyName].MainPackage = xmlNode.Attributes["MainPackage"].InnerText == "true";
            }
        }

        List<IUpdateIdentity> updateIdentities = [];
        foreach (XmlNode xmlNode in syncUpdatesResult.GetElementsByTagName("SecuredFragment"))
        {
            var xmlElement = (XmlElement)xmlNode.ParentNode.ParentNode.ParentNode;
            var update = updates.FirstOrDefault(update => update.Value.Id.Equals(xmlElement["ID"].InnerText));
            if (update.Value == null) continue;

            updateIdentities.Add(new UpdateIdentity(
                update.Key, 
                xmlElement.GetElementsByTagName("UpdateIdentity")[0].Attributes["UpdateID"].InnerText, 
                update.Value.MainPackage,
                CheckUpdateAvailability(xmlElement.GetElementsByTagName("AppxMetadata")[0].Attributes["PackageMoniker"].InnerText)));
        }

        return updateIdentities.OrderBy(updateIdentity => updateIdentity.MainPackage);
    }
}