using Godot;
using Rummy.Gameplay;
using Rummy.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Rummy.Interface;

public partial class Output : Control
{
    [Export] public bool OutputToConsole { get; set; } = true;

    [Export] public Shortcut Shortcut { get; set; }

    [ExportGroup("Card")]
    [Export] private Texture2D CardAtlas { get; set; }
    [Export] private Rect2 CardTextureRegion { get; set; } = new(0, 0, 256, 356);
    private readonly Dictionary<Card, AtlasTexture> _cardTextures = [];

    private RichTextLabel _label;

    [ExportGroup("Nodes")]
    [Export] private Control _panelRoot;
    [Export] private BaseButton _openButton;
    [Export] private BaseButton _closeButton;
    [Export] private Node _layerToggleRoot;

    private readonly HashSet<string> _visibleLayers = ["error"], _prevVisibleLayers = [];

    private readonly List<(string Line, Player speaker, string Layer)> _lines = [];

    public bool Open {
        get; set {
            field = value;
            this.OnReady(() => {
                bool containedFocus = GetViewport().GuiGetFocusOwner() is Control focus && IsAncestorOf(focus);
                _panelRoot.Visible = Open;
                _openButton.Visible = !Open;
                if (Open) _closeButton?.GrabFocus(); else if (containedFocus) _openButton?.GrabFocus();
            });
        }
    }

    public override void _Ready() {
        _label = this.FindChildOfType<RichTextLabel>();
        _openButton.Pressed += () => { Open = true; };
        _closeButton.Pressed += () => { Open = false; };

        if (_layerToggleRoot.IsValid()) {
            foreach (var button in _layerToggleRoot.FindChildrenOfType<BaseButton>()) {
                string buttonName = button.Name.ToString().Trim();
                string layer = (0 switch {
                    _ when buttonName.StartsWith("Toggle") => buttonName[6..],
                    _ when buttonName.EndsWith("Toggle") => buttonName[..-6],
                    _ => buttonName
                }).ToLower().Trim();

                SetLayerVisibility(layer, button.ButtonPressed, false);

                void UpdateLayerToButtonState(bool buttonState) => SetLayerVisibility(layer, buttonState);
                button.Toggled += UpdateLayerToButtonState;
            }
        }

        Open = false;
        Rebuild();
        foreach (var rank in Enum.GetValues<Rank>())
            foreach (var suit in Enum.GetValues<Suit>()) {
                var cardTexture = new AtlasTexture { Atlas = CardAtlas };
                Vector2 size = new(CardAtlas.GetWidth() / 13f, CardAtlas.GetHeight() / 4f);
                cardTexture.Region = new Rect2(((int)rank - 1) * size.X, (int)suit * size.Y, size);
                _cardTextures[new Card(rank, suit)] = cardTexture;
            }
    }
    
    public override void _UnhandledInput(InputEvent @event) {
        if (Shortcut.MatchesEvent(@event) && @event.IsPressed()) Open = !Open;
    }

    public void WriteLine(string line, Player speaker = null, string layer = null) {
        (string Line, Player speaker, string Layer) lineObj = (line, speaker, layer);
        _lines.Add(lineObj);
        InternalDisplayLine(lineObj);
        InternalOutputLineToConsole(lineObj);
    }
    public void WriteLine(string line, string layer) => WriteLine(line, null, layer);

    public void Clear() { _lines.Clear(); if (_label.IsValid()) _label.Clear(); }

    private static string StandardizeLayerAliases(string layer) => layer.ToLower() switch {
        "player" => "say",
        "thought" => "think",
        "err" => "error",
        _ => layer
    };

    public void SetLayerVisibility(string layer, bool visible, bool rebuild = true) {
        layer = StandardizeLayerAliases(layer);
        if (rebuild) { _prevVisibleLayers.Clear(); _visibleLayers.ForEach(x => _prevVisibleLayers.Add(x)); }
        if (visible) _visibleLayers.Add(layer); else _visibleLayers.Remove(layer);
        if (rebuild) Rebuild();
    }

    public bool IsLayerVisible(string layer) => _visibleLayers.Contains(StandardizeLayerAliases(layer));

