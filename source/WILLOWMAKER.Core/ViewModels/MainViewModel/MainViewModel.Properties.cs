namespace WILLOWMAKER.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string? GitHubLink { get; set; } = "https://github.com/Project-KONGOR-Open-Source";

    [ObservableProperty]
    public partial string? WebPortalLink { get; set; } = "https://kongor.net";

    [ObservableProperty]
    public partial string? RedditLink { get; set; } = "https://www.reddit.com/r/newerth";

    [ObservableProperty]
    public partial string? DiscordLink { get; set; } = "https://discord.com/invite/N6pKzGDqUH";

    [ObservableProperty]
    public partial string? ElementLink { get; set; } = "https://app.element.io/#/room/#newerth:matrix.org";

    [ObservableProperty]
    public partial ComboBoxItem? MasterServerAddress { get; set; } = new () { Content = "api.kongor.net" }; // Needs To Match The Default Value In The XAML

    [ObservableProperty]
    public partial string? CustomMasterServerAddress { get; set; }

    [ObservableProperty]
    public partial bool CanShowCustomMasterServerAddressField { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunchGameClient))]
    public partial bool MasterServerAddressIsValid { get; set; } = true;

    [ObservableProperty]
    public partial string VersionDisplay { get; set; } = VersionChecker.CurrentVersionDisplay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateIsPendingMessage))]
    public partial string? LatestAvailableVersionDisplay { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReleasesRepositoryIsUnreachable))]
    [NotifyPropertyChangedFor(nameof(UpdateIsUnavailable))]
    [NotifyPropertyChangedFor(nameof(UpdateIsPending))]
    [NotifyPropertyChangedFor(nameof(UpdateIsDownloading))]
    [NotifyPropertyChangedFor(nameof(UpdateIsRestarting))]
    [NotifyPropertyChangedFor(nameof(UpdateIsInstalling))]
    [NotifyPropertyChangedFor(nameof(UpdateCheckIsInProgress))]
    [NotifyPropertyChangedFor(nameof(UpdateCheckIsIdle))]
    [NotifyPropertyChangedFor(nameof(MasterServerInputIsEnabled))]
    [NotifyPropertyChangedFor(nameof(CanLaunchMapEditor))]
    [NotifyPropertyChangedFor(nameof(CanLaunchGameClient))]
    public partial UpdateStatus UpdateStatus { get; set; } = UpdateStatus.CheckInProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateDownloadPercentDisplay))]
    public partial double UpdateDownloadPercent { get; set; } = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SynchronisationIsIdle))]
    [NotifyPropertyChangedFor(nameof(MasterServerInputIsEnabled))]
    [NotifyPropertyChangedFor(nameof(CanLaunchMapEditor))]
    [NotifyPropertyChangedFor(nameof(CanLaunchGameClient))]
    public partial bool SynchronisationIsActive { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunchMapEditor))]
    [NotifyPropertyChangedFor(nameof(CanLaunchGameClient))]
    public partial bool LaunchIsInProgress { get; set; } = false;

    [ObservableProperty]
    public partial bool SynchronisationIsScheduled { get; set; } = false;

    [ObservableProperty]
    public partial double SynchronisationProgressPercent { get; set; } = 0;

    [ObservableProperty]
    public partial string SynchronisationStatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool SynchronisationIsFailed { get; set; } = false;

    [ObservableProperty]
    public partial string DownloadedFilesDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RemovedFilesDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SkippedFilesDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UpToDateFilesDisplay { get; set; } = string.Empty;

    public bool ReleasesRepositoryIsUnreachable => UpdateStatus is UpdateStatus.RepositoryUnreachable;

    public bool UpdateIsUnavailable => UpdateStatus is UpdateStatus.ApplicationUpToDate;

    public bool UpdateIsPending => UpdateStatus is UpdateStatus.UpdateAvailable;

    public string UpdateIsPendingMessage => $"{DeploymentManifest.ApplicationName} {LatestAvailableVersionDisplay} Is Available";

    public bool UpdateIsDownloading => UpdateStatus is UpdateStatus.UpdateDownloading;

    public bool UpdateIsRestarting => UpdateStatus is UpdateStatus.UpdateRestarting;

    public bool UpdateIsInstalling => UpdateStatus is UpdateStatus.UpdateDownloading or UpdateStatus.UpdateRestarting;

    public string UpdateDownloadMessage => $"Downloading {LatestAvailableVersionDisplay}" + ":";

    public string UpdateDownloadPercentDisplay => $"{(int) UpdateDownloadPercent}%";

    public bool UpdateCheckIsInProgress => UpdateStatus is UpdateStatus.CheckInProgress;

    public bool UpdateCheckIsIdle => UpdateStatus is not UpdateStatus.CheckInProgress;

    public bool SynchronisationIsIdle => SynchronisationIsActive is false;

    public bool MasterServerInputIsEnabled => UpdateCheckIsIdle && UpdateIsInstalling is false && SynchronisationIsIdle;

    public bool CanLaunchMapEditor => UpdateCheckIsIdle && UpdateIsInstalling is false && SynchronisationIsIdle && LaunchIsInProgress is false;

    public bool CanLaunchGameClient => UpdateCheckIsIdle && UpdateIsInstalling is false && MasterServerAddressIsValid && SynchronisationIsIdle && LaunchIsInProgress is false;

    public string LaunchMapEditorButtonText => "Open Map Editor";

    public string LaunchGameClientButtonText => "Play Heroes Of Newerth";

    partial void OnMasterServerAddressChanged(ComboBoxItem? oldValue, ComboBoxItem? newValue)
    {
        if (newValue is not null)
        {
            CanShowCustomMasterServerAddressField = newValue.Content?.ToString()?.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ?? false;

            MasterServerAddressIsValid = (MasterServerAddress?.Content?.ToString()?.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ?? false) is false
                ? true
                : (MasterServerAddress?.Content?.ToString()?.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ?? false) is true && string.IsNullOrWhiteSpace(CustomMasterServerAddress) is false
                    ? true : false;

            if (CanShowCustomMasterServerAddressField is false)
            {
                CustomMasterServerAddress = null;
                MasterServerAddressIsValid = true;

                LogLaunchParameters();
            }
        }
    }

    partial void OnCustomMasterServerAddressChanged(string? oldValue, string? newValue)
    {
        MasterServerAddressIsValid = (MasterServerAddress?.Content?.ToString()?.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ?? false) is true && string.IsNullOrWhiteSpace(CustomMasterServerAddress) is false
            ? true : false;
    }
}
