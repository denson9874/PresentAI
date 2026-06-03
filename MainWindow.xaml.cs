using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Graphics;
using Windows.UI;
using Windows.System;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PresentAI;

public sealed partial class MainWindow : Window
{
    private readonly PresentationHub _hub = new();
    private readonly DeckGenerator _generator = new();
    private readonly CastServer _castServer;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTimeOffset? _timerStartedAt;
    private PresentationWindow? _presentationWindow;
    private bool _presentationFullScreen;
    private AppColorScheme _colorScheme = AppColorScheme.PresentAI;
    private AppBackdropMode _backdropMode = AppBackdropMode.Solid;

    public MainWindow()
    {
        InitializeComponent();

        Title = "PresentAI";
        AppWindow.Resize(new SizeInt32(1280, 840));
        InitializeThemeControls();
        ApplyTheme();

        _castServer = new CastServer(_hub);
        _castServer.Start();
        ReceiverUrlBox.Text = $"Local: {_castServer.ReceiverUrl}{Environment.NewLine}Network: {_castServer.NetworkReceiverUrl}";

        _hub.StateChanged += (_, _) => DispatcherQueue.TryEnqueue(UpdateSpeakerView);
        SlideCountSlider.ValueChanged += (_, _) => SlideCountText.Text = $"{(int)SlideCountSlider.Value} slides";
        _timer.Tick += (_, _) => UpdateTimer();

        RootGrid.Loaded += (_, _) =>
        {
            RefreshDisplays();
            GenerateLocalDraft();
            RootGrid.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        };
        Closed += OnClosed;
    }

    private async void GenerateButton_OnClick(object sender, RoutedEventArgs e)
    {
        GenerateButton.IsEnabled = false;
        StatusText.Text = "Generating deck...";

        try
        {
            var request = new DeckGenerationRequest
            {
                Provider = SelectedProvider(),
                ApiKey = ApiKeyBox.Password,
                Model = ModelBox.Text,
                Prompt = PromptBox.Text,
                SlideCount = (int)SlideCountSlider.Value
            };

            _hub.Deck = await _generator.GenerateAsync(request, CancellationToken.None);
            StatusText.Text = ApiKeyBox.Password.Length == 0
                ? "Generated a local draft. Add an API key for full OpenAI/Gemini output."
                : "Deck generated.";
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
        finally
        {
            GenerateButton.IsEnabled = true;
        }
    }

    private void OpenPresentationButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_presentationWindow is null)
        {
            _presentationWindow = new PresentationWindow(_hub);
            _presentationWindow.Closed += (_, _) =>
            {
                _presentationWindow = null;
                _presentationFullScreen = false;
            };
        }

