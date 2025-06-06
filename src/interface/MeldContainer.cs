using System;
using System.Linq;
using Godot;
using Rummy.Game;
using Rummy.Util;
using static Rummy.Util.Option;

namespace Rummy.Interface;

[Tool]
public partial class MeldContainer : CardPileContainer
{
    public Option<PlayerHand> PlayerHand { get; set; } = None;

    public Option<Card> PotentialCard { get; set { field = value; Rebuild(); } } = None;
    public bool CanLayOff { get; set { field = value; if (!CanLayOff) PotentialCard = None; } } = false;

    private bool IsMouseOver { get; set; } = false;
    
    public event Action<Card> NotifyLaidOff;

    protected override void PostRebuild() {
        if (CardPile is not Meld meld) return;

        PotentialCard.Inspect(card => {
            int index = meld.IndexIfLaidOff(card);
            if (index != -1) {
                AddCard(card, index);
                var newDisplay = GetChild(index) as Control;
                newDisplay.Modulate = newDisplay.Modulate with { A = 0.5f };
            }
        }).InspectNone(() => { foreach (Control child in GetChildren().Cast<Control>()) child.Modulate = child.Modulate with { A = 1f }; });
    }
    protected override void PostChildSorted(CardDisplay display) {
        if (CardPile is null) return;

        int index = (CardPile as Meld).Cards.FindIndex(x => x == display.Card);
        if (index != -1) MoveChild(display, index);
    }

    public override void _Input(InputEvent @event) {
        if (CardPile is null || CardPile is not Meld) { return; }

        if (@event is InputEventMouseButton) {
            var mouseButtonEvent = @event as InputEventMouseButton;
            if (mouseButtonEvent.ButtonIndex == MouseButton.Left && !mouseButtonEvent.Pressed) {
                PotentialCard.Inspect(card => {
                    PotentialCard = None;
                    NotifyLaidOff?.Invoke(card);
                });
            }
        }
        if (@event is InputEventMouseMotion) {
            var mouseMotionEvent = @event as InputEventMouseMotion;
            bool mouseOver = GetChildren().Any(display => display.GetNode<Control>("Shadow").GetGlobalRect().HasPoint(mouseMotionEvent.GlobalPosition));
            
            if (!IsMouseOver && mouseOver) {
                // If currently dragging card
                PlayerHand.Inspect(hand => hand.DraggingCard.Inspect(card => {
                    if ((CardPile as Meld).CouldLayOff(card)) { PotentialCard = card; }
                }));
            }
            if (IsMouseOver && !mouseOver) { PotentialCard = None; }

            IsMouseOver = mouseOver;
        }
    }
}