using System;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;
using Rummy.Game;

namespace Rummy.Interface;

[Tool]
public partial class DrawableCardPileContainer : CardPileContainer
{
    private bool _isMousedOver = false;
    private bool _didFocusJustEnter = false;

    [Export] public Vector2 HighlightOffset { get; set { field = value; QueueSort(); } } = new (0f, 30f);
    [Export] public Vector2 HighlightBelowOffset { get; set { field = value; QueueSort(); } } = new (0f, 0f);

    private NodePath _prevFocusBottom = null;

    public int? HighlightedIndex {
        get;
        set {
            if (CardPile is IDrawableMulti && HighlightedIndex == GetChildCount() - 1) { _prevFocusBottom = FocusNeighborBottom; }
            field = value;
            if (CardPile is IDrawableMulti && _prevFocusBottom is not null) {
                if (HighlightedIndex is null || HighlightedIndex == GetChildCount() - 1) { FocusNeighborBottom = _prevFocusBottom; }
                else { FocusNeighborBottom = null; }
            }
            QueueSort();
        }
    } = null;

    public void UpdatePrevFocus() {
        if (CardPile is not IDrawableMulti) { return; }
        if (HighlightedIndex is not null) {
            if (FocusNeighborBottom is not null) {
                _prevFocusBottom = FocusNeighborBottom;
                if (HighlightedIndex != GetChildCount() - 1) {
                    FocusNeighborBottom = null;
                }
            }
        }
    }

    public bool AllowDraw { get; set { field = value; if (!AllowDraw) HighlightedIndex = null; FocusMode = AllowDraw ? FocusModeEnum.All : FocusModeEnum.None; } }

    protected override void PostChildSorted(CardDisplay child) {
        int index = GetChildCount() - child.GetIndex() - 1;
        if (index == HighlightedIndex) {
            child.Position += HighlightOffset;
        }
        else if (index < HighlightedIndex) {
            child.Position += HighlightBelowOffset;
        }
    }

    public override void _Notification(int what) {
        base._Notification(what);
        if (CardPile is null || GetChildCount() == 0 || CardPile is not IDrawable || !AllowDraw) { return; }
        if (what == NotificationFocusEnter) {
            HighlightedIndex ??= 0;
            _didFocusJustEnter = true;
        }
        if (what == NotificationFocusExit) {
            HighlightedIndex = null;
            _didFocusJustEnter = false;
        }
    }

    public event Action<int> NotifyDrew;

    private void OnPressConfirmed() {
        if (HighlightedIndex is null) { return; }
        NotifyDrew?.Invoke((int)HighlightedIndex + 1);
    }

    public override void _Process(double delta) {
        base._Process(delta);
        if (Engine.IsEditorHint() || CardPile is null || GetChildCount() == 0 || CardPile is not IDrawable || !AllowDraw) { return; }

        if (Input.IsActionJustPressed(ActionName.Select)) {
            if (HasFocus() && HighlightedIndex is not null) { OnPressConfirmed(); } else if (HasFocus()) { HighlightedIndex = 0; }
        }

        if (HasFocus() && CardPile is IDrawableMulti) {
            if (_didFocusJustEnter) { _didFocusJustEnter = false; }
        }
    }

    public override void _GuiInput(InputEvent @event) {
        if (Engine.IsEditorHint() || CardPile is null || GetChildCount() == 0 || CardPile is not IDrawable || !AllowDraw) { return; }
        if (_didFocusJustEnter) { return; }

        if (@event.IsAction(Direction == DirectionEnum.Vertical ? ActionName.UI.Up : ActionName.UI.Left) && @event.IsPressed()) {
            if (HighlightedIndex + 1 < CardPile.Count) { HighlightedIndex++; }
        }
        if (@event.IsAction(Direction == DirectionEnum.Vertical ? ActionName.UI.Down : ActionName.UI.Right) && @event.IsPressed()) {
            if (HighlightedIndex - 1 >= 0) { HighlightedIndex--; }
        }
    }

    public override void _Input(InputEvent @event) {
        if (Engine.IsEditorHint() || CardPile is null || GetChildCount() == 0 || CardPile is not IDrawable || !AllowDraw) { return; }

        if (@event is InputEventMouseButton) {
            var mouseButtonEvent = @event as InputEventMouseButton;

            if (_isMousedOver && mouseButtonEvent.Pressed) {
                if (mouseButtonEvent.ButtonIndex == MouseButton.WheelUp) {
                    if (HighlightedIndex + 1 < CardPile.Count) { HighlightedIndex++; }
                }
                else if (mouseButtonEvent.ButtonIndex == MouseButton.WheelDown) {
                    if (HighlightedIndex - 1 >= 0) { HighlightedIndex--; }
                }
            }

            // Left mouse button released
            if (HighlightedIndex is not null && mouseButtonEvent.ButtonIndex == MouseButton.Left && !mouseButtonEvent.Pressed) {
                OnPressConfirmed();
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

            if (mouseOver) {
                if (CardPile is IDrawableMulti) {
                    int hoveredIndex = -1;
                    GetChildren().ToList().ForEach(display => {
                        var cardEdge = relevantAxis(display.GetNode<Control>("Shadow").GetGlobalRect().Position);
                        if (relevantAxis(mouseMotionEvent.GlobalPosition) > cardEdge) { hoveredIndex = GetChildCount() - display.GetIndex() - 1; }
                    });
                    HighlightedIndex = hoveredIndex;
                }
                else { HighlightedIndex = 0; }
                if (!_isMousedOver) { GrabFocus(); }
            }
            else {
                if (_isMousedOver) { HighlightedIndex = null; }
            }
            _isMousedOver = mouseOver;
        }
    }
}
