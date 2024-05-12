using Avalonia;
using Avalonia.Controls;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using System.Collections.ObjectModel;
using System.Diagnostics;

namespace WILLOWMAKER.ViewModels;

// This class needs to be partial, to create an injection point for the auto-generated code produced by CommunityToolkit.Mvvm.
// Dependencies > Analyzers > CommunityToolkit.Mvvm.SourceGenerators > CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator > WILLOWMAKER.ViewModels.MainViewModel.g.cs

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _gitHubLink = "https://github.com/Project-KONGOR-Open-Source";

    [ObservableProperty]
    private string? _webPortalLink = "https://kongor.online";

    [ObservableProperty]
    private string? _redditLink = "https://www.reddit.com/r/Project_KONGOR";

    [ObservableProperty]
    private string? _discordLink = "ლ(ಠ益ಠლ) But At What Cost?";

    [ObservableProperty]
    private ComboBoxItem? _masterServerAddress;

    [ObservableProperty]
    private string? _customMasterServerAddress;

    [ObservableProperty]
    private bool _canShowCustomMasterServerAddressField = false;

    [ObservableProperty]
    private bool _canLaunchGame = true;

    [ObservableProperty]
    private string? _logTextArea = $@"[{DateTime.UtcNow}] [PARAMETERS] -masterserver api.kongor.online -webserver api.kongor.online -messageserver api.kongor.online" + Environment.NewLine;

    [ObservableProperty]
    private int _caretIndexForAutoScroll = int.MaxValue;

    [RelayCommand]
    private void GoToURL(string url)
        => Process.Start(new ProcessStartInfo() { FileName = url, UseShellExecute = true });

    partial void OnMasterServerAddressChanged(ComboBoxItem? oldValue, ComboBoxItem? newValue)
    {
        if (newValue is not null)
        {
            CanShowCustomMasterServerAddressField = newValue.Content?.ToString()?.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ?? false;

            CanLaunchGame = (MasterServerAddress?.Content?.ToString()?.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ?? false) is false
                ? true
                : (MasterServerAddress?.Content?.ToString()?.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ?? false) is true && string.IsNullOrWhiteSpace(CustomMasterServerAddress) is false
                    ? true : false;

            if (CanShowCustomMasterServerAddressField is false)
            {
                CustomMasterServerAddress = null;

                LogTextArea += LogLaunchParameters();
            }
        }
    }

    partial void OnCustomMasterServerAddressChanged(string? oldValue, string? newValue)
    {
        CanLaunchGame = (MasterServerAddress?.Content?.ToString()?.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ?? false) is true && string.IsNullOrWhiteSpace(CustomMasterServerAddress) is false
            ? true : false;

        if (string.IsNullOrWhiteSpace(newValue) is false)
            LogTextArea += LogLaunchParameters();
    }

    private string LogLaunchParameters()
    {
        string address = MasterServerAddress?.Content?.ToString()?.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ?? false
            ? CustomMasterServerAddress ?? throw new NullReferenceException("Custom Master Server Address Is NULL")
            : MasterServerAddress?.Content?.ToString() ?? throw new NullReferenceException("Master Server Address Is NULL");

        return $@"[{DateTime.UtcNow}] [PARAMETERS] -masterserver {address} -webserver {address} -messageserver {address}" + Environment.NewLine;
    }
}