    private void InternalOutputLineToConsole((string Line, Player Speaker, string Layer) lineObj) {
        if (!OutputToConsole) return;
        var (line, speaker, layer) = lineObj;

        var formattedString =
            new StringBuilder().AppendIf(speaker is not null, $"{speaker?.Name} ({layer}): ").Append(line).Replace("\u200B", ", ");

        if (layer == "error") GD.PrintErr(formattedString); else GD.Print(formattedString);
    }

    private void InternalDisplayLine((string Line, Player Speaker, string Layer) lineObj) {
        var (line, speaker, layer) = lineObj;
        if (_label.IsInvalid() || !IsLayerVisible(layer)) return;
        // Speaker label
        if (speaker is not null) {
            if (layer == "think") _label.PushItalics(); else _label.PushBold();
            _label.AddText($"{speaker.Name}: ");
            _label.Pop();
        }

        if (layer == "error") _label.PushColor(Colors.Red);

        foreach (var (text, potentialCard) in ParseStringForCardNames(line)) {
            if (potentialCard is Card card) {
                var font = _label.GetThemeFont("normal_font");
                var fontSize = _label.GetThemeFontSize("normal_font_size");

                int textureHeight = (int)font.GetHeight(fontSize);
                _label.AddImage(_cardTextures.GetValueOrDefault(card), 0, textureHeight, null, InlineAlignment.Center, CardTextureRegion, default, false, text);
            }
            else _label.AddText(text);
        }

        if (layer == "error") _label.Pop();

        _label.Newline();
    }

    private IEnumerable<int> Find(string query) {
        List<int> foundLines = [];
        int visibleParaCounter = -1;
        foreach (var (line, speaker, layer) in _lines) {
            if (IsLayerVisible(layer)) visibleParaCounter++;
            if (line.Find(query) != -1) foundLines.Add(visibleParaCounter);
        }
        return foundLines;
    }

    [GeneratedRegex("(Ace|Two|Three|Four|Five|Six|Seven|Eight|Nine|Ten|Jack|Queen|King) of (Hearts|Clubs|Diamonds|Spades)")]
    private static partial Regex CardRegex();

    private static List<(string Text, Card? Card)> ParseStringForCardNames(string text) {
        List<(string Text, Card? Card)> results = [];
        int prevIndex = 0;
        var matches = CardRegex().EnumerateMatches(text);
        foreach (var match in matches) {
            if (prevIndex < match.Index) results.Add((text[prevIndex..match.Index], null));

            var cardText = text[match.Index..(match.Index + match.Length)]; var split = cardText.Split(" of ");
            Rank rank = Enum.Parse<Rank>(split[0]); Suit suit = Enum.Parse<Suit>(split[1]);

            results.Add((cardText, new Card(rank, suit)));
            prevIndex = match.Index + match.Length;
        }
        if (prevIndex < text.Length) results.Add((text[prevIndex..], null));

        return results;
    }

    private void Rebuild() {
        if (_label.IsInvalid()) return;

        // Find currently scrolled-to paragraph
        var scrollBar = _label.GetVScrollBar();
        int focusedParagraphIndex = -1; float additionalScroll = 0f;
        for (int i = 0; i < _label.GetParagraphCount(); ++i) {
            float paragraphOffset = _label.GetParagraphOffset(i);
            if (paragraphOffset >= scrollBar.Value) { focusedParagraphIndex = i; additionalScroll = paragraphOffset - (float)scrollBar.Value; break; }
        }

        // Convert from paragraph index to true line index (accounting for non-visible layers)
        int focusedLineIndex = -1;
        int visibleParaCounter = -1;
        foreach (var (index, line) in _lines.Index()) {
            if (_prevVisibleLayers.Contains(line.Layer)) visibleParaCounter++;
            if (visibleParaCounter == focusedParagraphIndex) { focusedLineIndex = index; break; }
        }

        // Rebuild label
        _label.Clear(); foreach (var line in _lines) InternalDisplayLine(line);

        // Convert from line index back to paragraph index (accounting for changing layer visibility)
        int newFocusedParagraphIndex = -1;
        for (int i = 0; i < Math.Min(focusedLineIndex, _label.GetParagraphCount()); ++i) {
            if (_visibleLayers.Contains(_lines[i].Layer)) newFocusedParagraphIndex++;
        }

        // Scroll back to focused line
        _label.ScrollToParagraph(Math.Min(focusedParagraphIndex, _label.GetParagraphCount()));
        //scrollBar.Value += additionalScroll;
    }

}