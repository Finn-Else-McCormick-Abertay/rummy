using Godot;
using Rummy.AI;
using Rummy.Config;
using Rummy.Game;
using Rummy.Util;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Rummy.Interface;

public partial class ConfigPlayerEntry : Control
{
    [Signal] public delegate void PlayerTypeChangedEventHandler();

    public GameManager GameManager { get; set; }
    public Player Player { get; set { field = value; this.OnReady(Rebuild); } }

    [Export] private PlayerIconResource _iconResource;

    [ExportGroup("Nodes")]
    [Export] private Label _label;
    [Export] private TextureRect _icon;
    [Export] private Button _configureButton;
    [Export] private Button _deleteButton;
    [Export] private Popup _settingsPopup;
    [Export] private Control _propertiesBox;

    public static readonly ReadOnlyCollection<Type> PlayerTypes = new([typeof(UserPlayer), typeof(RandomPlayer), typeof(IntelligentPlayer)]);

    public override void _Ready() {
        _deleteButton.Pressed += OnDeletePressed;
        _configureButton.Pressed += OnConfigurePressed;
        _settingsPopup.Hide();
    }

    private void RebuildLabel() {
        _label.Text = Player?.Name ?? "Invalid Player";
        _icon.Texture = _iconResource?.IconFor(Player);
    }

    private void Rebuild() {
        RebuildLabel();
        _propertiesBox.TryConnect("value_changed", Callable.From<StringName, Variant>(OnPropertyValueChanged));

        void AddBool(StringName name, bool? val = null)
            => _propertiesBox.Call("add_bool", name, val ?? Player.Get(name));
        void AddInt(StringName name, int? val = null, int? min = null, int? max = null)
            => _propertiesBox.Call("add_int", name, val ?? Player.Get(name), min ?? int.MinValue, max ?? int.MaxValue);
        void AddFloat(StringName name, double? val = null, double step = 0.01, double min = -99999900000.0, double max = 99999900000.0)
            => _propertiesBox.Call("add_float", name, val ?? Player.Get(name), min, max, step);
        void AddString(StringName name, string val = null)
            => _propertiesBox.Call("add_string", name, val ?? Player.Get(name));

        // Update properties
        _propertiesBox.Call("clear");

        _propertiesBox.Call("add_options", "Type", new Godot.Collections.Array(PlayerTypes.Select(x => Variant.From(x.Name))), PlayerTypes.IndexOf(Player.GetType()), false);
        AddString(Player.PropertyName.Name);

        string openExportGroup = null;

        var exportedMembers = Player.GetType().GetMembers(
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField | BindingFlags.GetProperty
            )
            .Where(x => x.MemberType == MemberTypes.Property || x.MemberType == MemberTypes.Field)
            .Where(x => x.CustomAttributes.Any(y => y.AttributeType == typeof(ExportAttribute)));
        foreach (var memberInfo in exportedMembers) {
            if (memberInfo.GetCustomAttribute<ExportGroupAttribute>() is ExportGroupAttribute exportGroup) {
                if (openExportGroup is not null) _propertiesBox.Call("end_group");
                openExportGroup = exportGroup.Name;
                _propertiesBox.Call("add_group", exportGroup.Name);
            }

            var descriptionAttribute = memberInfo.GetCustomAttribute<ExportDescriptionAttribute>();
            string customType = descriptionAttribute?.Type;

            var name = memberInfo.Name;
            var type = memberInfo switch { PropertyInfo prop => prop.PropertyType, FieldInfo field => field.FieldType, _ => null };
            if (type == typeof(bool)) AddBool(name);
            else if (type == typeof(int)) AddInt(name);
            else if (type == typeof(double) || type == typeof(float)) {
                if (customType == "Percentage" || name.EndsWith("Chance")) AddInt(name, (int)Math.Floor(Player.Get(name).AsDouble() * 100), 0, 100);
                else if (customType == "PercentageFloat") AddFloat(name, step: 0.01, min: 0, max: 1);
                else AddFloat(name, step: 1);
            }
            else if (type == typeof(string)) AddString(name);
        }

        if (openExportGroup is not null) _propertiesBox.Call("end_group");
        
        var keys = _propertiesBox.Get("_keys").AsGodotDictionary();
        var editors = _propertiesBox.Get("_editors").AsGodotArray();

        foreach (var memberInfo in exportedMembers) {
            if (memberInfo.GetCustomAttribute<ExportDescriptionAttribute>() is ExportDescriptionAttribute descriptionAttribute) {
                if (keys.TryGetValue(Variant.From(new StringName(memberInfo.Name)), out var key)) {
                    var editor = editors[key.AsInt32()].As<Control>();
                    if (editor is null) continue;

                    var box = editor.GetParent() as Control;
                    var label = box.FindChildOfType<Label>();

                    if (descriptionAttribute.Tooltip is not null) {
                        box.TooltipText = descriptionAttribute.Tooltip;
                        editor.TooltipText = descriptionAttribute.Tooltip;
                        if (label.IsValid()) label.TooltipText = descriptionAttribute.Tooltip;
                    }
                    if (descriptionAttribute.DisplayName is not null && label.IsValid()) { label.Text = descriptionAttribute.DisplayName; }
                }
            }
        }

        _settingsPopup.ChildControlsChanged();
    }
    
    [GeneratedRegex("\\D*")]
    private static partial Regex SkipFinalNumberRegex();

    private void OnPropertyValueChanged(StringName prop, Variant newValue) {
        if (prop == "Type") {
            var newType = PlayerTypes.ElementAtOrDefault(newValue.AsInt32());
            if (newType is not null) {
                var newPlayer = (Player)Activator.CreateInstance(newType);

                newPlayer.Score = Player.Score;

                // Find player index
                var players = GameManager.Players;
                int index = players.FindIndex(Player);

                // If non-unique name
                if (SkipFinalNumberRegex().Match(Player.Name).Value == Player.GetType().Name)
                    newPlayer.Name = $"{newType.Name}{GameManager.Players.Take(index).Count(x => x.GetType() == newType) + 1}";
                else newPlayer.Name = Player.Name;

                // Replace player with new player
                players.Insert(index, newPlayer);
                players.Remove(Player);

                GameManager.Players = players;

                Player = newPlayer;

                EmitSignal(SignalName.PlayerTypeChanged);
            }
        }
        else {
            if (prop.ToString().EndsWith("Chance")) Player?.Set(prop, newValue.AsInt32()/100d);
            else Player?.Set(prop, newValue);
            RebuildLabel();
        }
    }

    private void Confirm(Action onConfirm, string title = null, string message = null, string acceptText = null) {
        var confirmationDialog = new ConfirmationDialog();
        confirmationDialog.Confirmed += onConfirm;

        if (title is not null) confirmationDialog.Title = title;
        if (message is not null) confirmationDialog.DialogText = message;
        if (acceptText is not null) confirmationDialog.OkButtonText = acceptText;

        AddChild(confirmationDialog);
        confirmationDialog.PopupCentered();
        confirmationDialog.Show();
    }

    private void OnConfigurePressed() => _settingsPopup.Visible = !_settingsPopup.Visible;

    private void OnDeletePressed()
        => Confirm(PerformDelete, title: $"Delete {Player?.Name}?", message: "This action cannot be undone.");

    private void PerformDelete() {
        if (GameManager.IsInvalid()) return;

        var players = GameManager.Players;
        players.Remove(Player);
        GameManager.Players = players;

        QueueFree();
    }
}
