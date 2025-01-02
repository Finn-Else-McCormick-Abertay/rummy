using Godot;
using Microsoft.VisualBasic;
using Rummy.Game;
using Rummy.Interface;
using System;

public partial class CardInput : MarginContainer
{
    private readonly static PackedScene CardDisplayScene = ResourceLoader.Load<PackedScene>("res://scenes/card_display.tscn");

    private CardDisplay _display;
    public CardDisplay Display { get => _display; }

    private bool selected = false;

    private bool shouldDrag = false, isDragging = false, clickDragging = false;
    private Vector2 dragOffset, dragBeginPosition;

    private bool isDislocated = false;

    private readonly Vector2 SelectedOffset = new(0f, -30f);

    private partial class Placeholder : Control {
        public Placeholder() {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
        }
        public CardInput Card;
    }
    private Placeholder dragPlaceholder;


    public override void _Ready() {
        AddThemeConstantOverride("margin_left", 0); AddThemeConstantOverride("margin_right", 0);
        AddThemeConstantOverride("margin_top", 0); AddThemeConstantOverride("margin_bottom", 0);

        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _display = (CardDisplay)CardDisplayScene.Instantiate();
        AddChild(Display);
        Display.Owner = this;
        Display.FocusMode = FocusModeEnum.All;
        
        Display.GuiInput += OnDisplayGuiInput;
    }

    public override void _EnterTree() {
        dragPlaceholder = new() {
            Card = this
        };
    }
    public override void _ExitTree() {
        dragPlaceholder.QueueFree();
    }

    private void OnDisplayGuiInput(InputEvent @event) {
        if (@event.IsAction("click") && @event.IsPressed()) {
            shouldDrag = true;
            clickDragging = true;
        }
    }
    
    public override void _Input(InputEvent @event)
    {
        if (shouldDrag && !isDragging) {
            if (clickDragging && @event is InputEventMouseMotion) {
                isDragging = true;
                Dislocate();
                dragOffset = GlobalPosition - GetGlobalMousePosition();
                dragBeginPosition = GetGlobalMousePosition();
            }
        }
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("select") && Display.HasFocus()) {
            shouldDrag = true;
        }

        if (Input.IsActionJustReleased("select") || (clickDragging && Input.IsActionJustReleased("click"))) {
            shouldDrag = false;
            clickDragging = false;
            if (isDragging) {
                isDragging = false;
                Undislocate();
                var dragLength = (GetGlobalMousePosition() - dragBeginPosition).Length();
                var nextSelectedState = selected;
                if (dragLength < 4f) {
                    nextSelectedState = !selected;
                }
                CallDeferred("SetSelected", nextSelectedState, true);
            }
            else if (Display.HasFocus()) {
                SetSelected(!selected);
            }
        }

        if (isDragging) {
            GlobalPosition = GetGlobalMousePosition() + dragOffset;

            var container = dragPlaceholder.GetParent();
            var placeholderIndex = dragPlaceholder.GetIndex();
            if (placeholderIndex > 0) {
                var leftNeighbour = (Control)container.GetChild(placeholderIndex - 1);
                if (GlobalPosition.X < leftNeighbour.GlobalPosition.X + leftNeighbour.Size.X / 2) {
                    container.MoveChild(dragPlaceholder, placeholderIndex - 1);
                    if (leftNeighbour is Placeholder placeholder) {
                        var card = placeholder.Card;
                        card.CallDeferred("SnapToPlaceholder");
                    }
                }
            }
            if (placeholderIndex < container.GetChildCount() - 1) {
                var rightNeighbour = (Control)container.GetChild(placeholderIndex + 1);
                if (GlobalPosition.X > rightNeighbour.GlobalPosition.X - rightNeighbour.Size.X / 2) {
                    container.MoveChild(dragPlaceholder, placeholderIndex + 1);
                    if (rightNeighbour is Placeholder placeholder) {
                        var card = placeholder.Card;
                        card.CallDeferred("SnapToPlaceholder");
                    }
                }
            }
        }
    }

    void SetSelected(bool selectedParam, bool force = false) {
        var wasSelected = selected;
        selected = selectedParam;
        if (!selected) {
            Undislocate();
        }
        else {
            if (!wasSelected || force) {
                Dislocate();
                Position += SelectedOffset;
            }
        }
    }

    void Dislocate() {
        if (isDislocated) { return; }
        isDislocated = true;

        var globalPos = GlobalPosition;
        bool hasFocus = Display.HasFocus();

        var root = GetTree().Root; var parent = GetParent(); var index = GetIndex();
        parent.RemoveChild(this);
        root.AddChild(this);
        parent.AddChild(dragPlaceholder);
        dragPlaceholder.Owner = parent;
        parent.MoveChild(dragPlaceholder, index);

        GlobalPosition = globalPos;
        if (hasFocus) { Display.GrabFocus(); }
    }

    void Undislocate() {
        if (!isDislocated) { return; }
        isDislocated = false;

        bool hasFocus = Display.HasFocus();

        var parent = dragPlaceholder.GetParent();
        var index = dragPlaceholder.GetIndex();
        parent.RemoveChild(dragPlaceholder);
        GetTree().Root.RemoveChild(this);
        parent.AddChild(this);
        parent.MoveChild(this, index);

        if (hasFocus) { Display.GrabFocus(); }
    }

    private void SnapToPlaceholder() {
        GlobalPosition = dragPlaceholder.GlobalPosition + SelectedOffset;
    }
}
