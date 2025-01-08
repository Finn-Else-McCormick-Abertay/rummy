using Godot;
using Rummy.Game;
using System.Collections.Specialized;
using System.Linq;

namespace Rummy.Interface;

[Tool]
public partial class CardPileDisplay : Container
{
    private bool _horizontal = false;
    [Export] public bool Horizontal { get => _horizontal; set { _horizontal = value; QueueSort(); } }

    private bool _faceDown = false;
    [Export] public bool FaceDown { get => _faceDown; set { _faceDown = value; QueueSort(); } }

    private float _cardSize = 100f;
    [Export] public float CardSize { get => _cardSize; set { _cardSize = value; QueueSort(); } }
    
    private int _cardSeparation = 10;
    [Export] public int CardSeparation { get => _cardSeparation; set { _cardSeparation = value; QueueSort(); } }

    private Vector2 _highlightOffset = new (0f, 30f);
    [Export] public Vector2 HighlightOffset { get => _highlightOffset; set { _highlightOffset = value; QueueSort(); } }

    private Vector2 _highlightBelowOffset = new (0f, 0f);
    [Export] public Vector2 HighlightBelowOffset { get => _highlightBelowOffset; set { _highlightBelowOffset = value; QueueSort(); } }

    private int? _highlightedIndex = null;
    private int? HighlightedIndex { get => _highlightedIndex; set { _highlightedIndex = value; QueueSort(); } }

    private bool _allowDraw = false;
    public bool AllowDraw { get => _allowDraw; set { _allowDraw = value; if (!AllowDraw) { HighlightedIndex = null; } } }
    
    private readonly static PackedScene CardDisplayScene = ResourceLoader.Load<PackedScene>("res://scenes/card_display.tscn");

    private Theme _cardInPileTheme = ResourceLoader.Load<Theme>("res://assets/themes/card/in_pile.tres");
    [Export] private Theme CardInPileTheme {
        get => _cardInPileTheme; set { _cardInPileTheme = value; ReapplyTheme(); }
    }
    private void ReapplyTheme() {
        if (!IsNodeReady()) { return; }
        foreach (Node node in GetChildren()) { (node.GetChild(0) as Control).Theme = CardInPileTheme; }
    }

    [ExportGroup("Debug")]
    private int _numCardsInEditor = 3;
    [Export] private int NumCardsInEditor {
        get => _numCardsInEditor; set { _numCardsInEditor = value; if (Engine.IsEditorHint()) { RebuildDisplays(); } }
    }

    private CardPile _cardPile;
    public CardPile CardPile {
        get => _cardPile;
        set {
            if (_cardPile is not null) { _cardPile.OnChanged -= OnCardPileChanged; }
            _cardPile = value; RebuildDisplays();
            if (_cardPile is not null) { _cardPile.OnChanged += OnCardPileChanged; }
        }
    }

    public override void _Notification(int what) {
        if (what == NotificationSortChildren) {
            var origin = Size / 2f;
            float startPos = -(GetChildCount() * CardSeparation) / 2f;
            foreach (CardDisplay display in GetChildren().Cast<CardDisplay>()) {
                display.SetAnchorsPreset(LayoutPreset.Center);
                display.Size = display.Size with { X = CardSize };
                display.FaceDown = FaceDown;

                float cardPos = startPos + display.GetIndex() * CardSeparation;
                display.Position = (Horizontal ? new(cardPos, 0f) : new(0f, cardPos)) + origin - display.Size / 2f;
                int index = GetChildCount() - display.GetIndex() - 1;
                if (index == HighlightedIndex) {
                    display.Position += HighlightOffset;
                }
                else if (index < HighlightedIndex) {
                    display.Position += HighlightBelowOffset;
                }
            }
        }
    }

    public override void _Ready() {
        RebuildDisplays();
    }

