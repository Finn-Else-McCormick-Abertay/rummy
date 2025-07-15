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

    [Export] public bool ShowTooltip { get; set { field = value; QueueSort(); } } = false;
    [Export] public bool ShowCountOverlay { get; set { field = value; Rebuild(); } } = false;

    private Label _countOverlayLabel;

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
            //var cardDisplays = this.FindChildrenWhere<Control>(x => x.GetScript().As<CSharpScript>()?.ResourcePath.TrimSuffix(".cs").EndsWith(nameof(CardDisplay)) ?? false).Select(x => x as CardDisplay);
            var cardDisplays = this.FindChildrenOfType<CardDisplay>();

            float sizeAlongAxisPerCard = MathF.Min(
                0 switch {
                    _ when CardsOverlap => CardSeparation,
                    _ => CardSeparation + Direction switch {
                        DirectionEnum.Horizontal => CardSize,
                        DirectionEnum.Vertical => cardDisplays.FirstOrDefault()?.Size.Y ?? 0f
                    }
                },
                CardSizingReaction switch {
                    CardSizingReactionEnum.None => float.PositiveInfinity,
                    CardSizingReactionEnum.SlideOver => Direction switch {
                        DirectionEnum.Horizontal => Size.X,
                        DirectionEnum.Vertical when CardsOverlap => Size.Y,
                        DirectionEnum.Vertical when !CardsOverlap => Size.Y - CardSeparation
                    } / cardDisplays.Count
                }
            );

            Vector2 origin = Size / 2f;
            float startPos = -(cardDisplays.Count * sizeAlongAxisPerCard) / 2f;
            foreach (var (index, display) in cardDisplays.Index()) {
                display.SetAnchorsPreset(LayoutPreset.Center);
                display.Size = display.Size with { X = CardSize };
                display.FaceDown = FaceDown;
                display.TooltipText = 0 switch { _ when ShowTooltip && FaceDown => $"{CardPile.Count} cards", _ when ShowTooltip && !FaceDown => $"{display.Card}", _ => "" };

                var positionOverriden = PreChildSorted(display);
                if (!positionOverriden) {
                    float cardPos = startPos + index * sizeAlongAxisPerCard;
                    display.Position = Direction switch {
                        DirectionEnum.Horizontal => new(cardPos, 0f),
                        DirectionEnum.Vertical => new(0f, cardPos)
                    } + origin - 0 switch { _ when CardsOverlap => display.Size / 2f, _ => new() };
                }

                PostChildSorted(display);
            }

            if (ShowCountOverlay && _countOverlayLabel.IsValid()) {
                _countOverlayLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                _countOverlayLabel.Text = Text.Plural(cardDisplays.Count, one: "% card", other: "% cards", none: "");
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

        if (_countOverlayLabel.IsValid() && !ShowCountOverlay) { _countOverlayLabel.QueueFree(); _countOverlayLabel = null; }
        if (_countOverlayLabel.IsInvalid() && ShowCountOverlay) {
            _countOverlayLabel = new Label() {
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                ThemeTypeVariation = "CountOverlayLabel"
            };
            AddChild(_countOverlayLabel, false, InternalMode.Back);
            _countOverlayLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

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