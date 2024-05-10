using CommunityToolkit.Mvvm.ComponentModel;

namespace WILLOWMAKER.ViewModels;

// This class needs to be partial, to create an injection point for the auto-generated code produced by CommunityToolkit.Mvvm.
// Dependencies > CommunityToolkit.Mvvm.SourceGenerators > CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator > WILLOWMAKER.ViewModels.MainViewModel.g.cs

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _gitHubLink = "https://github.com/Project-KONGOR-Open-Source";

    [ObservableProperty]
    private string? _webPortalLink = "https://kongor.online/";

    [ObservableProperty]
    private string? _redditLink = "https://www.reddit.com/r/Project_KONGOR/";

    [ObservableProperty]
    private string? _discordLink = "ლ(ಠ益ಠლ) But At What Cost?";

    [ObservableProperty]
    private string? _masterServerAddress;
}
