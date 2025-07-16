
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rummy.Util;

namespace Rummy.Interface;

public partial class ScoreboardMenu : ConfigMenu {
    private static readonly string LogFolderPath = "user://game_logs/";

    [ExportGroup("Nodes")]
    [Export] BaseButton _saveButton;
    [Export] BaseButton _openSaveFolderButton;
    [Export] Control _mainContent;

    [ExportGroup("Scripts")]
    [Export] Script _tableContainerScript;

    public override void _Ready() {
        _saveButton?.Connect(BaseButton.SignalName.Pressed, OpenSaveDialog);
        _openSaveFolderButton?.Connect(BaseButton.SignalName.Pressed, () => OS.ShellShowInFileManager(ProjectSettings.GlobalizePath(LogFolderPath)));
        VisibilityChanged += OnVisibilityChanged;
        ClearTable();
    }

    private void OnVisibilityChanged() { if (Visible) RebuildTable(GameManager?.Game?.Serialize()); }

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

    private void ClearTable() { foreach (var child in _mainContent.GetChildren()) child.QueueFree(); }

    private void RebuildTable(Godot.Collections.Dictionary data) {
        ClearTable(); if (data is null) return;
        if (!data.ContainsKey("Players") || !data.ContainsKey("Rounds")) return;

        var players = data["Players"].AsGodotArray<Godot.Collections.Dictionary>();
        var playerNames = players.Select(x => x["Name"]);

        var rounds = data["Rounds"].AsGodotArray<Godot.Collections.Dictionary>();

        var tableContainer = _tableContainerScript.New<Container>();
        _mainContent.AddChild(tableContainer); tableContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        static Label CreateLabel(Variant text, StringName typeVariation) => new() {
            Text = text.AsString(), ThemeTypeVariation = typeVariation, VerticalAlignment = VerticalAlignment.Bottom, SizeFlagsVertical = SizeFlags.Fill,
            HorizontalAlignment = typeVariation.ToString() switch {
                nameof(ThemeTypeVariationName.TableRoundHeaderLabel) or nameof(ThemeTypeVariationName.TableRowHeaderLabel) => HorizontalAlignment.Center,
                nameof(ThemeTypeVariationName.TableContentLabel) => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Left
            }
        };

        var titleRow = new HBoxContainer() { Name = "Title Row" }; tableContainer.AddChild(titleRow);
        titleRow.AddChild(CreateLabel("Round", ThemeTypeVariationName.TableRoundHeaderLabel));
        foreach (var name in playerNames) titleRow.AddChild(CreateLabel(name, ThemeTypeVariationName.TableColumnHeaderLabel));

        foreach (var (index, round) in rounds.Index()) {
            var roundRow = new HBoxContainer() { Name = $"Row {index + 1}" }; tableContainer.AddChild(roundRow);
            roundRow.AddChild(CreateLabel(index + 1, ThemeTypeVariationName.TableRowHeaderLabel));

            var scores = round.GetValueOrDefault("Scores").AsGodotDictionary() ?? [];
            foreach (var name in playerNames) {
                int score = scores.GetValueOrDefault(name).AsInt32();
                roundRow.AddChild(CreateLabel(score switch { 0 => "-", _ => score.ToString() }, ThemeTypeVariationName.TableContentLabel));
            }
        }
    }

    public static class ThemeTypeVariationName
    {
        public static readonly StringName TableRoundHeaderLabel  = nameof(TableRoundHeaderLabel);
        public static readonly StringName TableColumnHeaderLabel = nameof(TableColumnHeaderLabel);
        public static readonly StringName TableRowHeaderLabel    = nameof(TableRowHeaderLabel);
        public static readonly StringName TableContentLabel      = nameof(TableContentLabel);
    }
}