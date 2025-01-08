using Godot;
using Rummy.Game;
using System;
using System.Collections.Generic;
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
            if (_cardPile is not null) { _cardPile.OnChanged -= OnCardPileChanged; }
            _cardPile = value; Rebuild();
            if (_cardPile is not null) { _cardPile.OnChanged += OnCardPileChanged; }
        }
    }

    protected bool preserveOrderOnRebuild = false;

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
                
            float sizeAlongAxisMax =
                (Direction == DirectionEnum.Horizontal ? Size.X : Size.Y) / GetChildCount();// - (!CardsOverlap ? CardSeparation * 2 : 0f);

            float sizeAlongAxisPerCard = CardsOverlap ? CardSeparation : CardSeparation * 2 + cardSizeAlongAxis;
            if (CardSizingReaction != CardSizingReactionEnum.None && sizeAlongAxisPerCard > sizeAlongAxisMax) {
                sizeAlongAxisPerCard = sizeAlongAxisMax;
            }
            float startPos = -(GetChildCount() * sizeAlongAxisPerCard - CardSeparation * 2) / 2f;
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

    private void AddCard(Card card, int index = 0) {
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

    private void Rebuild() {
        List<Card> oldOrder = new();
        if (preserveOrderOnRebuild) { GetChildren().Cast<CardDisplay>().ToList().ForEach(display => oldOrder.Add(display.Card)); }

        Clear();
        if (!IsNodeReady() || (CardPile is null && !Engine.IsEditorHint())) { return; }
        else if (Engine.IsEditorHint()) {
            for (int i = 0; i < NumCardsInEditor; ++i) { AddCard(new Card(Rank.Ace, Suit.Spades)); }
            return;
        }

        // Is readable pile
        if (CardPile is IReadableCardPile || CardPile is IAccessibleCardPile) {
            foreach (Card card in Cards) { AddCard(card); }
            if (preserveOrderOnRebuild) {
                foreach (CardDisplay display in GetChildren().Cast<CardDisplay>()) {
                    MoveChild(display, oldOrder.FindIndex(x => x == display.Card));
                }
            }
        }
        else {
            for (int i = 0; i < CardPile.Count; ++i) { AddCard(new Card()); }
        }
    }
    
    private void OnCardPileChanged(object sender, NotifyCollectionChangedEventArgs args) {
        Rebuild();
        /*if (args.Action == NotifyCollectionChangedAction.Add) {
            int index = args.NewStartingIndex;
            foreach (object item in args.NewItems) {
                AddCard((Card)item, GetChildCount() - index - 1);
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
            int oldIndex = args.OldStartingIndex;
            foreach (object item in args.OldItems) {
                var display = GetChild(GetChildCount() - oldIndex - 1) as CardDisplay;
                RemoveChild(display);
                display.QueueFree();
                oldIndex++;
            }
        }
        else if (args.Action == NotifyCollectionChangedAction.Reset) {
            Rebuild();
        }*/
        PostCardPileChanged();
    }

    protected virtual void PostCardPileChanged() {}
}