        PositionPresentationWindow(_presentationWindow);
        _presentationWindow.Activate();
    }

    private void ToggleFullScreenButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_presentationWindow is null)
        {
            OpenPresentationButton_OnClick(sender, e);
            return;
        }

        _presentationFullScreen = !_presentationFullScreen;
        _presentationWindow.AppWindow.SetPresenter(_presentationFullScreen
            ? AppWindowPresenterKind.FullScreen
            : AppWindowPresenterKind.Overlapped);
    }

    private void PreviousButton_OnClick(object sender, RoutedEventArgs e) => _hub.Previous();
    private void NextButton_OnClick(object sender, RoutedEventArgs e) => _hub.Next();

    private void TimerButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_timerStartedAt is null)
        {
            _timerStartedAt = DateTimeOffset.Now;
            _timer.Start();
            TimerButton.Content = "Reset Timer";
        }
        else
        {
            _timerStartedAt = null;
            _timer.Stop();
            TimerText.Text = "00:00";
            TimerButton.Content = "Start Timer";
        }
    }

    private void OpenReceiverButton_OnClick(object sender, RoutedEventArgs e) => _castServer.OpenReceiver();
    private void OpenCastButton_OnClick(object sender, RoutedEventArgs e) => _castServer.OpenChromeCastWindow();

    private async void ImportHtmlButton_OnClick(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add(".html");
        picker.FileTypeFilter.Add(".htm");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            var html = await File.ReadAllTextAsync(file.Path);
            var deck = HtmlDeckImporter.Import(html, file.Name);
            if (deck.Slides.Count == 0)
            {
                StatusText.Text = "No slides found in the selected HTML file.";
                return;
            }

            _hub.Deck = deck;
            StatusText.Text = $"Imported {deck.Slides.Count} slides from {file.Name}.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Import failed: {ex.Message}";
        }
    }

    private async void ExportMarkdownButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_hub.Deck.Slides.Count == 0)
        {
            StatusText.Text = "Generate a deck before exporting.";
            return;
        }

        var picker = new FileSavePicker
        {
            SuggestedFileName = SanitizeFileName(_hub.Deck.Title),
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeChoices.Add("Markdown file", [".md"]);
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        await File.WriteAllTextAsync(file.Path, BuildMarkdown(_hub.Deck), Encoding.UTF8);
        StatusText.Text = $"Exported {file.Path}";
    }

    private void ProviderBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModelBox is null)
        {
            return;
        }

        ModelBox.Text = SelectedProvider().Equals("Gemini", StringComparison.OrdinalIgnoreCase)
            ? "gemini-1.5-flash"
            : "gpt-4.1-mini";
    }

    private void ThemeBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ColorSchemeBox?.SelectedItem is ThemeChoice<AppColorScheme> colorChoice)
        {
            _colorScheme = colorChoice.Value;
        }

        if (BackdropBox?.SelectedItem is ThemeChoice<AppBackdropMode> backdropChoice)
        {
            _backdropMode = backdropChoice.Value;
        }

        ApplyTheme();
    }

    private void InitializeThemeControls()
    {
        ColorSchemeBox.ItemsSource = Enum.GetValues<AppColorScheme>()
            .Select(value => new ThemeChoice<AppColorScheme>(ThemeManager.DisplayName(value), value))
            .ToList();
        BackdropBox.ItemsSource = Enum.GetValues<AppBackdropMode>()
            .Select(value => new ThemeChoice<AppBackdropMode>(ThemeManager.DisplayName(value), value))
            .ToList();

        ColorSchemeBox.SelectedIndex = 0;
        BackdropBox.SelectedIndex = 0;
    }

    private void ApplyTheme()
    {
        var palette = ThemeManager.PaletteFor(_colorScheme);
        SystemBackdrop = ThemeManager.CreateBackdrop(_backdropMode);

        RootGrid.Background = ThemeManager.Brush(WithOpacity(palette.Page, _backdropMode == AppBackdropMode.Solid ? (byte)255 : (byte)186));
        SidebarPanel.Background = ThemeManager.Brush(palette.Sidebar);
        NotesPanel.Background = ThemeManager.Brush(palette.Panel);
        PreviewPanel.Background = ThemeManager.Brush(palette.Slide);

        GenerateButton.Background = ThemeManager.Brush(palette.Primary);
        GenerateButton.Foreground = ThemeManager.Brush(Color.FromArgb(255, 255, 255, 255));

        ApplyThemeRecursive(RootGrid, palette);

        SlideTitleText.Foreground = ThemeManager.Brush(palette.LightText);
        VisualHintText.Foreground = ThemeManager.Brush(palette.Muted);
        NotesText.Foreground = ThemeManager.Brush(palette.Text);
        NextSlideText.Foreground = ThemeManager.Brush(palette.Text);
        DeckTitleText.Foreground = ThemeManager.Brush(palette.Text);
        DeckSubtitleText.Foreground = ThemeManager.Brush(palette.Muted);
        CounterText.Foreground = ThemeManager.Brush(palette.Muted);
        TimerText.Foreground = ThemeManager.Brush(palette.Text);
    }

    private static void ApplyThemeRecursive(DependencyObject element, ThemePalette palette)
    {
        switch (element)
        {
            case TextBlock textBlock:
                textBlock.Foreground = IsInsidePanel(textBlock, "SidebarPanel") || IsInsidePanel(textBlock, "PreviewPanel")
                    ? ThemeManager.Brush(palette.LightText)
                    : ThemeManager.Brush(palette.Text);
                break;
            case Button button when button.Name != "GenerateButton":
                button.Background = ThemeManager.Brush(palette.Panel);
                button.Foreground = ThemeManager.Brush(palette.Text);
                break;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            ApplyThemeRecursive(VisualTreeHelper.GetChild(element, i), palette);
        }
    }

    private static bool IsInsidePanel(DependencyObject element, string panelName)
    {
        var current = element;
        while (current is not null)
        {
            if (current is FrameworkElement frameworkElement && frameworkElement.Name == panelName)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private static Color WithOpacity(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    private void GenerateLocalDraft()
    {
        _hub.Deck = _generator
            .GenerateAsync(new DeckGenerationRequest
            {
                Prompt = PromptBox.Text,
                SlideCount = (int)SlideCountSlider.Value
            }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    private void UpdateSpeakerView()
    {
        var slide = _hub.CurrentSlide;
        DeckTitleText.Text = _hub.Deck.Title;
        DeckSubtitleText.Text = _hub.Deck.Subtitle;
        CounterText.Text = _hub.Counter;
        SlideTitleText.Text = slide?.Title ?? "No slide selected";
        BulletList.ItemsSource = slide?.Bullets ?? [];
        NotesText.Text = string.IsNullOrWhiteSpace(slide?.Notes) ? "No speaker notes for this slide." : slide.Notes;
        VisualHintText.Text = string.IsNullOrWhiteSpace(slide?.VisualHint) ? "" : $"Visual: {slide.VisualHint}";
        NextSlideText.Text = _hub.NextSlide?.Title ?? "End of deck";
    }

    private void RefreshDisplays()
    {
        var screens = NativeDisplays.GetDisplays();

        DisplayBox.ItemsSource = screens;
        DisplayBox.SelectedItem = screens.FirstOrDefault(s => !s.Primary) ?? screens.FirstOrDefault();
    }

    private void PositionPresentationWindow(Window window)
    {
        if (DisplayBox.SelectedItem is not DisplayInfo display)
        {
            return;
        }

        window.AppWindow.MoveAndResize(new RectInt32(display.Left, display.Top, display.Width, display.Height));
        window.AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        _presentationFullScreen = true;
    }

    private void UpdateTimer()
    {
        if (_timerStartedAt is null)
        {
            return;
        }

        var elapsed = DateTimeOffset.Now - _timerStartedAt.Value;
        TimerText.Text = $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
    }

    private string SelectedProvider() => ((ComboBoxItem)ProviderBox.SelectedItem).Content?.ToString() ?? "OpenAI";

    private void RootGrid_OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Right or VirtualKey.PageDown or VirtualKey.Space)
        {
            _hub.Next();
            e.Handled = true;
        }
        else if (e.Key is VirtualKey.Left or VirtualKey.PageUp or VirtualKey.Back)
        {
            _hub.Previous();
            e.Handled = true;
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _castServer.Dispose();
        _presentationWindow?.Close();
    }

    private static string BuildMarkdown(Deck deck)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {deck.Title}");
        if (!string.IsNullOrWhiteSpace(deck.Subtitle))
        {
            builder.AppendLine();
            builder.AppendLine(deck.Subtitle);
        }

        for (var i = 0; i < deck.Slides.Count; i++)
        {
            var slide = deck.Slides[i];
            builder.AppendLine();
            builder.AppendLine($"## Slide {i + 1}: {slide.Title}");
            foreach (var bullet in slide.Bullets)
            {
                builder.AppendLine($"- {bullet}");
            }

            builder.AppendLine();
            builder.AppendLine("### Speaker Notes");
            builder.AppendLine(slide.Notes);

            if (!string.IsNullOrWhiteSpace(slide.VisualHint))
            {
                builder.AppendLine();
                builder.AppendLine($"Visual: {slide.VisualHint}");
            }
        }

        return builder.ToString();
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "presentation" : cleaned;
    }

    private sealed record DisplayInfo(int Index, string DeviceName, bool Primary, int Left, int Top, int Width, int Height)
    {
        public string Label => $"{(Primary ? "Primary" : "External")} display {Index + 1} ({Width} x {Height})";
    }

    private sealed record ThemeChoice<T>(string Label, T Value);

    private static class NativeDisplays
    {
        private const int MonitorDefaultToNull = 0;

        public static List<DisplayInfo> GetDisplays()
        {
            var displays = new List<DisplayInfo>();
            var primaryLeft = GetSystemMetrics(SystemMetric.SM_XVIRTUALSCREEN);
            var primaryTop = GetSystemMetrics(SystemMetric.SM_YVIRTUALSCREEN);

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
            {
                var info = new MonitorInfoEx();
                info.cbSize = Marshal.SizeOf<MonitorInfoEx>();
                if (GetMonitorInfo(monitor, ref info))
                {
                    var index = displays.Count;
                    var rect = info.rcMonitor;
                    var primary = info.dwFlags.HasFlag(MonitorInfoFlags.MONITORINFOF_PRIMARY);
                    displays.Add(new DisplayInfo(
                        index,
                        info.szDevice,
                        primary,
                        rect.Left,
                        rect.Top,
                        rect.Right - rect.Left,
                        rect.Bottom - rect.Top));
                }

                return true;
            }, IntPtr.Zero);

            if (displays.Count == 0)
            {
                displays.Add(new DisplayInfo(0, "Primary", true, primaryLeft, primaryTop,
                    GetSystemMetrics(SystemMetric.SM_CXSCREEN),
                    GetSystemMetrics(SystemMetric.SM_CYSCREEN)));
            }

            return displays;
        }

        private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clipRect, MonitorEnumProc callback, IntPtr data);

        [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(SystemMetric index);

        private enum SystemMetric
        {
            SM_CXSCREEN = 0,
            SM_CYSCREEN = 1,
            SM_XVIRTUALSCREEN = 76,
            SM_YVIRTUALSCREEN = 77
        }

        [Flags]
        private enum MonitorInfoFlags : uint
        {
            MONITORINFOF_PRIMARY = 1
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MonitorInfoEx
        {
            public int cbSize;
            public Rect rcMonitor;
            public Rect rcWork;
            public MonitorInfoFlags dwFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
