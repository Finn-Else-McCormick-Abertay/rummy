
using System;
using System.Collections.Generic;
using Rummy.Game;

namespace Rummy.AI;

class RandomPlayer : Player 
{
    public RandomPlayer(Random randomParam) : base("RandomPlayer") {
        random = randomParam;
    }

    private readonly Random random;
    
    public override void OnAddedToRound(Round round) {}
    public override void OnRemovedFromRound(Round round) {}

    public override void BeginTurn(Round round) {
        List<Card> drawnCards = new();

        int drawSelection = random.Next(2);
        // Draw from deck
        if (drawSelection == 0) {
            round.Deck.Draw().Inspect(card => drawnCards.Add(card));
        }
        // Draw from discard
        else {
            round.DiscardPile.Draw().Inspect(card => drawnCards.Add(card));
        }

        drawnCards.ForEach(card => Hand.Add(card));

        Hand.PopAt(random.Next(Hand.Count)).Inspect(card => round.DiscardPile.Discard(card));
        round.EndTurn();
    }
}