using LFGL.Dialogs;
using LFGL.Features.Library;
using LFGL.Features.Scanning;
using LFGL.Features.Settings;
using LFGL.Features.Updates;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using H.NotifyIcon;
using CommunityToolkit.Mvvm.Input;

namespace LFGL;

public sealed partial class MainWindow : Window
{
    private readonly IGameScannerService _scannerService;
    private readonly GameLibrary _library;
    private string _currentCategory = "All";
    private TaskbarIcon? _trayIcon;

    public MainWindow(IGameScannerService scannerService, GameLibrary library)
    {
        this.InitializeComponent();
        _scannerService = scannerService;
        _library = library;
        this.Title = "LFGL";
        
        // Set window icon
        SetWindowIcon();
        
        // Setup tray icon
        SetupTrayIcon();
        
        // Handle close-to-tray
        this.Closed += MainWindow_Closed;
        
        // Load existing library
        RefreshDisplay();
        
        // Get Steam username and show welcome animation
        var steamUser = SteamLibraryScanner.GetSteamUsername() ?? "Gamer";
        SteamUsernameText.Text = $"🎮 {steamUser}";
        
        // Play welcome animation
        _ = PlayWelcomeAnimationAsync(steamUser);
        
        // Auto-scan if enabled
        if (AppSettings.Instance.AutoScanOnStartup && _library.Games.Count == 0)
        {
            _ = AutoScanAsync();
        }
        
        // Check for updates in background
        _ = CheckForUpdatesAsync();
    }

    private void SetWindowIcon()
    {
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (System.IO.File.Exists(iconPath))
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.SetIcon(iconPath);
        }
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new TaskbarIcon();
        _trayIcon.ToolTipText = "LFGL - Game Launcher";
        
        // Set icon from file
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (System.IO.File.Exists(iconPath))
        {
            _trayIcon.Icon = new System.Drawing.Icon(iconPath);
        }
        
        // Create context menu
        var menu = new MenuFlyout();
        
        var showItem = new MenuFlyoutItem { Text = "Show LFGL" };
        showItem.Click += (s, e) => RestoreWindow();
        menu.Items.Add(showItem);
        
        menu.Items.Add(new MenuFlyoutSeparator());
        
        var exitItem = new MenuFlyoutItem { Text = "Exit" };
        exitItem.Click += (s, e) => ExitApplication();
        menu.Items.Add(exitItem);
        
