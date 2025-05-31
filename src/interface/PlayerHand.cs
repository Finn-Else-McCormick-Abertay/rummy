using Godot;
using Rummy.Game;
using Rummy.Util;
using static Rummy.Util.Option;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Rummy.Interface;

[Tool]
public partial class PlayerHand : CardPileContainer
{
    public PlayerHand() : base() { CardInPileTheme = ResourceLoader.Load<Theme>("res://assets/themes/card/dropshadow.tres"); }

    public override void _Ready() {
        base._Ready();
        if (!Engine.IsEditorHint() && GetViewport().GuiGetFocusOwner() is null) SendFocusToCards();
    }

    protected override void PostAddCard(CardDisplay display) {
        if (Engine.IsEditorHint()) { return; }
        display.FocusMode = FocusModeEnum.All;
        display.Connect(Control.SignalName.FocusEntered, UpdateHoveredToMatchFocus);
        display.Connect(Control.SignalName.FocusExited, UpdateHoveredToMatchFocus);
    }
    private void UpdateHoveredToMatchFocus() {
        var focusedChild = GetChildren().Cast<Control>().ToList().Find(x => x.HasFocus());
        if (focusedChild is null) { HoveredCard = None; }
        else if (focusedChild is CardDisplay) { HoveredCard = (focusedChild as CardDisplay).Card; }
    }

    public void SendFocusToCards() {
        if (GetChildCount() > 0 && GetViewport().GuiGetFocusOwner() is not CardDisplay) _focusJustJumpedIn = true;

        if (HoveredCard.IsSome) HoveredCardDisplay.Inspect(display => display.GrabFocus());
        else if (GetChildCount() > 0) GetChild<Control>(0).GrabFocus();
    }

    private bool _focusJustJumpedIn = false;

    public Option<Card> HoveredCard {
        get; set {
            field = value;
            HoveredCardDisplay.Inspect(display => { if (GetViewport().GuiGetFocusOwner() is CardDisplay) display.GrabFocus(); });
            QueueSort();
        }
    } = None;
    private Option<CardDisplay> HoveredCardDisplay => HoveredCard.AndThen(FindCard);

    public Option<Card> CannotDiscardCard { get; set { field = value; QueueSort(); } } = None;
    public Option<Card> MustUseCard { get; set { field = value; QueueSort(); } } = None;

    private bool ShouldDrag { get; set; }
    private Vector2 DragOffset { get; set; }
    private Vector2 DragBeginMousePosition { get; set; }

    public Option<Card> DraggingCard { get; set { field = value; QueueSort(); } } = None;
    private Option<CardDisplay> DraggingCardDisplay => DraggingCard.AndThen(FindCard);

    [Export] public Vector2 HoveredOffset { get; set { field = value; QueueSort(); } } = new (0f, 5f);
    [Export] public Vector2 SelectedOffset { get; set { field = value; QueueSort(); } } = new (0f, 30f);
    
    private List<Card> _selectedCards = [];
    public List<Card> SelectedSequence { get { List<Card> sequence = []; _selectedCards.ForEach(sequence.Add); return sequence; } }

    public void Select(Card card) { _selectedCards.Add(card); QueueSort(); } 
    public void Deselect(Card card) { _selectedCards.Remove(card); QueueSort(); }
    
    public Option<CardDisplay> FindCard(Card card) {
        var display = GetChildren().Cast<CardDisplay>().ToList().Find(display => display.Card == card);
        return display is not null ? Some(display) : None;
    }

    protected override bool PreChildSorted(CardDisplay display) {
        if (DraggingCard.IsSomeAnd(draggingCard => draggingCard == display.Card)) { return true; }
        return false;
    }
    
    protected override void PostChildSorted(CardDisplay display) {
        if (DraggingCard.IsSomeAnd(draggingCard => draggingCard == display.Card)) return;

        display.Rotation = 0f;

        if (_selectedCards.Contains(display.Card)) {
            display.Position += SelectedOffset;
            if (GetChildCount() > 1 && _selectedCards.Count == 1 && CannotDiscardCard.IsSomeAnd(card => card == display.Card)) {
                display.Rotation = 5f * (MathF.PI / 180);
            }
        }
        if (HoveredCard.IsSomeAnd(card => card == display.Card)) { display.Position += HoveredOffset; }
    }

    protected override void PostCardPileChanged() {
        if (CardPile is null) return;

        _selectedCards = [.._selectedCards.Intersect(Cards)];
        HoveredCard.Inspect(card => { if (!Cards.Contains(card)) DraggingCard = None; });
        DraggingCard.Inspect(card => { if (!Cards.Contains(card)) { DraggingCard = None; ShouldDrag = false; } });
    }

    protected override void OnCardMouseOver(CardDisplay display, bool entering) {
        if (CardPile is null) return;
        if (DraggingCard.IsNone) HoveredCard = entering ? display.Card : None;
    }

    protected override void OnCardScroll(CardDisplay display, MouseButton buttonIndex) {
        if (CardPile is null) return;

        if (buttonIndex == MouseButton.WheelUp) {
            if (HoveredCardDisplay.IsSomeAnd(display => display.GetIndex() + 1 < GetChildCount())) {
                HoveredCard = HoveredCardDisplay.AndThen(display => Some((GetChild(display.GetIndex() + 1) as CardDisplay).Card));
            }
        }
        else if (buttonIndex == MouseButton.WheelDown) {
            if (HoveredCardDisplay.IsSomeAnd(display => display.GetIndex() - 1 >= 0)) {
                HoveredCard = HoveredCardDisplay.AndThen(display => Some((GetChild(display.GetIndex() - 1) as CardDisplay).Card));
            }
        }
    }

