using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PresentAI;

public sealed class PresentationHub : INotifyPropertyChanged
{
    private Deck _deck = new();
    private int _slideIndex;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? StateChanged;

    public Deck Deck
    {
        get => _deck;
        set
        {
            _deck = value;
            _slideIndex = 0;
            Notify();
        }
    }

    public int SlideIndex
    {
        get => _slideIndex;
        set
        {
            var max = Math.Max(0, Deck.Slides.Count - 1);
            var next = Math.Clamp(value, 0, max);
            if (_slideIndex == next)
            {
                return;
            }

            _slideIndex = next;
            Notify();
        }
    }

    public Slide? CurrentSlide => Deck.Slides.Count == 0 ? null : Deck.Slides[SlideIndex];
    public Slide? NextSlide => Deck.Slides.Count == 0 || SlideIndex + 1 >= Deck.Slides.Count ? null : Deck.Slides[SlideIndex + 1];
    public string Counter => Deck.Slides.Count == 0 ? "0 / 0" : $"{SlideIndex + 1} / {Deck.Slides.Count}";

    public PresentationState Snapshot() => new() { Deck = Deck, SlideIndex = SlideIndex };

    public void Next() => SlideIndex++;
    public void Previous() => SlideIndex--;

    private void Notify([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentSlide)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NextSlide)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Counter)));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
