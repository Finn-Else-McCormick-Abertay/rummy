using Godot;
using Rummy.Game;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;

namespace Rummy.Interface;

//[Tool]
public partial class CardPileDisplay : Control
{
    private bool _faceDown = false;
    [Export] public bool FaceDown { get => _faceDown; set { _faceDown = value; UpdateDisplaysFacing(); } }

    private float _cardSize = 100f;
    [Export] public float CardSize { get => _cardSize; set { _cardSize = value; UpdateDisplaysSize(); } }
    
    private int _cardSeparation = 10;
    [Export] public int CardSeparation { get => _cardSeparation; set { _cardSeparation = value; UpdateSeparation(); } }

    private float _highlightSpace = 30f;
    [Export] public float HighlightSpace {
        get => _highlightSpace;
        set {
            _highlightSpace = value;
            if (_offsetPlaceholder is not null && IsNodeReady()) {
                Vector2 size = new();
                if (container is HBoxContainer) { size.X = HighlightSpace; }
                else if (container is VBoxContainer) { size.Y = HighlightSpace; }
                _offsetPlaceholder.CustomMinimumSize = size;
            }
        }
    }

    private int? _highlightedIndex = null;
    private int? HighlightedIndex { get => _highlightedIndex; set { _highlightedIndex = value; UpdateDisplaysHighlighting(); } }

    private bool _allowDraw = false;
    public bool AllowDraw { get => _allowDraw; set { _allowDraw = value; if (!AllowDraw) { HighlightedIndex = null; } } }

    private Control _offsetPlaceholder;

    private Theme _cardInPileTheme = ResourceLoader.Load<Theme>("res://assets/themes/card/in_pile.tres");
    [Export] private Theme CardInPileTheme { get => _cardInPileTheme; set { _cardInPileTheme = value; UpdateDisplaysTheme(); } }

    [ExportGroup("Debug")]
    private int _numCardsInEditor = 3;
    [Export] private int NumCardsInEditor {
        get => _numCardsInEditor;
        set { _numCardsInEditor = value; if (Engine.IsEditorHint()) { RebuildDisplays(); } }
    }

    private CardPile _cardPile;
    public CardPile CardPile {
        get => _cardPile;
        set {
            if (_cardPile is not null) { _cardPile.OnChanged -= OnCardPileChanged; }
            _cardPile = value;
            RebuildDisplays();
            if (_cardPile is not null) { _cardPile.OnChanged += OnCardPileChanged; }
        }
    }

    private Control container;
    private readonly static PackedScene CardDisplayScene = ResourceLoader.Load<PackedScene>("res://scenes/card_display.tscn");

    public override void _Ready() {
        container = GetNode<Control>("Container");
        HighlightSpace = _highlightSpace;
        RebuildDisplays();
        UpdateSeparation();
        UpdateDisplaysHighlighting();
    }

    public override void _EnterTree() {
        _offsetPlaceholder = new();
        HighlightSpace = _highlightSpace;
    }

    public override void _ExitTree() {
        _offsetPlaceholder.QueueFree();
    }

    private void UpdateSeparation() {
        if (!IsNodeReady()) { return; }

        container.RemoveThemeConstantOverride("separation");
        container.AddThemeConstantOverride("separation", CardSeparation);
    }

    private void UpdateDisplaysTheme() {
        if (!IsNodeReady()) { return; }
        
        foreach (Node node in container.GetChildren()) {
            if (node == _offsetPlaceholder) { continue; }
            var display = node.GetChild(0) as Control;
            display.Theme = CardInPileTheme;
        }
    }

    private void UpdateDisplaysSize() {
        if (!IsNodeReady()) { return; }
        
        foreach (Node node in container.GetChildren()) {
            if (node == _offsetPlaceholder) { continue; }
            var display = node.GetChild(0) as Control;
            display.CustomMinimumSize = display.CustomMinimumSize with { X = CardSize };
        }
    }

    private void UpdateDisplaysFacing() {
        if (!IsNodeReady()) { return; }
        
        foreach (Node node in container.GetChildren()) {
            if (node == _offsetPlaceholder) { continue; }
            var display = node.GetChild(0) as CardDisplay;
            display.FaceDown = FaceDown;
        }
    }

    private void UpdateDisplaysHighlighting() {
        if (!IsNodeReady()) { return; }

        if (HighlightedIndex is not null && container.GetChildCount() > HighlightedIndex) {
            int index = container.GetChildCount() - (int)HighlightedIndex - 1;
            if (_offsetPlaceholder.GetParent() != container) {
                container.AddChild(_offsetPlaceholder);
                _offsetPlaceholder.Owner = container;
            }
            container.MoveChild(_offsetPlaceholder, (int)index);
        }
        else if (_offsetPlaceholder.GetParent() == container) {
            container.RemoveChild(_offsetPlaceholder);
            _offsetPlaceholder.Owner = null;
        }

        //container.Position = new Vector2();
    }

    private void ClearDisplays() {
        if (!IsNodeReady()) { return; }

        foreach (Node node in container.GetChildren()) {
            container.RemoveChild(node);
            if (node == _offsetPlaceholder) { continue; }
            node.QueueFree();
        }
    }

