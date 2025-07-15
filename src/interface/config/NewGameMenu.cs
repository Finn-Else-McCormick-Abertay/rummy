using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using Rummy.AI;
using Rummy.Gameplay;
using Rummy.Util;

namespace Rummy.Interface;

public partial class NewGameMenu : ConfigMenu
{
    private static readonly string RestoreLoadoutPath = "user://restore_loadout.json";
    private static readonly string LoadoutFolderPath = "user://loadouts/";

    [Export] private Control _playerEntryRoot;
    [Export] private PackedScene _playerEntryScene;

    [ExportGroup("Buttons")]
    [Export] private BaseButton _playButton;
    [Export] private BaseButton _simulateButton;
    [Export] private BaseButton _addButton;
    [Export] private BaseButton _saveButton;
    [Export] private BaseButton _loadButton;
    [Export] private BaseButton _openSaveFolderButton;

    public override void _Ready() {
        _playButton?.Connect(BaseButton.SignalName.Pressed, () => AcceptAction(GameManager.BeginNewRound));
        _simulateButton?.Connect(BaseButton.SignalName.Pressed, () => AcceptAction(GameManager.SimulateRoundWithoutDisplay, false));
        _addButton?.Connect(BaseButton.SignalName.Pressed, AddPlayer);

        _playerEntryRoot.Connect("order_changed", OnReordered);

        _saveButton?.Connect(BaseButton.SignalName.Pressed, OpenSaveDialog);
        _loadButton?.Connect(BaseButton.SignalName.Pressed, OpenLoadDialog);

        _openSaveFolderButton?.Connect(BaseButton.SignalName.Pressed, () => OS.ShellShowInFileManager(ProjectSettings.GlobalizePath(LoadoutFolderPath)));

        UpdatePlayButtons();
        RestoreLoadout();
    }

    public override void _Notification(int what) {
        if (what == NotificationPredelete) Save(RestoreLoadoutPath);
    }

    public void RestoreLoadout() => Load(RestoreLoadoutPath);

    protected override void OnGameManagerChanged() => RestoreLoadout();

    private void OnReordered() {
        var playerEntries = _playerEntryRoot.FindChildrenOfType<ConfigPlayerEntry>().Select(x => KeyValuePair.Create(x.Player, x)).ToDictionary();
        foreach (var (player, entry) in playerEntries) {
            int oldIndex = GameManager.Game.Players.IndexOf(player);
            int newIndex = entry.GetIndex();
            if (oldIndex != newIndex) {
                GameManager.Game.ReorderPlayer(player, newIndex);
                entry.OnReordered();
            }
        }
        UpdatePlayButtons();
    }

    protected override void Rebuild() {
        foreach (var child in _playerEntryRoot.GetChildren()) {
            _playerEntryRoot.RemoveChild(child); child.QueueFree();
        }
        if (GameManager.IsInvalid()) return;

        if (_playerEntryScene.IsValid())
            foreach (var player in GameManager.Game.Players) {
                var entry = _playerEntryScene?.Instantiate<ConfigPlayerEntry>();
                entry.Player = player; entry.GameManager = GameManager;
                _playerEntryRoot.AddChild(entry);
                entry.Connect(ConfigPlayerEntry.SignalName.PlayerTypeChanged, UpdatePlayButtons);
                entry.Connect(ConfigPlayerEntry.SignalName.PlayerDeleted, UpdatePlayButtons);
            }

        UpdatePlayButtons();
    }

    private void UpdatePlayButtons() {
        _playButton.Disabled = GameManager.IsInvalid();
        _simulateButton.Disabled = GameManager.IsInvalid();
        if (GameManager.IsInvalid()) return;

        int userPlayerCount = GameManager.Game.Players.Count(x => x is UserPlayer);
        bool noPlayers = GameManager.Game.Players.Count == 0;

        if (_playButton.IsValid()) {
            _playButton.TooltipText = "Begin stepping through round turn by turn.";
            _playButton.Disabled = noPlayers;
        }

        if (_simulateButton.IsValid()) {
                _simulateButton.Disabled = userPlayerCount > 0 || noPlayers;
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
        if (GameManager.Game.InRound) Confirm(OnAccept, title: "Are you sure?", message: "Will overwrite current round."); else OnAccept();
    }

    private void AddPlayer() {
        UserPlayer newPlayer = new() { Name = nameof(UserPlayer) };
        GameManager.Game.AddPlayer(newPlayer);
        Rebuild();
        var playerEntries = _playerEntryRoot.FindChildrenOfType<ConfigPlayerEntry>().Select(x => KeyValuePair.Create(x.Player, x)).ToDictionary();
        playerEntries[newPlayer].OnReordered();
    }

    private void OpenSaveDialog() => TextEnterDialog("Save", TrySave);
    private void OpenLoadDialog() => TextEnterDialog("Load", TryLoad,
        DirAccess.Open(LoadoutFolderPath).GetFiles().Select(x => x.TrimPrefix(LoadoutFolderPath).TrimSuffix(".json")));

    private static string IdentifierToSavePath(string identifier) => $"{LoadoutFolderPath}{identifier.Trim()}.json";

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
        if (GameManager is null) return;

        var userDir = DirAccess.Open("user://");
        string loadoutFolderName = LoadoutFolderPath.TrimPrefix("user://").TrimSuffix("/");
        if (!userDir.DirExists(loadoutFolderName)) userDir.MakeDir(loadoutFolderName);

        var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
        if (file is null || !file.IsOpen()) return;

        // Serialize players
        Godot.Collections.Array playerDataArray = [..GameManager.Game.Players.Select(Player.Serialize)];
        
        Godot.Collections.Dictionary fullData = [];
        fullData["Players"] = playerDataArray;

        file.StoreLine(Json.Stringify(fullData, "\t", false));
    }

    private void Load(string filePath) {
        if (GameManager is null) return;

        var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
        if (file is null || !file.IsOpen()) return;

        var json = new Json();
        var parseResult = json.Parse(file.GetAsText());
        if (parseResult != Error.Ok) {
            GD.PrintErr($"Json parse error {json.GetErrorMessage()} at line {json.GetErrorLine()} while loading from {filePath}.");
            return;
        }
        
        var data = new Godot.Collections.Dictionary<string, Variant>((Godot.Collections.Dictionary)json.Data);

        if (!data.TryGetValue("Players", out Variant players)) return;

        var loadedPlayers = players.AsGodotArray<Godot.Collections.Dictionary>().Select(Player.Deserialize);
        if (GameManager.IsValid()) { GameManager.Game.SetPlayers(loadedPlayers); Rebuild(); }
    }
}
