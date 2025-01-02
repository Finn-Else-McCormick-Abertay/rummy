using Godot;
using Microsoft.VisualBasic;
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
    private Control dragPlaceholder;

    private bool isDislocated = false;

    private readonly Vector2 SelectedOffset = new(0f, -30f);

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
        dragPlaceholder = new Control {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
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
                GD.Print("Click Drag Begin");
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
        GD.Print("Dislocate ", isDislocated ? "Failure" : "Success");
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
        GD.Print("Undislocate ", !isDislocated ? "Failure" : "Success");
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
}
