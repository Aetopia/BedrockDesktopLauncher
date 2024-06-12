using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

file class Resources
{
    static readonly Assembly assembly = Assembly.GetExecutingAssembly();

    internal static string GetString(string name)
    {
        using var stream = assembly.GetManifestResourceStream(name);
        using StreamReader streamReader = new(stream);
        return streamReader.ReadToEnd();
    }
}

class MainWindow : Window
{
    internal MainWindow()
    {
        using WebClient webClient = new();
        Title = "Bedrock Desktop Launcher";
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        Grid grid1 = new()
        {
            Width = 600,
            Height = 450,
        };
        Content = grid1;
        grid1.RowDefinitions.Add(new());
        grid1.RowDefinitions.Add(new() { Height = GridLength.Auto });
        grid1.RowDefinitions.Add(new() { Height = GridLength.Auto });

        WebBrowser webBrowser = new()
        {
            IsHitTestVisible = false
        };
        webBrowser.NavigateToString($@"
<head>
<meta http-equiv=""X-UA-Compatible"" content=""IE=edge""/>
</head>
<body style=""height: 96vh"" oncontextmenu=""return false"" ondragstart=""return false"">
{(global::Resources.GetString("Minecraft.svg"))}
</body>");
        Grid.SetRow(webBrowser, 0);
        grid1.Children.Add(webBrowser);

        Grid grid2 = new()
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        grid2.ColumnDefinitions.Add(new());
        grid2.ColumnDefinitions.Add(new());
        Grid.SetRow(grid2, 1);
        grid1.Children.Add(grid2);

        ComboBox comboBox = new()
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,

        };
        comboBox.Items.Add("Release");
        comboBox.Items.Add("Preview");
        comboBox.SelectedItem = "Release";
        Grid.SetColumn(comboBox, 0);
        grid2.Children.Add(comboBox);

        Button button = new()
        {
            Content = "Play",
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        Grid.SetColumn(button, 1);
        grid2.Children.Add(button);


        ProgressBar progressBar = new()
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 23
        };
        Grid.SetRow(progressBar, 2);
        grid1.Children.Add(progressBar);

        webClient.DownloadProgressChanged += (sender, e) =>
        {
            progressBar.Value = e.ProgressPercentage;
        };

        webClient.DownloadFileCompleted += (sender, e) => progressBar.Value = 0;

        CancellationTokenSource cancellationTokenSource = default;

        button.Click += async (sender, e) =>
        {
            if (button.Content.Equals("Cancel"))
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
            }
            else
            {
                cancellationTokenSource = new();
                cancellationTokenSource.Token.Register(webClient.CancelAsync);

                var app = comboBox.SelectedItem.Equals("Release") ? Apps.Release : Apps.Preview;
                button.IsEnabled = comboBox.IsEnabled = false;
                if (!await AppLauncher.Launch(app))
                {
                    button.Content = "Cancel";
                    var updateManager = await UpdateManager.CreateAsync();
                    var packages = await updateManager.GetAsync(app);
                    button.IsEnabled = true;
                    foreach (var package in packages)
                    {
                        try
                        {
                            progressBar.IsIndeterminate = false;
                            await webClient.DownloadFileTaskAsync(package.Address, package.FileName);

                            progressBar.IsIndeterminate = true;
                            await AppInstaller.ExtractAsync(package.FileName, app, cancellationTokenSource.Token);
                        }
                        catch { break; }
                    }
                    progressBar.IsIndeterminate = false;
                }
            }
            button.Content = "Play";
            button.IsEnabled = comboBox.IsEnabled = true;
        };
    }
}