        _trayIcon.ContextFlyout = menu;
        _trayIcon.LeftClickCommand = new RelayCommand(RestoreWindow);
    }

    private void RestoreWindow()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Windows.Win32.PInvoke.ShowWindow(new Windows.Win32.Foundation.HWND(hwnd), 
            Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_RESTORE);
        this.Activate();
    }

    private void ExitApplication()
    {
        _trayIcon?.Dispose();
        Application.Current.Exit();
    }

    private async Task CheckForUpdatesAsync()
    {
        var update = await AutoUpdater.CheckForUpdatesAsync();
        if (update != null)
        {
            var dialog = new ContentDialog
            {
                Title = $"Update Available: v{update.Version}",
                Content = $"A new version is available.\n\n{update.ReleaseNotes}",
                PrimaryButtonText = "Update Now",
                CloseButtonText = "Later",
                XamlRoot = this.Content.XamlRoot
            };
            
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrEmpty(update.DownloadUrl))
            {
                await AutoUpdater.DownloadAndInstallAsync(update.DownloadUrl, progress =>
                {
                    System.Diagnostics.Debug.WriteLine($"Download: {progress}%");
                });
            }
        }
    }

    private async Task AutoScanAsync()
    {
        if (_scannerService == null) return;
        
        LoadingOverlay.Visibility = Visibility.Visible;
        try
        {
            var games = await _scannerService.ScanAsync();
            var categorized = games.Select(g => g with 
            { 
                Category = g.ExecutablePath.StartsWith("steam://") ? "Steam" : "All" 
            });
            _library.SetScannedGames(categorized);
            RefreshDisplay();
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        if (AppSettings.Instance.CloseToTray)
        {
            args.Handled = true;
            // Hide the window - tray icon will remain
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Windows.Win32.PInvoke.ShowWindow(new Windows.Win32.Foundation.HWND(hwnd), Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_HIDE);
        }
        else
        {
            _trayIcon?.Dispose();
        }
    }

    private async Task PlayWelcomeAnimationAsync(string username)
    {
        UsernameText.Text = username;
        NavView.Visibility = Visibility.Collapsed;
        
        // Staggered fade-in animation
        await Task.Delay(300);
        await AnimateFadeIn(WelcomeEmoji, 400);
        await Task.Delay(200);
        await AnimateFadeIn(WelcomeText, 300);
        await Task.Delay(100);
        await AnimateFadeIn(UsernameText, 400);
        await Task.Delay(100);
        await AnimateFadeIn(SubtitleText, 300);
        
        // Hold for a moment
        await Task.Delay(1500);
        
        // Fade out overlay
        await AnimateFadeOut(WelcomeOverlay, 500);
        
        // Show main UI
        NavView.Visibility = Visibility.Visible;
        WelcomeOverlay.Visibility = Visibility.Collapsed;
    }

    private static async Task AnimateFadeIn(UIElement element, int durationMs)
    {
        var animation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        Storyboard.SetTarget(animation, element);
        Storyboard.SetTargetProperty(animation, "Opacity");
        storyboard.Begin();
        
        await Task.Delay(durationMs);
    }

    private static async Task AnimateFadeOut(UIElement element, int durationMs)
    {
        var animation = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        Storyboard.SetTarget(animation, element);
        Storyboard.SetTargetProperty(animation, "Opacity");
        storyboard.Begin();
        
        await Task.Delay(durationMs);
    }

    public MainWindow() 
    {
        this.InitializeComponent();
        _scannerService = null!;
        _library = new GameLibrary();
    }

    private void RefreshDisplay()
    {
        var games = _library.GetByCategory(_currentCategory).ToList();
        GamesGrid.ItemsSource = games;
        EmptyStatePanel.Visibility = games.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Card_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid card)
        {
            card.Scale = new System.Numerics.Vector3(1.03f, 1.03f, 1f);
            card.CenterPoint = new System.Numerics.Vector3((float)card.ActualWidth / 2, (float)card.ActualHeight / 2, 0);
        }
    }

    private void Card_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid card)
        {
            card.Scale = new System.Numerics.Vector3(1f, 1f, 1f);
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            LibraryView.Visibility = Visibility.Collapsed;
            ContentFrame.Visibility = Visibility.Visible;
            ContentFrame.Navigate(typeof(LFGL.Pages.SettingsPage));
            return;
        }

        // Show library view
        ContentFrame.Visibility = Visibility.Collapsed;
        LibraryView.Visibility = Visibility.Visible;

        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            _currentCategory = tag;
            RefreshDisplay();
        }
    }

    private async void ScanBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_scannerService == null) return;

        ScanBtn.IsEnabled = false;
        LoadingOverlay.Visibility = Visibility.Visible;

        try
        {
            var games = await _scannerService.ScanAsync();
            
            // Categorize scanned games
            var categorized = games.Select(g => g with 
            { 
                Category = g.ExecutablePath.StartsWith("steam://") ? "Steam" : "All" 
            });
            
            _library.SetScannedGames(categorized);
            RefreshDisplay();
        }
        catch (Exception ex)
        {
            var errorDialog = new ContentDialog
            {
                Title = "Scan Error",
                Content = ex.Message,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            await errorDialog.ShowAsync();
        }
        finally
        {
            ScanBtn.IsEnabled = true;
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private async void AddBtn_Click(object sender, RoutedEventArgs e)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        
        var dialog = new AddGameDialog
        {
            XamlRoot = this.Content.XamlRoot,
            WindowHandle = hwnd
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var iconPath = IconExtractor.ExtractAndCacheIcon(dialog.ExecutablePath, dialog.GameName) 
                          ?? "ms-appx:///Assets/Square44x44Logo.targetsize-24_altform-unplated.png";
            
            _library.AddManualGame(dialog.GameName, dialog.ExecutablePath, iconPath, dialog.Category);
            RefreshDisplay();
        }
    }

    private void GamesGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is GameModel game)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = game.ExecutablePath,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to launch {game.Name}: {ex.Message}");
            }
        }
    }
}
