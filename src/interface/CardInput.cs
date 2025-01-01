using Godot;
using Microsoft.VisualBasic;
using Rummy.Interface;
using System;

public partial class CardInput : MarginContainer
{
    private readonly static PackedScene CardDisplayScene = ResourceLoader.Load<PackedScene>("res://scenes/card_display.tscn");

    private CardDisplay _display;
    public CardDisplay Display { get => _display; }

    public override void _Ready() {
        AddThemeConstantOverride("margin_left", 0); AddThemeConstantOverride("margin_right", 0);
        AddThemeConstantOverride("margin_top", 0); AddThemeConstantOverride("margin_bottom", 0);

        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _display = (CardDisplay)CardDisplayScene.Instantiate();
        AddChild(Display);
        Display.SetOwner(this);
        Display.FocusMode = FocusModeEnum.All;

        //Display.FocusEntered += 
        Display.GuiInput += OnGuiInput;
    }

    private void OnGuiInput(InputEvent inputEvent) {
        if (inputEvent is InputEventMouseMotion && inputEvent.IsActionPressed("select")) {

        }
    }
}
