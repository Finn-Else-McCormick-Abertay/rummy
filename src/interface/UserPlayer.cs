
using Godot;
using Rummy.Game;

namespace Rummy.Interface;

class UserPlayer : Player 
{
    public override string Name { get => "User"; }

    public override void TakeTurn(Round round) {
        Hand.Add((Card)round.Deck.Draw());
        round.DiscardPile.Discard(hand.Cards[0]);
    }
}