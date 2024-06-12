using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

interface IApp
{
    string ProductId { get; }
    Uri Uri { get; }
    string Path { get; }
    string PackageFamilyName { get; }
}

file struct App(string productId, Uri uri, string path, string packageFamilyName) : IApp
{
    public readonly string ProductId => productId;
    public readonly Uri Uri => uri;
    public readonly string Path => path;
    public readonly string PackageFamilyName => packageFamilyName;
}

static class Apps
{
    internal static IApp Release => new App(
        "9NBLGGH2JHXJ",
        new("minecraft://"),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "Release"),
        "Microsoft.MinecraftUWP_8wekyb3d8bbwe");
    internal static IApp Preview => new App(
        "9P5X4QVLC2XR",
        new("minecraft-preview://"),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "Preview"),
        "Microsoft.MinecraftWindowsBeta_8wekyb3d8bbwe");
}

interface IAppxPackage
{
    IUpdateIdentity Update { get; }
    string Address { get; }
    string FileName { get; }
}

file struct AppxPackage(IUpdateIdentity update, string address, string fileName) : IAppxPackage
{
    public readonly IUpdateIdentity Update => update;

    public readonly string Address => address;

    public readonly string FileName => fileName;
}

class UpdateManager
{
    internal readonly Store Store;

    static readonly string TempPath = Path.GetTempPath();

    static readonly IEnumerable<string> packageFamilyNames = ["Microsoft.Services.Store.Engagement_8wekyb3d8bbwe"];

    UpdateManager(Store store)
    {
        Store = store;
    }

    internal static async Task<UpdateManager> CreateAsync()
    {
        return new(await Store.CreateAsync());
    }

    internal async Task<IEnumerable<IAppxPackage>> GetAsync(IApp app)
    {
        List<IAppxPackage> packages = [];

        foreach (var update in (await Store.SyncUpdatesAsync(await Store.GetProductAsync(app.ProductId))).Where(update => !packageFamilyNames.Contains(update.PackageFamilyName)))
            packages.Add(new AppxPackage(update, await Store.GetUrlAsync(update), Path.Combine(TempPath, $"{update.PackageFamilyName}.appx")));

        return packages;
    }
}