    private void AddCardDisplay(Card card, int index = 0) {
        if (!IsNodeReady()) { return; }
        
        var cardDisplay = CardDisplayScene.Instantiate() as CardDisplay;
        cardDisplay.Card = card;
        cardDisplay.FaceDown = FaceDown;
        cardDisplay.Theme = CardInPileTheme;
        cardDisplay.CustomMinimumSize = new Vector2( CardSize, 0f );

        Control parent = new();
        container.AddChild(parent);
        if (!Engine.IsEditorHint()) { parent.Owner = container; }

        int realIndex = container.GetChildCount() - index - 1;
        if (_offsetPlaceholder.GetParent() == container) {
            int offsetIndex = _offsetPlaceholder.GetIndex();
            if (offsetIndex < realIndex) { realIndex++; }
        }
        container.MoveChild(parent, realIndex);

        parent.AddChild(cardDisplay);
        cardDisplay.Owner = parent;

        cardDisplay.MouseEntered += () => { OnCardMouseOver(cardDisplay, true); };
        cardDisplay.MouseExited += () => { OnCardMouseOver(cardDisplay, false); };

        cardDisplay.GuiInput += (@event) => {
            if (@event is InputEventMouseButton) {
                var buttonEvent = @event as InputEventMouseButton;
                OnCardClicked(cardDisplay, buttonEvent.ButtonIndex, buttonEvent.Pressed);
            }
        };

        UpdateDisplaysHighlighting();
    }

    private (Control Parent, CardDisplay CardDisplay) GetCardDisplay(int index) {
        int realIndex = container.GetChildCount() - index - 1;
        if (_offsetPlaceholder is not null && _offsetPlaceholder.GetParent() == container) {
            int offsetIndex = _offsetPlaceholder.GetIndex();
            if (offsetIndex < realIndex) { realIndex++; }
        }
        var parent = container.GetChild(realIndex) as Control;
        var display = parent?.GetChild(0) as CardDisplay;
        return (parent, display);
    }

    private void RebuildDisplays() {
        ClearDisplays();
        if (!IsNodeReady() || (CardPile is null && !Engine.IsEditorHint())) { return; }
        else if (Engine.IsEditorHint()) {
            for (int i = 0; i < NumCardsInEditor; ++i) { AddCardDisplay(new Card(Rank.Ace, Suit.Spades)); }
            return;
        }

        // Is readable pile
        if (CardPile is IReadableCardPile || CardPile is IAccessibleCardPile) {
            var cards = (CardPile is IReadableCardPile) ? (CardPile as IReadableCardPile).Cards : (CardPile as IAccessibleCardPile).Cards.ToList().AsReadOnly();
            foreach (Card card in cards) { AddCardDisplay(card); }
        }
        else {
            for (int i = 0; i < CardPile.Count; ++i) { AddCardDisplay(new Card()); }
        }
    }

    private void OnCardMouseOver(CardDisplay display, bool entering) {
        if (CardPile is null || CardPile is not IDrawable || !AllowDraw) { return; }

        int index = container.GetChildCount() - display.GetParent().GetIndex() - 1;

        /*if (CardPile is IDrawableMulti) {
            // TK
        }
        else */if (index == 0) {
            HighlightedIndex = entering ? index : null;
        }
    }

    public delegate void NotifyDrewAction(int count);
    public event NotifyDrewAction NotifyDrew;

    private void OnCardClicked(CardDisplay display, MouseButton buttonIndex, bool pressed) {
        if (CardPile is null || CardPile is not IDrawable || !AllowDraw) { return; }

        int index = container.GetChildCount() - display.GetParent().GetIndex() - 1;

        if (index == HighlightedIndex) {
            NotifyDrew?.Invoke((int)HighlightedIndex + 1);
        }
    }

    private void OnCardPileChanged(object sender, NotifyCollectionChangedEventArgs args) {
        if (args.Action == NotifyCollectionChangedAction.Add) {
            int index = args.NewStartingIndex;
            foreach (object item in args.NewItems) {
                AddCardDisplay((Card)item, index);
                index++;
            }
        }
        else if (args.Action == NotifyCollectionChangedAction.Replace) {
            int index = args.OldStartingIndex;
            foreach (object item in args.NewItems) {
                GetCardDisplay(index).CardDisplay.Card = (Card)item;
                index++;
            }
        }
        else if (args.Action == NotifyCollectionChangedAction.Move) {
            int oldIndex = args.OldStartingIndex;
            int newIndex = args.NewStartingIndex;
            foreach (object item in args.OldItems) {
                var (parent, _) = GetCardDisplay(oldIndex);
                container.MoveChild(parent, -newIndex);
                oldIndex++;
                newIndex++;
            }

        }
        else if (args.Action == NotifyCollectionChangedAction.Remove) {
            RebuildDisplays();
            /*
            int index = args.OldStartingIndex;
            foreach (object item in args.OldItems) {
                var (parent, _) = GetCardDisplay(index);
                if (parent is not null) {
                    container.RemoveChild(parent);
                    parent.QueueFree();
                index++;
            }
            */
        }
        else if (args.Action == NotifyCollectionChangedAction.Reset) {
            RebuildDisplays();
        }
    }
}
