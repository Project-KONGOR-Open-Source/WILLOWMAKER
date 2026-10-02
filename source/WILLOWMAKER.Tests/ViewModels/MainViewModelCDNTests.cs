namespace WILLOWMAKER.Tests.ViewModels;

/// <summary>
///     Verifies the CDN selection of <see cref="MainViewModel"/>: the CDN options offered for each master server, address validation, launch gating, and connectivity probing.
///     The work of each test is dispatched to the headless UI thread, and the observed state is returned so that it can be asserted on the test thread.
///     The tests run sequentially, since the probe tests depend on timing.
/// </summary>
[NotInParallel]
public sealed class MainViewModelCDNTests
{
    private const string CustomMasterServer = "Custom Address ...";

    private static string ClientVariant => OperatingSystem.IsWindows() ? "wac" : OperatingSystem.IsLinux() ? "lac" : "mac";

    [Test]
    public async Task The_Official_Master_Server_Offers_The_Public_CDNs_And_Selects_The_Mesh_CDN()
    {
        (string[] options, string? selectedOption) = await HeadlessSession.Dispatch(() =>
        {
            MainViewModel viewModel = CreateIdleViewModel();

            return (viewModel.AvailableCDNOptions.Select(option => option.TargetURL).ToArray(), viewModel.SelectedCDNAddressItem?.TargetURL);
        });

        using (Assert.Multiple())
        {
            await Assert.That(options.SequenceEqual(["cdn.kongor.net", "api.kongor.net/cdn", "CUSTOM"])).IsTrue();
            await Assert.That(selectedOption).IsEqualTo("cdn.kongor.net");
        }
    }

    [Test]
    public async Task The_Local_Master_Server_Offers_The_Local_CDN_First_And_Selects_It()
    {
        (string[] options, string? selectedOption) = await HeadlessSession.Dispatch(() =>
        {
            MainViewModel viewModel = CreateIdleViewModel();

            viewModel.MasterServerAddress = new ComboBoxItem { Content = "localhost:5555" };

            return (viewModel.AvailableCDNOptions.Select(option => option.TargetURL).ToArray(), viewModel.SelectedCDNAddressItem?.TargetURL);
        });

        using (Assert.Multiple())
        {
            await Assert.That(options.SequenceEqual(["localhost:5555/cdn", "cdn.kongor.net", "api.kongor.net/cdn", "CUSTOM"])).IsTrue();
            await Assert.That(selectedOption).IsEqualTo("localhost:5555/cdn");
        }
    }

    [Test]
    public async Task A_Custom_Master_Server_Offers_Only_A_Custom_CDN_With_Both_Custom_Fields_Empty()
    {
        (string[] options, bool customCDNAddressFieldIsShown, string? customMasterServerAddress, string? customCDNAddress, bool cdnAddressIsValid) = await HeadlessSession.Dispatch(() =>
        {
            MainViewModel viewModel = CreateIdleViewModel();

            viewModel.MasterServerAddress = new ComboBoxItem { Content = "localhost:5555" };
            viewModel.SelectedCDNAddressItem = viewModel.AvailableCDNOptions.Single(option => option.TargetURL is "CUSTOM");
            viewModel.CustomCDNAddress = "cdn.example.com";
            viewModel.MasterServerAddress = new ComboBoxItem { Content = CustomMasterServer };

            return (viewModel.AvailableCDNOptions.Select(option => option.TargetURL).ToArray(), viewModel.CanShowCustomCDNAddressField, viewModel.CustomMasterServerAddress, viewModel.CustomCDNAddress, viewModel.CDNAddressIsValid);
        });

        using (Assert.Multiple())
        {
            await Assert.That(options.SequenceEqual(["CUSTOM"])).IsTrue();
            await Assert.That(customCDNAddressFieldIsShown).IsTrue();
            await Assert.That(customMasterServerAddress).IsEqualTo(string.Empty);
            await Assert.That(customCDNAddress).IsEqualTo(string.Empty);
            await Assert.That(cdnAddressIsValid).IsFalse();
        }
    }

    [Test]
    public async Task An_Invalid_Custom_CDN_Address_Disables_Both_Launch_Buttons()
    {
        (bool cdnAddressIsValid, bool canLaunchGameClient, bool canLaunchMapEditor, string statusMessage) = await HeadlessSession.Dispatch(() =>
        {
            MainViewModel viewModel = CreateIdleViewModel();

            viewModel.MasterServerAddress = new ComboBoxItem { Content = CustomMasterServer };
            viewModel.CustomMasterServerAddress = "localhost:5555";
            viewModel.CustomCDNAddress = "cdn .kongor.net";

            return (viewModel.CDNAddressIsValid, viewModel.CanLaunchGameClient, viewModel.CanLaunchMapEditor, viewModel.CDNProbeStatusMessage);
        });

        using (Assert.Multiple())
        {
            await Assert.That(cdnAddressIsValid).IsFalse();
            await Assert.That(canLaunchGameClient).IsFalse();
            await Assert.That(canLaunchMapEditor).IsFalse();
            await Assert.That(statusMessage).IsEqualTo("Invalid CDN Address");
        }
    }

