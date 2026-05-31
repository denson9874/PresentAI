using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System.ComponentModel;
using Windows.System;

namespace PresentAI;

public sealed partial class PresentationWindow : Window
{
    private readonly PresentationHub _hub;

    public PresentationWindow(PresentationHub hub)
    {
        InitializeComponent();
        Title = "PresentAI Presentation";

        _hub = hub;
        _hub.PropertyChanged += HubOnPropertyChanged;
        RootGrid.Loaded += (_, _) => RootGrid.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        Closed += OnClosed;
        Update();
    }

    private void HubOnPropertyChanged(object? sender, PropertyChangedEventArgs e) => DispatcherQueue.TryEnqueue(Update);

    private void Update()
    {
        var slide = _hub.CurrentSlide;
        SlideTitleText.Text = slide?.Title ?? _hub.Deck.Title;
        BulletList.ItemsSource = slide?.Bullets ?? [];
        DeckText.Text = _hub.Deck.Title;
        CounterText.Text = _hub.Counter;
    }

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
        else if (e.Key == VirtualKey.Escape)
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            e.Handled = true;
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _hub.PropertyChanged -= HubOnPropertyChanged;
    }
}
