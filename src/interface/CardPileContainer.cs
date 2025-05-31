using Godot;
using Rummy.Game;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Rummy.Util;

namespace Rummy.Interface;

[Tool]
public partial class CardPileContainer : Container
{
    public enum DirectionEnum { Horizontal, Vertical }
    public enum CardSizingReactionEnum { None, SlideOver, /*Shorten,*/ }

    [Export] public DirectionEnum Direction { get; set { field = value; QueueSort(); } }
    [Export] public bool FaceDown { get; set { field = value; QueueSort(); } }

    [Export] public float CardSize { get; set { field = value; QueueSort(); } } = 100f;
    [Export] public int CardSeparation { get; set { field = value; QueueSort(); } } = 10;
    [Export] public bool CardsOverlap { get; set { field = value; QueueSort(); } } = true;
    [Export] public CardSizingReactionEnum CardSizingReaction { get; set { field = value; QueueSort(); } } = CardSizingReactionEnum.None;
    
    [ExportGroup("Debug")]
    [Export] protected int NumCardsInEditor { get; set { field = value; if (Engine.IsEditorHint()) Rebuild(); } } = 3;
    
    public CardPile CardPile { get; set { SetCardPileHooks(false); field = value; Rebuild(); SetCardPileHooks(true); } }
    private void SetCardPileHooks(bool enable) {
        if (CardPile is null) return;
        if (enable) CardPile.OnChanged += OnCardPileChanged; else CardPile.OnChanged -= OnCardPileChanged;
    }

    protected ReadOnlyCollection<Card> Cards =>
        (CardPile is IReadableCardPile readablePile) ? readablePile.Cards :
        (CardPile is IAccessibleCardPile accessiblePile) ? accessiblePile.Cards.ToList().AsReadOnly() :
        new List<Card>().AsReadOnly();

    [Export] protected PackedScene CardDisplayScene = ResourceLoader.Load<PackedScene>("res://scenes/card_display.tscn");
    [Export] protected Theme CardInPileTheme { get; set { field = value; ReapplyTheme(); } } = ResourceLoader.Load<Theme>("res://assets/themes/card/in_pile.tres");
    private void ReapplyTheme() {
        if (!IsNodeReady()) return;
        foreach (Node node in GetChildren()) if (node.GetChild(0) is Control control) control.Theme = CardInPileTheme;
    }
    
    public override void _Notification(int what) {
        if (what == NotificationSortChildren) {
            var origin = Size / 2f;
            
            float cardSizeAlongAxis = Direction == DirectionEnum.Horizontal ?
                CardSize : GetChildCount() > 0 ? (GetChild(0) as Control).Size.Y : 0f;

            float areaAlongAxis = Direction == DirectionEnum.Horizontal ? Size.X : Size.Y - (!CardsOverlap ? CardSeparation : 0f);

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

    protected virtual bool PreChildSorted(CardDisplay child) => false;
    protected virtual void PostChildSorted(CardDisplay child) {}

    public override void _Ready() { Rebuild(); }
    public override void _EnterTree() { SetCardPileHooks(true); }
    public override void _ExitTree() { SetCardPileHooks(false); }

    private void Clear() {
        if (!IsNodeReady()) return;
        foreach (Node node in GetChildren()) { RemoveChild(node); node.QueueFree(); }
    }

    protected void AddCard(Card card, int index = 0) {
        if (!IsNodeReady()) { return; }

        var cardDisplay = CardDisplayScene.Instantiate() as CardDisplay;
        cardDisplay.Card = card;
        cardDisplay.FaceDown = FaceDown;
        cardDisplay.Theme = CardInPileTheme;
        cardDisplay.CustomMinimumSize = new Vector2(CardSize, 0f);

        AddChild(cardDisplay); if (!Engine.IsEditorHint()) { cardDisplay.Owner = this; }
        if (index >= 0) { MoveChild(cardDisplay, Math.Min(index, GetChildCount() - 1)); }

        if (!Engine.IsEditorHint()) {
            cardDisplay.MouseEntered += () => { OnCardMouseOver(cardDisplay, true); };
            cardDisplay.MouseExited += () => { OnCardMouseOver(cardDisplay, false); };
            cardDisplay.GuiInput += (@event) => {
                if (@event is InputEventMouseButton) {
                    var buttonEvent = @event as InputEventMouseButton;
                    switch (buttonEvent.ButtonIndex) {
                        case MouseButton.WheelUp:
                        case MouseButton.WheelDown:
                        case MouseButton.WheelLeft:
                        case MouseButton.WheelRight:
                            if (buttonEvent.Pressed) { OnCardScroll(cardDisplay, buttonEvent.ButtonIndex); }
                            break;
                        default:
                            OnCardClicked(cardDisplay, buttonEvent.ButtonIndex, buttonEvent.Pressed); break;
                    }
                }
                if (@event is InputEventMouseMotion) {
                    OnCardMouseMotion(cardDisplay, @event as InputEventMouseMotion);
                }
            };
        }

        PostAddCard(cardDisplay);

        QueueSort();
    }
    protected virtual void PostAddCard(CardDisplay display) {}

    protected virtual void OnCardMouseOver(CardDisplay display, bool entering) {}
    protected virtual void OnCardScroll(CardDisplay display, MouseButton buttonIndex) {}
    protected virtual void OnCardClicked(CardDisplay display, MouseButton buttonIndex, bool pressed) {}
    protected virtual void OnCardMouseMotion(CardDisplay display, InputEventMouseMotion @event) {}
    
    public Action NotifyCardPileRebuilt;

    protected void Rebuild() {
        if (!IsNodeReady()) return;

        if (Engine.IsEditorHint()) { Clear(); for (int i = 0; i < NumCardsInEditor; ++i) AddCard(new Card(Rank.Ace, Suit.Spades)); }
        else if (CardPile is IReadableCardPile || CardPile is IAccessibleCardPile) {
            var currentOrder = Cards;
            var oldOrder = GetChildren().Cast<CardDisplay>().ToList().ConvertAll(x => x.Card);
            foreach (var card in currentOrder) if (!oldOrder.Contains(card)) { AddCard(card, GetChildCount()); }
            foreach (var card in oldOrder)
                if (!currentOrder.Contains(card) && this.FindChildWhere<CardDisplay>(x => x.Card == card) is CardDisplay display) {
                    RemoveChild(display); display.QueueFree();
                }
        }
        else if (CardPile is not null) { Clear(); for (int i = 0; i < CardPile.Count; ++i) AddCard(new Card()); }
        else { Clear(); }

        PostRebuild();
        NotifyCardPileRebuilt?.Invoke();
    }

    protected virtual void PostRebuild() {}
    
    private void OnCardPileChanged(object sender, NotifyCollectionChangedEventArgs args) {
        Rebuild(); PostCardPileChanged();
    }

    protected virtual void PostCardPileChanged() {}
}