    [Test]
    public async Task A_Valid_Custom_CDN_Address_Enables_Both_Launch_Buttons_Without_Waiting_For_The_Probe()
    {
        (bool canLaunchGameClient, bool canLaunchMapEditor, bool probeSucceeded) = await HeadlessSession.Dispatch(() =>
        {
            MainViewModel viewModel = CreateIdleViewModel();

            viewModel.MasterServerAddress = new ComboBoxItem { Content = CustomMasterServer };
            viewModel.CustomMasterServerAddress = "localhost:5555";
            viewModel.CustomCDNAddress = "localhost:5555/cdn";

            return (viewModel.CanLaunchGameClient, viewModel.CanLaunchMapEditor, viewModel.CDNProbeSucceeded);
        });

        using (Assert.Multiple())
        {
            await Assert.That(canLaunchGameClient).IsTrue();
            await Assert.That(canLaunchMapEditor).IsTrue();
            await Assert.That(probeSucceeded).IsFalse();
        }
    }

    [Test]
    public async Task The_Map_Editor_Can_Be_Launched_Without_A_Valid_Master_Server()
    {
        (bool canLaunchGameClient, bool canLaunchMapEditor) = await HeadlessSession.Dispatch(() =>
        {
            MainViewModel viewModel = CreateIdleViewModel();

            viewModel.MasterServerAddress = new ComboBoxItem { Content = CustomMasterServer };
            viewModel.CustomCDNAddress = "localhost:5555/cdn";

            return (viewModel.CanLaunchGameClient, viewModel.CanLaunchMapEditor);
        });

        using (Assert.Multiple())
        {
            await Assert.That(canLaunchGameClient).IsFalse();
            await Assert.That(canLaunchMapEditor).IsTrue();
        }
    }

    [Test]
    public async Task Resolving_The_Active_CDN_URL_Normalises_The_Selected_CDN_Address()
    {
        (string officialCDNURL, string localCDNURL, string customCDNURL) = await HeadlessSession.Dispatch(() =>
        {
            MainViewModel viewModel = CreateIdleViewModel();

            string officialCDNURL = viewModel.ResolveActiveCDNURL();

            viewModel.MasterServerAddress = new ComboBoxItem { Content = "localhost:5555" };

            string localCDNURL = viewModel.ResolveActiveCDNURL();

            viewModel.MasterServerAddress = new ComboBoxItem { Content = CustomMasterServer };
            viewModel.CustomCDNAddress = "cdn.example.com/files";

            string customCDNURL = viewModel.ResolveActiveCDNURL();

            return (officialCDNURL, localCDNURL, customCDNURL);
        });

        using (Assert.Multiple())
        {
            await Assert.That(officialCDNURL).IsEqualTo("https://cdn.kongor.net/");
            await Assert.That(localCDNURL).IsEqualTo("http://localhost:5555/cdn/");
            await Assert.That(customCDNURL).IsEqualTo("https://cdn.example.com/files/");
        }
    }

    [Test]
    public async Task Probing_A_Reachable_CDN_Reports_It_As_Online()
    {
        await using TestHTTPServer server = new (HttpStatusCode.OK);

        (bool probeSucceeded, string statusMessage) = await HeadlessSession.Dispatch(async () =>
        {
            MainViewModel viewModel = CreateViewModelWithCustomCDN(server.BaseURL);

            await WaitForProbeResult(viewModel);

            return (viewModel.CDNProbeSucceeded, viewModel.CDNProbeStatusMessage);
        });

        using (Assert.Multiple())
        {
            await Assert.That(probeSucceeded).IsTrue();
            await Assert.That(statusMessage).IsEqualTo("CDN Is Online: HTTP 200 (OK)");
            await Assert.That(server.LastRequestMethod).IsEqualTo("HEAD");
            await Assert.That(server.LastRequestPath).IsEqualTo($"/{ClientVariant}/manifest.json");
        }
    }

    [Test]
    public async Task Probing_A_CDN_Which_Responds_With_An_Error_Reports_It_As_Unreachable()
    {
        await using TestHTTPServer server = new (HttpStatusCode.NotFound);

        (bool probeSucceeded, bool probeFailed, string statusMessage) = await HeadlessSession.Dispatch(async () =>
        {
            MainViewModel viewModel = CreateViewModelWithCustomCDN(server.BaseURL);

            await WaitForProbeResult(viewModel);

            return (viewModel.CDNProbeSucceeded, viewModel.CDNProbeFailed, viewModel.CDNProbeStatusMessage);
        });

        using (Assert.Multiple())
        {
            await Assert.That(probeSucceeded).IsFalse();
            await Assert.That(probeFailed).IsTrue();
            await Assert.That(statusMessage).IsEqualTo("CDN Is Unreachable: HTTP 404 (NotFound)");
        }
    }

