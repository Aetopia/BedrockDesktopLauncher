using System;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Hosting;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;


static class Program
{
  [STAThread]
  static void Main()
  {
    new Application().Run(new MainWindow());
    // ((NameValueCollection)ConfigurationManager.GetSection("System.Windows.Forms.ApplicationConfigurationSection"))["DpiAwareness"] = "PerMonitorV2";
    // Application.EnableVisualStyles();
    // Application.SetCompatibleTextRenderingDefault(false);
    // Application.Run(new MainForm());
    //  var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content");
    //  Directory.CreateDirectory(path);
    //   Directory.SetCurrentDirectory(path);

    //   var appManager = await AppManager.CreateAsync();
    //   var packages = await appManager.GetAsync(Apps.Preview);

    //   WebClient webClient = new();
    //   webClient.DownloadProgressChanged += (sender, e) => Console.Write($"\r{e.ProgressPercentage}%");
    //   webClient.DownloadFileCompleted += (sender, e) => Console.WriteLine();

    //   foreach (var package in packages)
    //   {
    //       Console.WriteLine(package.Update.PackageFamilyName);
    //
    //      await webClient.DownloadFileTaskAsync(package.Address, package.FileName);
    //      AppInstaller.Extract(package.FileName, Apps.Preview);
    //  }
    //  }
    //  await AppInstaller.RegisterAsync(Apps.Preview);

    //   Console.WriteLine(new JavaScriptSerializer().Serialize(packages));
  }
}