    private void ClearDisplays() {
        if (!IsNodeReady()) { return; }

        foreach (Node node in GetChildren()) {
            RemoveChild(node);
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

        AddChild(cardDisplay); if (!Engine.IsEditorHint()) { cardDisplay.Owner = this; }
        MoveChild(cardDisplay, index);

        if (!Engine.IsEditorHint()) {
            cardDisplay.MouseEntered += () => { OnCardMouseOver(cardDisplay, true); };
            cardDisplay.MouseExited += () => { OnCardMouseOver(cardDisplay, false); };
            cardDisplay.GuiInput += (@event) => { OnCardGuiInput(cardDisplay, @event); };
        }

        QueueSort();
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
            GD.Print($"(Disp) {string.Join(", ", cards)}");
        }
        else {
            for (int i = 0; i < CardPile.Count; ++i) { AddCardDisplay(new Card()); }
        }
    }

    private void OnCardMouseOver(CardDisplay display, bool entering) {
        if (CardPile is null || CardPile is not IDrawable || !AllowDraw) { return; }

        int index = GetChildCount() - display.GetIndex() - 1;
        //GD.Print("Card ", index, " : ", entering ? "Mouse Enter" : "Mouse Exit");
        if (CardPile is IDrawableMulti) {
            HighlightedIndex = entering ? index : null;
        }
        else {
            HighlightedIndex = entering ? 0 : null;
        }
    }

    private void OnCardGuiInput(CardDisplay display, InputEvent @event) {
        if (@event is InputEventMouseButton) {
            var buttonEvent = @event as InputEventMouseButton;
            switch (buttonEvent.ButtonIndex) {
                case MouseButton.WheelUp: case MouseButton.WheelDown: case MouseButton.WheelLeft: case MouseButton.WheelRight:
                    if (buttonEvent.Pressed) { OnCardScroll(display, buttonEvent.ButtonIndex); } break;
                default:
                    OnCardClicked(display, buttonEvent.ButtonIndex, buttonEvent.Pressed); break;
            }
        }
    }

    private void OnCardScroll(CardDisplay display, MouseButton buttonIndex) {
        if (CardPile is null || CardPile is not IDrawableMulti) { return; }

        if (buttonIndex == MouseButton.WheelUp) {
            if (HighlightedIndex + 1 < CardPile.Count) {
                HighlightedIndex++;
            }
        }
        else if (buttonIndex == MouseButton.WheelDown) {
            if (HighlightedIndex - 1 >= 0) {
                HighlightedIndex--;
            }
        }
    }

    public delegate void NotifyDrewAction(int count);
    public event NotifyDrewAction NotifyDrew;

    private void OnCardClicked(CardDisplay display, MouseButton buttonIndex, bool pressed) {
        if (CardPile is null || CardPile is not IDrawable || !AllowDraw) { return; }

        if (buttonIndex == MouseButton.Left && !pressed) {
            NotifyDrew?.Invoke((int)HighlightedIndex + 1);
        }
    }

    private void OnCardPileChanged(object sender, NotifyCollectionChangedEventArgs args) {
        RebuildDisplays();
        /*if (args.Action == NotifyCollectionChangedAction.Add) {
            int index = args.NewStartingIndex;
            foreach (object item in args.NewItems) {
                AddCardDisplay((Card)item, index);
                index++;
            }
        }
        else if (args.Action == NotifyCollectionChangedAction.Replace) {
            int index = args.OldStartingIndex;
            foreach (object item in args.NewItems) {
                (GetChild(GetChildCount() - index - 1) as CardDisplay).Card = (Card)item;
                index++;
            }
        }
        else if (args.Action == NotifyCollectionChangedAction.Move) {
            int oldIndex = args.OldStartingIndex;
            int newIndex = args.NewStartingIndex;
            foreach (object item in args.OldItems) {
                MoveChild(GetChild(GetChildCount() - oldIndex - 1), GetChildCount() - newIndex - 1);
                oldIndex++;
                newIndex++;
            }
        }
        else if (args.Action == NotifyCollectionChangedAction.Remove) {
            RebuildDisplays();
        }
        else if (args.Action == NotifyCollectionChangedAction.Reset) {
            RebuildDisplays();
        }*/
    }
}
