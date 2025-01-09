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

    protected override void OnCardMouseOver(CardDisplay display, bool entering) {
        if (CardPile is null || CardPile is not IMeld || !CanLayOff) { return; }
        
        if (entering) {
            // Currently dragging card
            PlayerHand.Inspect(hand => hand.DraggingCard.Inspect(card => {
                if ((CardPile as IMeld).CanLayOff(card)) {
                    PotentialCard = card;
                }
            }));
        }
        else { PotentialCard = None; }
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
            //bool mouseOver = GetChildren().Cast<Control>().Any(child => child.GetGlobalRect().HasPoint(mouseMotionEvent.GlobalPosition));
            //GD.Print($"Mouse Over {mouseOver}");
            //if (!mouseOver) {
            //    PotentialCard = None;
            //}
        }
    }
}