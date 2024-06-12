using System;
using System.IO.Packaging;
using System.Linq;
using System.Threading.Tasks;
using Windows.Management.Deployment;
using Windows.System;

static class AppLauncher
{
    static readonly PackageManager packageManager = new();
    internal static async Task<bool> Launch(IApp app)
    {
        var package = packageManager.FindPackagesForUser(string.Empty, app.PackageFamilyName).FirstOrDefault();
        if (!package?.InstalledLocation.Path.Equals(app.Path, StringComparison.OrdinalIgnoreCase) ?? true)
        {
            if (package?.IsDevelopmentMode ?? false) return false;
            try { await AppInstaller.RegisterAsync(app); }
            catch { return false;}
        }
        return await Launcher.LaunchUriAsync(app.Uri);
    }
}