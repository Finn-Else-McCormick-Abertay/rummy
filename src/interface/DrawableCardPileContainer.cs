using System;
using System.Linq;
using System.Text.RegularExpressions;
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

    /*
    protected override void OnCardMouseOver(CardDisplay display, bool entering) {
        if (CardPile is null || CardPile is not IDrawable || !AllowDraw) { return; }

        int index = GetChildCount() - display.GetIndex() - 1;
        HighlightedIndex = entering ? (CardPile is IDrawableMulti ? index : 0) : null;
    }
    */

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

    public override void _Input(InputEvent @event) {
        if (CardPile is null || GetChildCount() == 0 || CardPile is not IDrawable || !AllowDraw) { return; }

        if (@event is InputEventMouseButton) {
            var mouseButtonEvent = @event as InputEventMouseButton;

            // Left mouse button released
            if (HighlightedIndex is not null && mouseButtonEvent.ButtonIndex == MouseButton.Left && !mouseButtonEvent.Pressed) {
                NotifyDrew?.Invoke((int)HighlightedIndex + 1);
            }
        }
        if (@event is InputEventMouseMotion) {
            float relevantAxis(Vector2 point) => Direction switch {
                DirectionEnum.Horizontal => point.X, DirectionEnum.Vertical => point.Y, _ => throw new NotImplementedException()
            };
            float secondaryAxis(Vector2 point) => Direction switch {
                DirectionEnum.Horizontal => point.Y, DirectionEnum.Vertical => point.X, _ => throw new NotImplementedException()
            };

            var mouseMotionEvent = @event as InputEventMouseMotion;
            bool mouseOver = GetChildren().Any(display => display.GetNode<Control>("Shadow").GetGlobalRect().HasPoint(mouseMotionEvent.GlobalPosition));

            if (!mouseOver && HighlightedIndex != null) {
                Rect2 firstRect = GetChildren().First().GetNode<Control>("Shadow").GetGlobalRect(),
                    lastRect = GetChildren().Last().GetNode<Control>("Shadow").GetGlobalRect(),
                    highlightedRect = GetChildren().ElementAt((int)HighlightedIndex).GetNode<Control>("Shadow").GetGlobalRect();

                float startOnAxis = relevantAxis(firstRect.Position), endOnAxis = relevantAxis(lastRect.End);
                float startOffAxis = secondaryAxis(firstRect.Position), endOffAxis = secondaryAxis(firstRect.End);

                Vector2 highlightedOriginalStart = highlightedRect.Position - HighlightOffset,
                    highlightedOriginalEnd = highlightedRect.End - HighlightOffset;

                if (firstRect.Equals(highlightedRect)) {
                    startOnAxis = relevantAxis(highlightedOriginalStart);
                    startOffAxis = secondaryAxis(highlightedOriginalStart);
                    endOffAxis = secondaryAxis(highlightedOriginalEnd);
                }
                if (lastRect.Equals(highlightedRect)) {
                    endOnAxis = relevantAxis(highlightedOriginalEnd);
                }

                float mousePosOnAxis = relevantAxis(mouseMotionEvent.GlobalPosition), mousePosOffAxis = secondaryAxis(mouseMotionEvent.GlobalPosition);
                mouseOver |= mousePosOnAxis > startOnAxis && mousePosOnAxis < endOnAxis && mousePosOffAxis > startOffAxis && mousePosOffAxis < endOffAxis;
            }
            
            if (!mouseOver) { HighlightedIndex = null; }
            else {
                if (CardPile is IDrawableMulti) {
                    int hoveredIndex = -1;
                    GetChildren().ToList().ForEach(display => {
                        var cardEdge = relevantAxis(display.GetNode<Control>("Shadow").GetGlobalRect().Position);
                        if (relevantAxis(mouseMotionEvent.GlobalPosition) > cardEdge) { hoveredIndex = GetChildCount() - display.GetIndex() - 1; }
                    });
                    HighlightedIndex = hoveredIndex;
                }
                else { HighlightedIndex = 0; }
            }
        }
    }
}
