using Godot;
using Rummy.Game;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace Rummy.Interface;

[Tool]
public partial class CardPileContainer : Container
{
    public enum DirectionEnum {
        Horizontal,
        Vertical
    }

    private DirectionEnum _direction;
    [Export] public DirectionEnum Direction {
        get => _direction;
        set { _direction = value; QueueSort(); }
    }
    
    private bool _faceDown = false;
    [Export] public bool FaceDown {
        get => _faceDown;
        set { _faceDown = value; QueueSort(); }
    }
    
    private float _cardSize = 100f;
    [Export] public float CardSize {
        get => _cardSize;
        set { _cardSize = value; QueueSort(); }
    }
    
    private int _cardSeparation = 10;
    [Export] public int CardSeparation {
        get => _cardSeparation;
        set { _cardSeparation = value; QueueSort(); }
    }

    private bool _cardsOverlap = true;
    [Export] public bool CardsOverlap {
        get => _cardsOverlap;
        set { _cardsOverlap = value; QueueSort(); }
    }

    public enum CardSizingReactionEnum {
        None,
        SlideOver,
        //Shorten,
    }
    
    private CardSizingReactionEnum _cardSizingReaction = CardSizingReactionEnum.None;
    [Export] public CardSizingReactionEnum CardSizingReaction {
        get => _cardSizingReaction;
        set { _cardSizingReaction = value; QueueSort(); }
    }
    
    [ExportGroup("Debug")]
    private int _numCardsInEditor = 3;
    [Export] protected int NumCardsInEditor {
        get => _numCardsInEditor; set { _numCardsInEditor = value; if (Engine.IsEditorHint()) { Rebuild(); } }
    }
    
    private CardPile _cardPile;
    public CardPile CardPile {
        get => _cardPile;
        set {
            if (_cardPile is not null) { OnCardPileRemoved(_cardPile); }
            _cardPile = value; Rebuild();
            if (_cardPile is not null) { OnCardPileAdded(_cardPile); }
        }
    }
    protected virtual void OnCardPileAdded(CardPile newPile) => newPile.OnChanged += OnCardPileChanged;
    protected virtual void OnCardPileRemoved(CardPile oldPile) => oldPile.OnChanged -= OnCardPileChanged;

    protected ReadOnlyCollection<Card> Cards => CardPile is null ? new List<Card>().AsReadOnly() :
        (CardPile is IReadableCardPile) ? (CardPile as IReadableCardPile).Cards :
        (CardPile is IAccessibleCardPile) ? (CardPile as IAccessibleCardPile).Cards.ToList().AsReadOnly() :
        new List<Card>().AsReadOnly();

    [Export] protected PackedScene CardDisplayScene = ResourceLoader.Load<PackedScene>("res://scenes/card_display.tscn");
    
    private Theme _cardInPileTheme = ResourceLoader.Load<Theme>("res://assets/themes/card/in_pile.tres");
    [Export] protected Theme CardInPileTheme {
        get => _cardInPileTheme;
        set { _cardInPileTheme = value; ReapplyTheme(); }
    }
    private void ReapplyTheme() {
        if (!IsNodeReady()) { return; }
        foreach (Node node in GetChildren()) { (node.GetChild(0) as Control).Theme = CardInPileTheme; }
    }
    
    public override void _Notification(int what) {
        if (what == NotificationSortChildren) {
            var origin = Size / 2f;
            
            float cardSizeAlongAxis = Direction == DirectionEnum.Horizontal ?
                CardSize : GetChildCount() > 0 ? (GetChild(0) as Control).Size.Y : 0f;

            float areaAlongAxis = Direction == DirectionEnum.Horizontal ? Size.X : Size.Y - (!CardsOverlap ? CardSeparation * 2 : 0f);

            float sizeAlongAxisPerCard = CardsOverlap ? CardSeparation : CardSeparation + cardSizeAlongAxis;
            float sizeAlongAxisPerCardMax = areaAlongAxis / GetChildCount();
            if (CardSizingReaction != CardSizingReactionEnum.None && sizeAlongAxisPerCard > sizeAlongAxisPerCardMax) {
                sizeAlongAxisPerCard = sizeAlongAxisPerCardMax;
            }
            float startPos = -(GetChildCount() * sizeAlongAxisPerCard) / 2f;
            foreach (CardDisplay display in GetChildren().Cast<CardDisplay>()) {
                display.SetAnchorsPreset(LayoutPreset.Center);
                display.Size = display.Size with { X = CardSize };
                display.FaceDown = FaceDown;

                var positionOverriden = PreChildSorted(display);
                if (!positionOverriden) {
                    float cardPos = startPos + display.GetIndex() * sizeAlongAxisPerCard;// + (!CardsOverlap ? CardSeparation : 0f);
                    display.Position =
                        (Direction == DirectionEnum.Horizontal ? new(cardPos, 0f) : new(0f, cardPos))
                        + origin - (CardsOverlap ? display.Size / 2f : new());
                }

                PostChildSorted(display);
            }
        }
    }

