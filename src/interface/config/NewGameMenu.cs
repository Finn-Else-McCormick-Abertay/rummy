using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using Rummy.AI;
using Rummy.Game;
using Rummy.Util;

namespace Rummy.Interface;

public partial class NewGameMenu : ConfigMenu
{
    [Export] private Control _playerEntryRoot;
    [Export] private PackedScene _playerEntryScene;

    [ExportGroup("Buttons")]
    [Export] private BaseButton _playButton;
    [Export] private BaseButton _simulateButton;
    [Export] private BaseButton _closeButton;
    [Export] private BaseButton _addButton;
    [Export] private BaseButton _saveButton;
    [Export] private BaseButton _loadButton;
    [Export] private BaseButton _openSaveFolderButton;

    public override void _Ready() {
        _playButton?.Connect(BaseButton.SignalName.Pressed, () => AcceptAction(GameManager.BeginNewRound));
        _simulateButton?.Connect(BaseButton.SignalName.Pressed, () => AcceptAction(GameManager.SimulateRoundWithoutDisplay, false));
        _closeButton?.Connect(BaseButton.SignalName.Pressed, () => EmitSignal(ConfigMenu.SignalName.CloseRequested));
        _addButton?.Connect(BaseButton.SignalName.Pressed, AddPlayer);

        _playerEntryRoot.Connect("order_changed", OnReordered);

        _saveButton?.Connect(BaseButton.SignalName.Pressed, OpenSaveDialog);
        _loadButton?.Connect(BaseButton.SignalName.Pressed, OpenLoadDialog);

        _openSaveFolderButton?.Connect(BaseButton.SignalName.Pressed, () => OS.ShellShowInFileManager(ProjectSettings.GlobalizePath("user://loadouts/")));

        UpdatePlayButtons();
        RestoreLoadout();
    }

    public override void _Notification(int what) {
        if (what == NotificationPredelete) Save("user://restore_loadout.json");
    }

    public void RestoreLoadout() => Load("user://restore_loadout.json");

    protected override void OnGameManagerChanged() => RestoreLoadout();

    private void OnReordered() {
        var playerEntries = _playerEntryRoot.FindChildrenOfType<ConfigPlayerEntry>().Select(x => KeyValuePair.Create(x.Player, x)).ToDictionary();

        var players = GameManager.Players.OrderBy(x => playerEntries[x].GetIndex());
        GameManager.Players = [.. players];
    }

    protected override void Rebuild() {
        foreach (var child in _playerEntryRoot.GetChildren()) {
            _playerEntryRoot.RemoveChild(child); child.QueueFree();
        }
        if (GameManager.IsInvalid()) return;

        if (_playerEntryScene.IsValid())
            foreach (var player in GameManager.Players) {
                var entry = _playerEntryScene?.Instantiate<ConfigPlayerEntry>();
                entry.Player = player; entry.GameManager = GameManager;
                _playerEntryRoot.AddChild(entry);
                entry.Connect(ConfigPlayerEntry.SignalName.PlayerTypeChanged, UpdatePlayButtons);
            }

        UpdatePlayButtons();
    }

    private void UpdatePlayButtons() {
        _playButton.Disabled = GameManager.IsInvalid();
        _simulateButton.Disabled = GameManager.IsInvalid();
        if (GameManager.IsInvalid()) return;

        int userPlayerCount = GameManager.Players.Count(x => x is UserPlayer);

        if (_playButton.IsValid()) _playButton.TooltipText = "Begin stepping through round turn by turn.";

        if (_simulateButton.IsValid()) {
            _simulateButton.Disabled = userPlayerCount > 0;
            _simulateButton.TooltipText = userPlayerCount switch {
                0 => "Simulate full round without display.",
                _ => "Cannot run simulation containing UserPlayer"
            };
        }
    }