    protected override void OnCardClicked(CardDisplay display, MouseButton buttonIndex, bool pressed) {
        if (CardPile is null) return;

        if (buttonIndex == MouseButton.Left && pressed && HoveredCardDisplay.IsSomeAnd(x => x == display)) {
            //HoveredCardDisplay.Value.GrabFocus();
            ShouldDrag = true;
        }
        if (buttonIndex == MouseButton.Left && !pressed) {
            ShouldDrag = false;
            bool shouldToggleSelect = true;
            if (DraggingCardDisplay.IsSome) {
                var dragDistance = (GetGlobalMousePosition() - DragBeginMousePosition).Length();
                shouldToggleSelect = dragDistance < 5f;
                HoveredCard = DraggingCard;
                DraggingCardDisplay.Inspect(display => {
                    display.ZIndex = 0;
                    display.MouseDefaultCursorShape = CursorShape.Arrow;
                });
                DraggingCard = None;
                QueueSort();
            }
            if (shouldToggleSelect) ToggleSelectHovered();
        }
    }

    protected override void OnCardMouseMotion(CardDisplay display, InputEventMouseMotion @event) {
        if (CardPile is null) return;

        if (ShouldDrag) {
            if (DraggingCard.IsNone && HoveredCard.IsSome) {
                DraggingCard = HoveredCard;
                HoveredCard = None;
                DragOffset = DraggingCardDisplay.Value.GlobalPosition - @event.GlobalPosition;
                DraggingCardDisplay.Value.ZIndex = 100;
                DraggingCardDisplay.Value.MouseDefaultCursorShape = CursorShape.PointingHand;
                DragBeginMousePosition = GetGlobalMousePosition();
                //DraggingCardDisplay.Value.MouseFilter = MouseFilterEnum.Ignore;
            }
        }
        else {
            HoveredCard = display.Card;
        }
    }

    private void ToggleSelectHovered() {
        HoveredCard.Inspect(card => { if (_selectedCards.Contains(card)) { Deselect(card); } else { Select(card); } });
    }

    public override void _Process(double delta) {
        if (Engine.IsEditorHint()) { return; }
        if (_focusJustJumpedIn && Input.IsActionJustReleased(ActionName.Select)) { _focusJustJumpedIn = false; }

        if (HoveredCard.IsSome && Input.IsActionJustPressed(ActionName.Select) && !_focusJustJumpedIn) {
            ToggleSelectHovered();
        }

        if (GetChildren().Cast<Control>().Any(child => child.HasFocus()) && Input.IsActionPressed(ActionName.Select) && HoveredCard.IsSome) {
            int index = HoveredCardDisplay.Value.GetIndex();
            if (Input.IsActionJustPressed(ActionName.UI.Left) && index - 1 >= 0) {
                MoveChild(HoveredCardDisplay.Value, index - 1);
                QueueSort();
            }
            if (Input.IsActionJustPressed(ActionName.UI.Right) && index + 1 < GetChildCount()) {
                MoveChild(HoveredCardDisplay.Value, index + 1);
                QueueSort();
            }
        }
    }

    public override void _Input(InputEvent @event) {
        if (Engine.IsEditorHint()) { return; }
        /*if (@event is InputEventAction) {
            var actionEvent = @event as InputEventAction;
            if (HoveredCard.IsSome && Input.IsActionPressed(ActionName.Select) &&
                    (actionEvent.Action == ActionName.UI.Left || actionEvent.Action == ActionName.UI.Right)) {
                GetViewport().SetInputAsHandled();
            }
        }*/
        if (@event is InputEventMouseMotion) {
            var motionEvent = @event as InputEventMouseMotion;
            if (ShouldDrag && DraggingCard.IsSome) {
                DraggingCardDisplay.Inspect(draggingCard => {
                    draggingCard.GlobalPosition = motionEvent.GlobalPosition + DragOffset;
                    float posMainAxis = Direction == DirectionEnum.Horizontal ? draggingCard.Position.X : draggingCard.Position.Y;

                    int index = draggingCard.GetIndex();
                    var leftNeighbour = (index - 1 >= 0) ? Some(GetChild(index - 1) as CardDisplay) : None;
                    var rightNeighbour = (index + 1 < GetChildCount()) ? Some(GetChild(index + 1) as CardDisplay) : None;

                    leftNeighbour.Inspect(neighbour => {
                        float neighbourPosMainAxis = Direction == DirectionEnum.Horizontal ? neighbour.Position.X : neighbour.Position.Y;
                        float neighbourSizeMainAxis = Direction == DirectionEnum.Horizontal ? neighbour.Size.X : neighbour.Size.Y;
                        if (posMainAxis < neighbourPosMainAxis + neighbourSizeMainAxis / 2f) {
                            MoveChild(draggingCard, neighbour.GetIndex());
                        }
                    });

                    rightNeighbour.Inspect(neighbour => {
                        float neighbourPosMainAxis = Direction == DirectionEnum.Horizontal ? neighbour.Position.X : neighbour.Position.Y;
                        float neighbourSizeMainAxis = Direction == DirectionEnum.Horizontal ? neighbour.Size.X : neighbour.Size.Y;
                        if (posMainAxis > neighbourPosMainAxis - neighbourSizeMainAxis / 2f) {
                            MoveChild(draggingCard, neighbour.GetIndex());
                        }
                    });
                });
            }
        }
    }
}
