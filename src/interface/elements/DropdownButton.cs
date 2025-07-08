using System;
using System.Linq;
using Godot;
using Rummy.Util;

namespace Rummy.Interface;

[Tool, GlobalClass]
public partial class DropdownButton : Container
{
    public DropdownButton() { ThemeTypeVariation = nameof(DropdownButton); }
    public enum DropdownAlignmentEnum { Begin, Center, End }
    public enum DropdownSizeConstraintEnum { LessThan, ExactMatch, Unconstrained }

    [Export] public bool DropdownOpen { get; set { if (DropdownOpen != value && value == false) OnDropdownClosed(); field = value; QueueSort(); } }

    [ExportGroup("Dropdown Behaviour", "Dropdown")]
    [Export] public Side DropdownSide { get; set { field = value; QueueSort(); } } = Side.Bottom;
    [Export] public DropdownAlignmentEnum DropdownAlignment { get; set { field = value; QueueSort(); } } = DropdownAlignmentEnum.Begin;
    [Export] public DropdownSizeConstraintEnum DropdownSizeContraints { get; set { field = value; QueueSort(); } } = DropdownSizeConstraintEnum.ExactMatch;
    [Export] public double DropdownSeparation { get; set { field = value; QueueSort(); } } = 0;

    [ExportCategory("Button")]
    [Export] string Text { get; set { field = value; SetButtonValue(Button.PropertyName.Text, value); } }
    [Export] Texture2D Icon { get; set { field = value; SetButtonValue(Button.PropertyName.Icon, value); } }
    [Export] bool Flat { get; set { field = value; SetButtonValue(Button.PropertyName.Flat, value); } }
    [Export] bool Disabled { get; set { field = value; SetButtonValue(BaseButton.PropertyName.Disabled, value); } }

    [ExportGroup("Text Behaviour")]
    [Export] HorizontalAlignment Alignment { get; set { field = value; SetButtonValue(Button.PropertyName.Alignment, value); } }
    [Export] TextServer.OverrunBehavior TextOverrunBehaviour { get; set { field = value; SetButtonValue(Button.PropertyName.TextOverrunBehavior, value); } }
    [Export] TextServer.AutowrapMode AutowrapMode { get; set { field = value; SetButtonValue(Button.PropertyName.AutowrapMode, value); } }
    [Export] bool ClipText { get; set { field = value; SetButtonValue(Button.PropertyName.ClipText, value); } }

    [ExportGroup("Icon Behaviour")]
    [Export] HorizontalAlignment IconAlignment { get; set { field = value; SetButtonValue(Button.PropertyName.IconAlignment, value); } }
    [Export] VerticalAlignment VerticalIconAlignment { get; set { field = value; SetButtonValue(Button.PropertyName.VerticalIconAlignment, value); } }
    [Export] bool ExpandIcon { get; set { field = value; SetButtonValue(Button.PropertyName.ExpandIcon, value); } }

    public Button Button { get; private set; }
    private void SetButtonValue<T>(StringName property, T value) => this.OnReady(() => Button?.Set(property, Variant.From(value)));

    public override void _Ready() {
        if (Engine.IsEditorHint()) return;
        DropdownOpen = false;
        GetViewport().GuiFocusChanged += OnGuiFocusChanged;
    }

    public override void _EnterTree() {
        Button = new Button() { Text = Text, Icon = Icon, Disabled = Disabled };
        Button.Connect(BaseButton.SignalName.Pressed, OnButtonPressed);
        AddChild(Button, false, InternalMode.Front);
        Button.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Button.ThemeTypeVariation = ThemeTypeVariation;
    }
    public override void _ExitTree() {
        Button?.TryDisconnect(BaseButton.SignalName.Pressed, OnButtonPressed);
        RemoveChild(Button); Button?.QueueFree();
        Button = null;
    }

    public override void _Notification(int what) {
        if (what == NotificationSortChildren) {
            // Update button transform
            Button.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

            var directControlChildren = this.FindChildrenOfType<Control>(false); // Get all control children, excluding the button (which is internal)
            if (Engine.IsEditorHint()) DropdownOpen = directControlChildren.Any(x => x.Visible); // Update DropdownOpen state when toggling visibility of children directly in editor

            directControlChildren.ForEach(child => child.Visible = DropdownOpen); // Set visibility
            if (DropdownOpen) directControlChildren.ForEach(UpdateDropdownTransform); // Update transform
        }
    }

    public void UpdateDropdownTransform(Control dropdown) {
        var dropdownMinimumSize = dropdown.GetCombinedMinimumSize();

        // Clamp dropdown size based on size constraints
        if (DropdownSizeContraints == DropdownSizeConstraintEnum.Unconstrained) dropdown.Size = dropdownMinimumSize;
        else {
            Vector2 thresholdSize = DropdownSizeContraints switch {
                DropdownSizeConstraintEnum.LessThan => new(MathF.Min(dropdownMinimumSize.X, Size.X), MathF.Min(dropdownMinimumSize.Y, Size.Y)),
                _ => Size
            };
            dropdown.Size = DropdownSide switch {
                Side.Top or Side.Bottom => new(thresholdSize.X, dropdownMinimumSize.Y),
                Side.Left or Side.Right => new(dropdownMinimumSize.X, thresholdSize.Y)
            };
        }

        dropdown.Position = new(
            (float)(DropdownSide switch {
                Side.Left => -dropdown.Size.X - DropdownSeparation,
                Side.Right => Size.X + DropdownSeparation,
                Side.Top or Side.Bottom => DropdownAlignment switch {
                    DropdownAlignmentEnum.Begin => 0,
                    DropdownAlignmentEnum.Center => (Size.X / 2) - (dropdown.Size.X / 2),
                    DropdownAlignmentEnum.End => Size.X - dropdown.Size.X
                }
            }),
            (float)(DropdownSide switch {
                Side.Top => -dropdown.Size.Y - DropdownSeparation,
                Side.Bottom => Size.Y + DropdownSeparation,
                Side.Left or Side.Right => DropdownAlignment switch {
                    DropdownAlignmentEnum.Begin => 0,
                    DropdownAlignmentEnum.Center => (Size.Y / 2) - (dropdown.Size.Y / 2),
                    DropdownAlignmentEnum.End => Size.Y - dropdown.Size.Y
                }
            })
        );
    }

    public override Vector2 _GetMinimumSize() => Button?.GetMinimumSize() ?? default;

    private void OnButtonPressed() => DropdownOpen = !DropdownOpen;

    private void OnDropdownClosed() {
        if (GetViewport().GuiGetFocusOwner() is Control focus && focus != Button && IsAncestorOf(focus)) Button?.GrabFocus();
    }

    private void OnGuiFocusChanged(Control focus) {
        if (DropdownOpen && (focus is null || !IsAncestorOf(focus))) DropdownOpen = false;
    }

    public override void _Input(InputEvent @event) {
        if (!DropdownOpen) return;
        if (@event is InputEventMouseButton mouseButtonEvent && mouseButtonEvent.Pressed) {
            bool inButtonOrDropdown = false;
            foreach (var child in this.FindChildrenOfType<Control>(false).Concat(Button is not null ? [Button] : [])) {
                if (child.GetGlobalRect().HasPoint(mouseButtonEvent.GlobalPosition)) { inButtonOrDropdown = true; break; }
            }
            if (!inButtonOrDropdown) DropdownOpen = false;
        }
    }
}