    protected virtual bool PreChildSorted(CardDisplay child) { return false; }
    protected virtual void PostChildSorted(CardDisplay child) {}

    public override void _Ready() {
        Rebuild();
    }
    private void Clear() {
        if (!IsNodeReady()) { return; }

        foreach (Node node in GetChildren()) {
            RemoveChild(node);
            node.QueueFree();
        }
    }

    public override void _ExitTree() {
        if (CardPile is not null) {
            CardPile.OnChanged -= OnCardPileChanged;
        }
    }

    protected void AddCard(Card card, int index = 0) {
        if (!IsNodeReady()) { return; }
        
        var cardDisplay = CardDisplayScene.Instantiate() as CardDisplay;
        cardDisplay.Card = card;
        cardDisplay.FaceDown = FaceDown;
        cardDisplay.Theme = CardInPileTheme;
        cardDisplay.CustomMinimumSize = new Vector2( CardSize, 0f );

        AddChild(cardDisplay); if (!Engine.IsEditorHint()) { cardDisplay.Owner = this; }
        if (index >= 0) { MoveChild(cardDisplay, Math.Min(index, GetChildCount() - 1)); }

        if (!Engine.IsEditorHint()) {
            cardDisplay.MouseEntered += () => { OnCardMouseOver(cardDisplay, true); };
            cardDisplay.MouseExited += () => { OnCardMouseOver(cardDisplay, false); };
            cardDisplay.GuiInput += (@event) => {
                if (@event is InputEventMouseButton) {
                    var buttonEvent = @event as InputEventMouseButton;
                    switch (buttonEvent.ButtonIndex) {
                        case MouseButton.WheelUp: case MouseButton.WheelDown: case MouseButton.WheelLeft: case MouseButton.WheelRight:
                            if (buttonEvent.Pressed) { OnCardScroll(cardDisplay, buttonEvent.ButtonIndex); } break;
                        default:
                            OnCardClicked(cardDisplay, buttonEvent.ButtonIndex, buttonEvent.Pressed); break;
                    }
                }
                if (@event is InputEventMouseMotion) {
                    OnCardMouseMotion(cardDisplay, @event as InputEventMouseMotion);
                }
            };
        }

        QueueSort();
    }

    protected virtual void OnCardMouseOver(CardDisplay display, bool entering) {}
    protected virtual void OnCardScroll(CardDisplay display, MouseButton buttonIndex) {}
    protected virtual void OnCardClicked(CardDisplay display, MouseButton buttonIndex, bool pressed) {}
    protected virtual void OnCardMouseMotion(CardDisplay display, InputEventMouseMotion @event) {}

    protected void Rebuild() {
        if (!IsNodeReady() || (CardPile is null && !Engine.IsEditorHint())) {
            Clear();
        }
        else if (Engine.IsEditorHint()) {
            Clear();
            for (int i = 0; i < NumCardsInEditor; ++i) { AddCard(new Card(Rank.Ace, Suit.Spades)); }
        }
        else {
            if (CardPile is IReadableCardPile || CardPile is IAccessibleCardPile) {
                var oldOrder = GetChildren().Cast<CardDisplay>().ToList().ConvertAll(x => x.Card);
                Cards.ToList().ForEach(card => {
                    if (!oldOrder.Contains(card)) { AddCard(card, GetChildCount()); }
                });
                oldOrder.ForEach(card => {
                    if (!Cards.Contains(card)) {
                        var display = GetChildren().Cast<CardDisplay>().ToList().Find(x => x.Card == card);
                        if (display is not null) { RemoveChild(display); display.QueueFree(); }
                    }
                });
            }
            else { Clear(); for (int i = 0; i < CardPile.Count; ++i) { AddCard(new Card()); } }
        }
        PostRebuild();
    }

    protected virtual void PostRebuild() {}
    
    private void OnCardPileChanged(object sender, NotifyCollectionChangedEventArgs args) {
        Rebuild();
        PostCardPileChanged();
    }

    protected virtual void PostCardPileChanged() {}
}