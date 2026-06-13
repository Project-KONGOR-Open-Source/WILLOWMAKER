namespace WILLOWMAKER.Core.Views;

public partial class MainView : UserControl
{
    // The Duration For Which The Play Button Must Be Held To Arm The Hidden "Launch Without Synchronisation" Gesture
    private const double HoldDurationSeconds = 2.5;

    // The Delay Before The Hold Fill Becomes Visible, So That A Regular Quick Click Does Not Briefly Flash The Fill
    private const double FillStartDelaySeconds = 0.5;

    // The Style Class Marking A Press That Has Not Yet Lasted Long Enough To Count As A Deliberate Hold; It Is Set For The Initial Window (And Thus For The Whole Of A Quick Click) And Removed Once The Delay Elapses, At Which Point The Theme's Default Pressed Appearance Takes Over As The "Loading" Cue
    private const string HoldPendingClass = "HoldPending";

    private readonly DispatcherTimer launchGameClientHoldTimer;
    private readonly Stopwatch       launchGameClientHoldStopwatch = new ();

    // Set Once The Hold Reaches Its Full Duration So The Subsequent Pointer Release Does Not Also Trigger A Normal Launch
    private bool launchGameClientHoldCompleted;

    public MainView()
    {
        InitializeComponent();

        launchGameClientHoldTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
        launchGameClientHoldTimer.Tick += LaunchGameClientHoldTimer_Tick;

        // "Button" Marks Its Pointer Press And Release As Handled In Its Own Class Handlers, So The Gesture Handlers Are Registered With "handledEventsToo" To Still Receive Them
        LaunchGameClientButton.AddHandler(PointerPressedEvent,     LaunchGameClientButton_PointerPressed,     RoutingStrategies.Bubble, handledEventsToo: true);
        LaunchGameClientButton.AddHandler(PointerReleasedEvent,    LaunchGameClientButton_PointerReleased,    RoutingStrategies.Bubble, handledEventsToo: true);
        LaunchGameClientButton.AddHandler(PointerExitedEvent,      LaunchGameClientButton_PointerExited,      RoutingStrategies.Bubble, handledEventsToo: true);
        LaunchGameClientButton.AddHandler(PointerCaptureLostEvent, LaunchGameClientButton_PointerCaptureLost, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void LaunchGameClientButton_PointerPressed(object? sender, PointerPressedEventArgs arguments)
    {
        // Only A Primary (Left) Button Press Begins The Hold; Other Buttons Are Ignored
        if (arguments.GetCurrentPoint(LaunchGameClientButton).Properties.IsLeftButtonPressed is false)
            return;

        launchGameClientHoldCompleted = false;

        if (LaunchGameClientButton.Classes.Contains(HoldPendingClass) is false)
            LaunchGameClientButton.Classes.Add(HoldPendingClass);

        arguments.Pointer.Capture(LaunchGameClientButton);

        launchGameClientHoldStopwatch.Restart();
        launchGameClientHoldTimer.Start();
    }

    private void LaunchGameClientButton_PointerReleased(object? sender, PointerReleasedEventArgs arguments)
    {
        double elapsedSeconds = launchGameClientHoldStopwatch.Elapsed.TotalSeconds;
        bool   holdCompleted  = launchGameClientHoldCompleted;

        CancelLaunchGameClientHold();

        launchGameClientHoldCompleted = false;

        // A Completed Hold Has Already Armed The Bypass Launch; And Once The Press Has Lasted Past The Pending Window It Is A Committed Hold That The User Released Early, So Neither Case Fires A Normal Launch
        if (holdCompleted || elapsedSeconds >= FillStartDelaySeconds)
            return;

        // A Quick Press And Release Over The Button Is A Normal Click: Launch The Game Client With Synchronisation
        if (arguments.InitialPressMouseButton is MouseButton.Left && IsPointerWithinLaunchGameClientButton(arguments) && DataContext is MainViewModel viewModel)
            viewModel.LaunchGameClientCommand.Execute(null);
    }

    private void LaunchGameClientButton_PointerExited(object? sender, PointerEventArgs arguments)
        => CancelLaunchGameClientHold();

    private void LaunchGameClientButton_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs arguments)
        => CancelLaunchGameClientHold();

    private void LaunchGameClientHoldTimer_Tick(object? sender, EventArgs arguments)
    {
        double elapsedSeconds = launchGameClientHoldStopwatch.Elapsed.TotalSeconds;

        // The Fill Stays Empty Until The Start Delay Elapses, Then Grows Across The Remaining Hold Window So It Begins From Zero And Reaches Full Exactly At The Trigger
        double fillFraction = Math.Clamp((elapsedSeconds - FillStartDelaySeconds) / (HoldDurationSeconds - FillStartDelaySeconds), 0, 1);

        LaunchGameClientHoldFill.Width = fillFraction * LaunchGameClientHoldFillHost.Bounds.Width;

        // Once The Delay Has Elapsed The Press Counts As A Deliberate Hold, So Drop The Pending Marker And Let The Theme's Default Pressed Appearance Show Alongside The Fill As A "Loading" Cue
        if (elapsedSeconds >= FillStartDelaySeconds)
            LaunchGameClientButton.Classes.Remove(HoldPendingClass);

        if (elapsedSeconds < HoldDurationSeconds)
            return;

        launchGameClientHoldCompleted = true;

        CancelLaunchGameClientHold();

        if (DataContext is MainViewModel viewModel)
            viewModel.LaunchGameClientWithoutSynchronisationCommand.Execute(null);
    }

    private void CancelLaunchGameClientHold()
    {
        launchGameClientHoldTimer.Stop();
        launchGameClientHoldStopwatch.Reset();

        LaunchGameClientHoldFill.Width = 0;

        LaunchGameClientButton.Classes.Remove(HoldPendingClass);
    }

    private bool IsPointerWithinLaunchGameClientButton(PointerReleasedEventArgs arguments)
    {
        Point position = arguments.GetPosition(LaunchGameClientButton);

        return position.X >= 0 && position.Y >= 0 && position.X <= LaunchGameClientButton.Bounds.Width && position.Y <= LaunchGameClientButton.Bounds.Height;
    }

    private void CustomMasterServerAddress_LostFocus(object? sender, RoutedEventArgs arguments)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.LogCustomMasterServerAddressCommand.Execute(null);
        }
    }

    private void CustomMasterServerAddress_KeyDown(object? sender, KeyEventArgs arguments)
    {
        if (arguments.Key == Key.Enter && sender is InputElement inputElement)
        {
            Visual? parent = inputElement.GetVisualParent();

            while (parent is not null)
            {
                if (parent is InputElement focusableParent && focusableParent.Focusable)
                {
                    focusableParent.Focus();

                    break;
                }

                parent = parent.GetVisualParent();
            }

            arguments.Handled = true;
        }
    }

    private void Background_PointerPressed(object? sender, RoutedEventArgs arguments)
    {
        if (sender is InputElement element)
        {
            element.Focus();
        }
    }
}
