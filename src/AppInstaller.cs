using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Windows.Management.Deployment;

static class AppInstaller
{
    static readonly PackageManager packageManager = new();

    internal static async Task<bool> ExtractAsync(string archiveFileName, IApp app, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            Directory.CreateDirectory(app.Path);
            var file = ZipFile.OpenRead(archiveFileName);
            foreach (var entry in file.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!entry.Name.Equals("AppxSignature.p7x", StringComparison.OrdinalIgnoreCase))
                {
                    var destinationFileName = Path.Combine(app.Path, entry.FullName);
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationFileName));
                    entry.ExtractToFile(destinationFileName, true);
                }
            }
            return true;
        }, cancellationToken);
    }

    internal static async Task RegisterAsync(IApp app)
    {
        var path = Path.Combine(app.Path, "AppxManifest.xml");
        Console.WriteLine(path);
        await Task.Run(() =>
        {

            XmlDocument xml = new();
            xml.Load(path);

            ((XmlElement)xml.GetElementsByTagName("uap:VisualElements")[0]).SetAttribute("AppListEntry", "none");

            var newChild = xml.GetElementsByTagName("TargetDeviceFamily")[0];
            var node = xml.GetElementsByTagName("Dependencies")[0];

            node.RemoveAll();
            node.AppendChild(newChild);

            xml.Save(path);
        });

        await packageManager.RegisterPackageAsync(new(path), null, DeploymentOptions.DevelopmentMode | DeploymentOptions.ForceApplicationShutdown);
    }
}