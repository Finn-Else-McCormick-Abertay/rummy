
using Godot;
using Rummy.Game;

namespace Rummy.AI;

class ComputerPlayer : Player 
{
    public ComputerPlayer() : base("ComputerPlayer") {}
    
    public override void OnAddedToRound(Round round) {}
    public override void OnRemovedFromRound(Round round) {}

    public override void BeginTurn(Round round) {
        //round.Deck.Draw().Inspect(card => Hand.Add(card));
        //Hand.PopAt(0).Inspect(card => round.DiscardPile.Discard(card));
        round.DiscardPile.Draw(2).ForEach(card => Hand.Add(card));
        Hand.PopAt(Hand.Count - 2).Inspect(card => round.DiscardPile.Discard(card));
        round.EndTurn();
    }
}