using Godot;
using Rummy.Game;

namespace Rummy.Interface;

[Tool]
public partial class DrawableCardPileContainer : CardPileContainer
{
    private Vector2 _highlightOffset = new (0f, 30f);
    [Export] public Vector2 HighlightOffset { get => _highlightOffset; set { _highlightOffset = value; QueueSort(); } }

    private Vector2 _highlightBelowOffset = new (0f, 0f);
    [Export] public Vector2 HighlightBelowOffset { get => _highlightBelowOffset; set { _highlightBelowOffset = value; QueueSort(); } }

    private int? _highlightedIndex = null;
    private int? HighlightedIndex { get => _highlightedIndex; set { _highlightedIndex = value; QueueSort(); } }

    private bool _allowDraw = false;
    public bool AllowDraw { get => _allowDraw; set { _allowDraw = value; if (!AllowDraw) { HighlightedIndex = null; } } }

    protected override void PostChildSorted(CardDisplay child) {
        int index = GetChildCount() - child.GetIndex() - 1;
        if (index == HighlightedIndex) {
            child.Position += HighlightOffset;
        }
        else if (index < HighlightedIndex) {
            child.Position += HighlightBelowOffset;
        }
    }

    protected override void OnCardMouseOver(CardDisplay display, bool entering) {
        if (CardPile is null || CardPile is not IDrawable || !AllowDraw) { return; }

        int index = GetChildCount() - display.GetIndex() - 1;
        HighlightedIndex = entering ? (CardPile is IDrawableMulti ? index : 0) : null;
    }

    protected override void OnCardScroll(CardDisplay display, MouseButton buttonIndex) {
        if (CardPile is null || CardPile is not IDrawableMulti) { return; }

        if (buttonIndex == MouseButton.WheelUp) {
            if (HighlightedIndex + 1 < CardPile.Count) {
                HighlightedIndex++;
            }
        }
        else if (buttonIndex == MouseButton.WheelDown) {
            if (HighlightedIndex - 1 >= 0) {
                HighlightedIndex--;
            }
        }
    }

    public delegate void NotifyDrewAction(int count);
    public event NotifyDrewAction NotifyDrew;

    protected override void OnCardClicked(CardDisplay display, MouseButton buttonIndex, bool pressed) {
        if (CardPile is null || CardPile is not IDrawable || !AllowDraw) { return; }

        if (buttonIndex == MouseButton.Left && !pressed) {
            NotifyDrew?.Invoke((int)HighlightedIndex + 1);
        }
    }
}
