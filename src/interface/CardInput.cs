using Godot;
using Rummy.Game;
using Rummy.Interface;

public partial class CardInput : MarginContainer
{
    [Export] private float selectedOffsetDistance = 30f;

    private readonly static PackedScene CardDisplayScene = ResourceLoader.Load<PackedScene>("res://scenes/card_display.tscn");

    public CardDisplay Display { get; private set; }
    
    private partial class Placeholder : Control {
        public Placeholder() {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
        }
        public CardInput Card;
    }
    private Placeholder placeholder;
    
    private Vector2 _snapOffset = new();
    private Vector2 SnapOffset { get => _snapOffset; set { _snapOffset = value; CallDeferred("SnapToPlaceholder"); } }
    
    private void SnapToPlaceholder() {
        if (!Dislocated) { return; }
        GlobalPosition = placeholder.GlobalPosition + SnapOffset;
    }

    private bool _selected = false;
    public bool Selected { get => _selected; set { SetSelected(value); } }

    void SetSelected(bool selectedParam, bool force = false) {
        var wasSelected = Selected;
        _selected = selectedParam;
        if (!Selected) {
            SnapOffset = new();
            Undislocate();
        }
        else {
            if (!wasSelected || force) {
                Dislocate();
                SnapOffset = new(0f, -selectedOffsetDistance);
            }
        }
    }
    
    private bool _dislocated = false;
    private bool Dislocated { get => _dislocated; }
    
    void Dislocate() {
        if (Dislocated) { return; }
        _dislocated = true;

        bool hasFocus = Display.HasFocus(); var globalPos = GlobalPosition;
        var parent = GetParent(); var index = GetIndex();
        parent.RemoveChild(this); parent.GetTree().Root.AddChild(this); Owner = parent.GetTree().Root;
        parent.AddChild(placeholder); placeholder.Owner = parent; parent.MoveChild(placeholder, index);
        GlobalPosition = globalPos; if (hasFocus) { Display.GrabFocus(); }
    }

    void Undislocate() {
        if (!Dislocated) { return; }
        _dislocated = false;

        bool hasFocus = Display.HasFocus();
        var parent = placeholder.GetParent(); var index = placeholder.GetIndex();
        parent.RemoveChild(placeholder); GetTree().Root.RemoveChild(this);
        parent.AddChild(this); Owner = parent; parent.MoveChild(this, index);
        if (hasFocus) { Display.GrabFocus(); }
    }

    private bool shouldDrag = false, isDragging = false, clickDragging = false;
    private Vector2 dragBeginPosition, dragGrabOffset;

    public override void _Ready() {
        AddThemeConstantOverride("margin_left", 0); AddThemeConstantOverride("margin_right", 0);
        AddThemeConstantOverride("margin_top", 0); AddThemeConstantOverride("margin_bottom", 0);

        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        Display = (CardDisplay)CardDisplayScene.Instantiate();
        AddChild(Display);
        Display.Owner = this;
        Display.FocusMode = FocusModeEnum.All;
        
        Display.GuiInput += OnDisplayGuiInput;
    }

    public override void _EnterTree() {
        placeholder = new() {
            Card = this
        };
    }
    public override void _ExitTree() {
        placeholder.QueueFree();
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
                dragGrabOffset = GlobalPosition - GetGlobalMousePosition();
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
                bool wasClick = (GetGlobalMousePosition() - dragBeginPosition).Length() < 4f;
                CallDeferred("SetSelected", wasClick ? !Selected : Selected, true);
            }
            else if (Display.HasFocus()) {
                Selected = !Selected;
            }
        }

        if (isDragging) {
            if (clickDragging) {
                GlobalPosition = GetGlobalMousePosition() + dragGrabOffset;
            }

            var container = placeholder.GetParent();
            var placeholderIndex = placeholder.GetIndex();

            for (int i = 0; i < 2; ++i) {
                bool toLeft = i == 0;
                var neighbourIndex = placeholderIndex + (toLeft ? -1 : 1);
                if (neighbourIndex < 0 || neighbourIndex >= container.GetChildCount()) { continue; }

                var neighbour = (Control)container.GetChild(neighbourIndex);
                var boundX = neighbour.GlobalPosition.X + neighbour.Size.X / 2 * (toLeft ? 1 : -1);

                if ((toLeft && (GlobalPosition.X < boundX)) || (!toLeft && (GlobalPosition.X > boundX))) {
                    container.MoveChild(placeholder, neighbourIndex);
                    if (neighbour is Placeholder neighbourPlaceholder) {
                        var card = neighbourPlaceholder.Card;
                        card.CallDeferred("SnapToPlaceholder");
                    }
                }
            }
        }
    }
}
