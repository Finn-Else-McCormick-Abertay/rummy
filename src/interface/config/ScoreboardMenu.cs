
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rummy.Util;

namespace Rummy.Interface;

public partial class ScoreboardMenu : ConfigMenu
{
    private static readonly string LogFolderPath = "user://game_logs/";

    [Export] BaseButton _saveButton;
    [Export] BaseButton _openSaveFolderButton;

    public override void _Ready() {
        _saveButton?.Connect(BaseButton.SignalName.Pressed, OpenSaveDialog);

        _openSaveFolderButton?.Connect(BaseButton.SignalName.Pressed, () => OS.ShellShowInFileManager(ProjectSettings.GlobalizePath(LogFolderPath)));
    }

    private void OpenSaveDialog() => TextEnterDialog("Save", TrySave);

    private static string IdentifierToSavePath(string identifier) => $"{LogFolderPath}{identifier.Trim()}.json";

    private void TrySave(string identifier) {
        if (string.IsNullOrWhiteSpace(identifier)) Message(title: "Could not save.", message: "No save name provided.");
        if (FileAccess.FileExists(IdentifierToSavePath(identifier)))
            Confirm(() => Save(IdentifierToSavePath(identifier)), title: "Are you sure?", message: $"Will overwrite existing save for {identifier}.");
        else Save(IdentifierToSavePath(identifier));
    }

    private void Save(string filePath) {
        if (GameManager is null) return;

        var userDir = DirAccess.Open("user://");
        string logFolderName = LogFolderPath.TrimPrefix("user://").TrimSuffix("/");
        if (!userDir.DirExists(logFolderName)) userDir.MakeDir(logFolderName);

        var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
        if (file is null || !file.IsOpen()) return;
        file.StoreLine(Json.Stringify(GameManager.Game.Serialize(), "\t", false));
        file.Close();
    }
}