    [Test]
    public async Task Changing_The_CDN_Address_Clears_The_Previous_Probe_Result_Immediately()
    {
        await using TestHTTPServer server = new (HttpStatusCode.OK);

        (bool probeSucceededBeforeChange, bool probeSucceededAfterChange, string statusMessageAfterChange) = await HeadlessSession.Dispatch(async () =>
        {
            MainViewModel viewModel = CreateViewModelWithCustomCDN(server.BaseURL);

            await WaitForProbeResult(viewModel);

            bool probeSucceededBeforeChange = viewModel.CDNProbeSucceeded;

            viewModel.CustomCDNAddress = "http://127.0.0.1:1/";

            return (probeSucceededBeforeChange, viewModel.CDNProbeSucceeded, viewModel.CDNProbeStatusMessage);
        });

        using (Assert.Multiple())
        {
            await Assert.That(probeSucceededBeforeChange).IsTrue();
            await Assert.That(probeSucceededAfterChange).IsFalse();
            await Assert.That(statusMessageAfterChange).IsEqualTo(string.Empty);
        }
    }

    [Test]
    public async Task A_Superseded_Probe_Does_Not_Overwrite_The_Result_Of_The_Probe_Which_Replaced_It()
    {
        await using TestHTTPServer slowServer = new (HttpStatusCode.OK, responseDelay: TimeSpan.FromSeconds(2));
        await using TestHTTPServer fastServer = new (HttpStatusCode.NotFound);

        (bool probeSucceeded, string statusMessage) = await HeadlessSession.Dispatch(async () =>
        {
            MainViewModel viewModel = CreateViewModelWithCustomCDN(slowServer.BaseURL);

            await WaitUntil(() => viewModel.CDNProbeInProgress);

            viewModel.CustomCDNAddress = fastServer.BaseURL;

            await WaitForProbeResult(viewModel);

            // Outlast The Slow Server's Response, Which Would Report The CDN As Online If The Superseded Probe Were Still Applied
            await Task.Delay(TimeSpan.FromSeconds(3));

            return (viewModel.CDNProbeSucceeded, viewModel.CDNProbeStatusMessage);
        });

        using (Assert.Multiple())
        {
            await Assert.That(probeSucceeded).IsFalse();
            await Assert.That(statusMessage).IsEqualTo("CDN Is Unreachable: HTTP 404 (NotFound)");
        }
    }

    [Test]
    public async Task A_Failure_To_Write_The_Log_Does_Not_Affect_The_Probe_Result()
    {
        await using TestHTTPServer server = new (HttpStatusCode.OK);

        (bool probeInProgress, bool probeSucceeded, string statusMessage) = await HeadlessSession.Dispatch(async () =>
        {
            MainViewModel viewModel = CreateIdleViewModel();

            viewModel.MasterServerAddress = new ComboBoxItem { Content = CustomMasterServer };

            // Holding The Log File Open Exclusively Makes Every Write To It Fail While The Probe Runs
            using (FileStream logFile = new (Path.Combine(Environment.CurrentDirectory, DeploymentManifest.LogFileName), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                viewModel.CustomCDNAddress = server.BaseURL;

                await WaitForProbeResult(viewModel);
            }

            return (viewModel.CDNProbeInProgress, viewModel.CDNProbeSucceeded, viewModel.CDNProbeStatusMessage);
        });

        using (Assert.Multiple())
        {
            await Assert.That(probeInProgress).IsFalse();
            await Assert.That(probeSucceeded).IsTrue();
            await Assert.That(statusMessage).IsEqualTo("CDN Is Online: HTTP 200 (OK)");
        }
    }

    private static MainViewModel CreateIdleViewModel()
        => new () { UpdateStatus = UpdateStatus.ApplicationUpToDate };

    private static MainViewModel CreateViewModelWithCustomCDN(string customCDNAddress)
    {
        MainViewModel viewModel = CreateIdleViewModel();

        viewModel.MasterServerAddress = new ComboBoxItem { Content = CustomMasterServer };
        viewModel.CustomCDNAddress = customCDNAddress;

        return viewModel;
    }

    private static Task WaitForProbeResult(MainViewModel viewModel)
        => WaitUntil(() => viewModel.CDNProbeInProgress is false && string.IsNullOrEmpty(viewModel.CDNProbeStatusMessage) is false);

    private static async Task WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(15);

        while (condition() is false)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The Expected View Model State Was Not Reached");

            // Awaiting On The UI Thread Lets The Dispatcher Process The Probe Results Posted By Background Tasks
            await Task.Delay(25);
        }
    }
}
