using Godot;
using Rummy.Game;
using Rummy.Util;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Rummy.Interface;

public partial class Output : Control
{
    [Export] public bool OutputToConsole { get; set; } = true;
    [Export] private Texture2D CardAtlas { get; set; }
    [Export] private Rect2 CardTextureRegion { get; set; } = new(0, 0, 256, 356);
    private readonly Dictionary<Card, AtlasTexture> _cardTextures = [];

    private RichTextLabel _label;
    private SplitContainer _splitContainer;
    [Export] private Control _panelRoot;
    [Export] private BaseButton _openButton;
    [Export] private BaseButton _closeButton;

    private readonly List<(string Line, Player speaker, string Category)> _lines = [];

    public bool Open {
        get;
        set {
            field = value;
            this.OnReady(() => {
                _panelRoot.Visible = Open;
                _openButton.Visible = !Open;
            });
        }
    }

    public override void _Ready() {
        _label = this.FindChildOfType<RichTextLabel>();
        _splitContainer = this.FindChildOfType<SplitContainer>();
        _openButton.Pressed += () => { Open = true; };
        _closeButton.Pressed += () => { Open = false; };
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

    public void WriteLine(string line, Player speaker = null, string category = null) {
        (string Line, Player speaker, string Category) lineObj = (line, speaker, category);
        _lines.Add(lineObj);
        InternalDisplayLine(lineObj);
        InternalOutputLineToConsole(lineObj);
    }
    public void WriteLine(string line, string category) => WriteLine(line, null, category);

    public void Clear() { _lines.Clear(); if (_label.IsValid()) _label.Clear(); }

    private void InternalOutputLineToConsole((string Line, Player Speaker, string Category) lineObj) {
        if (!OutputToConsole) return;
        var (line, speaker, category) = lineObj;

        var formattedString =
            new StringBuilder().AppendIf(speaker is not null, $"{speaker?.Name} ({category}): ").Append(line).Replace("\u200B", ", ");
        GD.Print(formattedString);
    }

    private void InternalDisplayLine((string Line, Player Speaker, string Category) lineObj) {
        if (_label.IsInvalid()) return;
        var (line, speaker, category) = lineObj;
        // Speaker label
        if (speaker is not null) {
            if (category == "think") _label.PushItalics(); else _label.PushBold();
            _label.AddText($"{speaker.Name}: ");
            _label.Pop();
        }

        foreach (var (text, potentialCard) in ParseStringForCardNames(line)) {
            if (potentialCard is Card card) {
                var font = _label.GetThemeFont("normal_font");
                var fontSize = _label.GetThemeFontSize("normal_font_size");

                int textureHeight = (int)font.GetHeight(fontSize);
                _label.AddImage(_cardTextures.GetValueOrDefault(card), 0, textureHeight, null, InlineAlignment.Center, CardTextureRegion, default, false, text);
            }
            else _label.AddText(text);
        }
        _label.AddText("\n");
    }
    
    [GeneratedRegex("(Ace|Two|Three|Four|Five|Six|Seven|Eight|Nine|Ten|Jack|Queen|King) of (Hearts|Clubs|Diamonds|Spades)")]
    private static partial Regex CardRegex();

    private static List<(string Text, Card? Card)> ParseStringForCardNames(string text) {
        List<(string Text, Card? Card)> results = [];
        int prevIndex = 0;
        var matches = CardRegex().EnumerateMatches(text);
        foreach (var match in matches) {
            if (prevIndex < match.Index) results.Add((text[prevIndex..match.Index], null));

            var cardText = text[match.Index..(match.Index + match.Length)];
            var split = cardText.Split(" of ");
            var rank = Enum.Parse<Rank>(split[0]);
            var suit = Enum.Parse<Suit>(split[1]);

            results.Add((cardText, new Card(rank, suit)));
            prevIndex = match.Index + match.Length;
        }
        if (prevIndex < text.Length) results.Add((text[prevIndex..], null));

        return results;
    }

    private void Rebuild() {
        if (_label.IsInvalid()) return;
        _label.Clear(); foreach (var line in _lines) InternalDisplayLine(line);
    }
}