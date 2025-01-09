
using Godot;
using Rummy.Game;

namespace Rummy.AI;

class ComputerPlayer : Player 
{
    public ComputerPlayer(string name = "Computer") : base() {
        _name = name;
    }

    private string _name;
    public override string Name { get => _name; }

    public override void BeginTurn(Round round) {
        round.Deck.Draw().Inspect(card => Hand.Add(card));
        Hand.PopAt(0).Inspect(card => round.DiscardPile.Discard(card));
        round.EndTurn();
    }
}