    private void AcceptAction(Action gameAction, bool shouldClose = true) {
        void OnAccept() {
            gameAction?.Invoke();
            EmitSignal(ConfigMenu.SignalName.CloseRequested);
        }
        if (GameManager.InGame) Confirm(OnAccept, title: "Are you sure?", message: "Will overwrite current game."); else OnAccept();
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
    private void Message(string title = null, string message = null, string acceptText = null) {
        var dialog = new AcceptDialog();

        if (title is not null) dialog.Title = title;
        if (message is not null) dialog.DialogText = message;
        if (acceptText is not null) dialog.OkButtonText = acceptText;

        AddChild(dialog);
        dialog.PopupCentered();
        dialog.Show();
    }

    private void AddPlayer() {
        UserPlayer newPlayer = new() { Name = nameof(UserPlayer) };
        GameManager.Players = [.. GameManager.Players.Concat([newPlayer])];
        Rebuild();
    }

    private void TextEnterDialog(string actionName, Action<string> onSubmit, IEnumerable<string> options = null) {
        var dialog = new AcceptDialog();
        dialog.Title = actionName;
        dialog.OkButtonText = actionName;
        dialog.AddCancelButton("Cancel");

        // Line edit (can enter anything)
        if (options is null) {
            var lineEdit = new LineEdit();
            dialog.AddChild(lineEdit);
            dialog.RegisterTextEnter(lineEdit);

            dialog.Confirmed += () => onSubmit(lineEdit.Text);
        }
        // Option button (can only enter one of the options)
        else {
            var optionButton = new OptionButton();
            foreach (var (index, option) in options.Index()) optionButton.AddItem(option, index);
            dialog.AddChild(optionButton);
            
            dialog.Confirmed += () => onSubmit(options.ElementAtOrDefault(optionButton.Selected));
        }

        AddChild(dialog);
        dialog.PopupCentered();
        dialog.Show();
    }

    private void OpenSaveDialog() => TextEnterDialog("Save", TrySave);
    private void OpenLoadDialog() => TextEnterDialog("Load", TryLoad,
        DirAccess.Open("user://loadouts").GetFiles().Select(x => x.TrimPrefix("user://loadouts/").TrimSuffix(".json")));

    private static string IdentifierToSavePath(string identifier) => $"user://loadouts/{identifier.Trim()}.json";

    private void TrySave(string identifier) {
        if (string.IsNullOrWhiteSpace(identifier)) Message(title: "Could not save.", message: "No save name provided.");
        if (FileAccess.FileExists(IdentifierToSavePath(identifier)))
            Confirm(() => Save(IdentifierToSavePath(identifier)), title: "Are you sure?", message: $"Will overwrite existing save for {identifier}.");
        else Save(IdentifierToSavePath(identifier));
    }
    private void TryLoad(string identifier) {
        if (!FileAccess.FileExists(IdentifierToSavePath(identifier)))
            Message(title: "Could not load.", message: $"No such save file '{IdentifierToSavePath(identifier)}'.");
        else Load(IdentifierToSavePath(identifier));
    }

    private void Save(string filePath) {
        var userDir = DirAccess.Open("user://");
        if (!userDir.DirExists("loadouts")) userDir.MakeDir("loadouts");

        var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
        if (file is null || !file.IsOpen()) return;

        Godot.Collections.Array playerDataArray = [];

        foreach (var player in GameManager?.Players ?? []) {
            Godot.Collections.Dictionary playerData = [];

            playerData["Type"] = player.GetType().Name;

            var exportedMembers =
                player.GetType().GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField | BindingFlags.GetProperty)
                    .Where(x => x.MemberType == MemberTypes.Property || x.MemberType == MemberTypes.Field)
                    .Where(x => x.CustomAttributes.Any(y => y.AttributeType == typeof(ExportAttribute)));

            foreach (var memberInfo in exportedMembers) playerData[memberInfo.Name] = player.Get(memberInfo.Name);

            playerDataArray.Add(playerData);
        }
        
        Godot.Collections.Dictionary fullData = [];
        fullData["Players"] = playerDataArray;

        file.StoreLine(Json.Stringify(fullData, "\t", false));
    }

    private void Load(string filePath) {
        var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
        if (file is null || !file.IsOpen()) return;

        Godot.Collections.Array<Player> loadedPlayers = [];

        var json = new Json();
        var parseResult = json.Parse(file.GetAsText());
        if (parseResult != Error.Ok) {
            GD.PrintErr($"Json parse error {json.GetErrorMessage()} at line {json.GetErrorLine()} while loading from {filePath}.");
            return;
        }
        
        var data = new Godot.Collections.Dictionary<string, Variant>((Godot.Collections.Dictionary)json.Data);

        if (!data.TryGetValue("Players", out Variant players)) return;

        foreach (var player in players.AsGodotArray<Godot.Collections.Dictionary>()) {
            if (!(player.TryGetValue("Type", out var typeNameVariant) && typeNameVariant.AsString() is string typeName)) continue;
            var playerType = ConfigPlayerEntry.PlayerTypes.FirstOrDefault(x => x.Name == typeName);
            var newPlayer = (Player)Activator.CreateInstance(playerType);

            var exportedMembers =
                playerType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField | BindingFlags.GetProperty)
                    .Where(x => x.MemberType == MemberTypes.Property || x.MemberType == MemberTypes.Field)
                    .Where(x => x.CustomAttributes.Any(y => y.AttributeType == typeof(ExportAttribute)));

            foreach (var member in exportedMembers) if (player.TryGetValue(member.Name, out var variant)) newPlayer.Set(member.Name, variant);
            loadedPlayers.Add(newPlayer);
        }

        if (GameManager.IsValid()) {
            GameManager.Players = loadedPlayers;
            Rebuild();
        }
    }
}
