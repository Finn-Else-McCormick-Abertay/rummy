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

    private Option<Card> _potentialCard = None;
    private Option<Card> PotentialCard { get => _potentialCard; set { _potentialCard = value; Rebuild(); } }

    private bool _canLayOff = false;
    public bool CanLayOff { get => _canLayOff; set { _canLayOff = value; if (!_canLayOff) { PotentialCard = None; } } }

    private bool IsMouseOver { get; set; } = false;

    protected override void OnCardPileAdded(CardPile newPile) {
        base.OnCardPileAdded(newPile);
        //if (newPile is IMeld) { (newPile as IMeld).NotifyLaidOff += OnLayOff; }
    }
    protected override void OnCardPileRemoved(CardPile oldPile) {
        base.OnCardPileAdded(oldPile);
        //if (oldPile is IMeld) { (oldPile as IMeld).NotifyLaidOff -= OnLayOff; }
    }
    //private void OnLayOff(Card card) => Rebuild();

    protected override void PostRebuild() {
        if (CardPile is null || CardPile is not IMeld) { return; }

        PotentialCard.Inspect(card => {
            var meld = CardPile as IMeld;
            int index = meld.IndexIfLaidOff(card);
            if (index != -1) {
                AddCard(card, index);
                var newDisplay = GetChild(index) as Control;
                newDisplay.Modulate = newDisplay.Modulate with { A = 0.5f };
            }
        }).InspectNone(() => {
            foreach (Control child in GetChildren().Cast<Control>()) {
                child.Modulate = child.Modulate with { A = 1f };
            }
        });
    }
    
    public delegate void NotifyLaidOffAction(Card card);
    public event NotifyLaidOffAction NotifyLaidOff;

    public override void _Input(InputEvent @event) {
        if (CardPile is null || CardPile is not IMeld) { return; }

        if (@event is InputEventMouseButton) {
            var mouseButtonEvent = @event as InputEventMouseButton;
            if (mouseButtonEvent.ButtonIndex == MouseButton.Left && !mouseButtonEvent.Pressed) {
                PotentialCard.Inspect(card => {
                    NotifyLaidOff?.Invoke(card);
                    PotentialCard = None;
                });
            }
        }
        if (@event is InputEventMouseMotion) {
            var mouseMotionEvent = @event as InputEventMouseMotion;
            bool mouseOver = GetChildren().Any(display => display.GetNode<Control>("Shadow").GetGlobalRect().HasPoint(mouseMotionEvent.GlobalPosition));
            
            if (!IsMouseOver && mouseOver) {
                // If currently dragging card
                PlayerHand.Inspect(hand => hand.DraggingCard.Inspect(card => {
                    if ((CardPile as IMeld).CouldLayOff(card)) { PotentialCard = card; }
                }));
            }
            if (IsMouseOver && !mouseOver) { PotentialCard = None; }

            IsMouseOver = mouseOver;
        }
    }
}