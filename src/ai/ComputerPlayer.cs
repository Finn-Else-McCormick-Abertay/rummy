
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Godot;
using Rummy.Game;
using Rummy.Util;
using static Rummy.Util.Option;

namespace Rummy.AI;

[Tool, GlobalClass]
public abstract partial class ComputerPlayer : Player
{
    public ComputerPlayer(string name) : base(name) { }
    public ComputerPlayer() : this(nameof(ComputerPlayer)) { }

    protected new HandInternal Hand => _hand;

    protected (List<Meld> Melds, List<NearMeld> NearMelds) FindPotentialMelds() {
        var (potentialMelds, nearMelds) = PotentialMoves.FindMelds(Hand.Cards);
        if (potentialMelds.Count > 0) Think("Potential Melds: ".ToBuilder().AppendJoin(", ", potentialMelds));
        if (nearMelds.Count > 0) Think("Near Melds: ".ToBuilder().AppendJoin(", ", nearMelds));
        return (potentialMelds, nearMelds);
    }
    protected Dictionary<Card, List<Meld>> FindPotentialLayOffs() {
        var potentialLayOffs = PotentialMoves.FindLayOffs(Hand.Cards, Round);
        if (potentialLayOffs.Count > 0)
            Think("Potential Layoffs".ToBuilder().AppendIf(Melds.None(), " (cannot lay off)").Append(": ")
                .AppendJoin(", ", potentialLayOffs.Select((card, melds) =>
                    $"{card} -> ".ToBuilder().AppendIf(melds.Count > 1, '{').AppendJoin(", ", melds).AppendIf(melds.Count > 1, '}'))));
        return potentialLayOffs;
    }

    protected List<Meld> FindPotentialLayOffs(Card card) => PotentialMoves.FindLayOffs(card, Round);

    protected (List<Card> DrawnCards, Card? CannotDiscard, Card? MustUse) Draw(IDrawable pile, int index) {
        if (pile == Round.Deck && index != 0) { GD.PushWarning("Attempted to draw multiple cards from Deck."); index = 0; }
        var drawnCards = pile.Draw(index + 1);
        drawnCards.ForEach(Hand.Add);

        Card? cannotDiscard = pile == Round.DiscardPile ? drawnCards.First() : null;
        Card? mustUse = pile == Round.DiscardPile && drawnCards.Count > 1 ? drawnCards.Last() : null;

        if (pile == Round.DiscardPile)
            Say("Drew ".ToBuilder().AppendJoin('\u200B', drawnCards).Append(" from discard pile.")
                .AppendIf(mustUse is not null, $" Must use {mustUse}.")
                .AppendIf(cannotDiscard is not null, $" Cannot discard {cannotDiscard}."));
        else SayAndThink("Drew from deck.", $"Drew {drawnCards.SingleOrDefault()} from deck.");

        Think("Hand: ".ToBuilder().AppendJoin('\u200B', Hand.Cards));

        return (drawnCards, cannotDiscard, mustUse);
    }

    protected (List<Card> DrawnCards, Card? CannotDiscard, Card? MustUse) Draw((IDrawable Pile, int Index) drawSelection) => Draw(drawSelection.Pile, drawSelection.Index);
    protected (List<Card> DrawnCards, Card? CannotDiscard, Card? MustUse) Draw(IDrawable pile) => Draw(pile, 0);

    protected void Meld(Meld meld) {
        Say($"Melding {meld}.");
        meld.Cards.ForEach(card => Hand.Pop(card));
        Round.Meld(meld).InspectErr(err => Think($"Failed to meld {meld}: {err}"));
    }

    protected void LayOff(Card card, Meld meld) {
        Say($"Laying off {card} to {meld}.");
        Hand.Pop(card);
        meld.LayOff(card).InspectErr(_ => Think($"Failed to lay off {card} to {meld}."));
    }

    protected void Discard(Card card) {
        Say($"Discarding {card}.");
        Hand.Pop(card);
        Round.DiscardPile.Discard(card);
    }

    // Find usable cards in the discard pile
    protected IEnumerable<(Card Card, int Index, (IEnumerable<Meld> Melds, IEnumerable<Meld> Layoffs, PotentialMoves.RummyConfiguration RummyConfig, IEnumerable<Card> CardsTaken) Info)> FindUsableDiscardPileCards() {
        List<(Card Card, int Index, (IEnumerable<Meld> Melds, IEnumerable<Meld> Layoffs, PotentialMoves.RummyConfiguration RummyConfig, IEnumerable<Card> CardsTaken) Info)> usableDrawDownCards = [];
        foreach (var (index, card) in Round.DiscardPile.Cards.Index()) {
            var cardsTaken = Round.DiscardPile.Cards.Take(index + 1);
            var potentialMeldsWith = PotentialMoves.FindMelds(Hand.Cards.Concat(cardsTaken)).Melds.Where(meld => meld.Cards.Contains(card));
            var potentialLayoffs = Melds.Any() || PotentialMoves.FindMelds(Hand.Cards.Concat(cardsTaken.SkipLast())).Melds.Any() ? FindPotentialLayOffs(card) : [];

            var rummyConfig = PotentialMoves.FindRummyConfiguration(Hand.Cards.Concat(cardsTaken), this, Round);

            // TK - May need to add logic for cards which fit with a partial meld, but don't complete it
            if (potentialMeldsWith.Any() || potentialLayoffs.Any() || rummyConfig is not null)
                usableDrawDownCards.Add((card, index, (potentialMeldsWith, potentialLayoffs, rummyConfig, cardsTaken)));
        }

        if (usableDrawDownCards.Any()) Think("Possible cards to draw down to:\n".ToBuilder().AppendJoin("\n",
            usableDrawDownCards.Select((card, index, info) => "\t- ".ToBuilder().AppendWrapped(info.CardsTaken.Count() > 1 ? "[]" : "",
                info.CardsTaken.SkipLast().AsStrings().Concat(info.CardsTaken.TakeLast().Select(x => $"({x})")).ToJoinedString(", ")))));
        else Think("No usable cards in discard pile.");

        return usableDrawDownCards;
    }
}