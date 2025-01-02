
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Godot;

namespace Rummy.Game;

public class Round
{
	public bool Finished { get; private set; } = false;
	public Player Winner { get; private set; }

	private readonly List<Player> _players;
	public ReadOnlyCollection<Player> Players { get => _players.AsReadOnly(); }

	public int Turn { get; private set; } = 0;
	public Player CurrentPlayer { get => Players[Turn % Players.Count]; }

	public Deck Deck { get; init; } = new();
	public DiscardPile DiscardPile { get; init; } = new();

	public ReadOnlyCollection<IMeld> Melds {
		get {
			List<IMeld> melds = new();
			foreach (Player player in Players) {
				melds = melds.Concat(player.Melds).ToList();
			}
			return melds.AsReadOnly();
		}
	}
	public bool Meld(IMeld meld) {
		if (meld.Valid) {
			CurrentPlayer.Melds.Add(meld);
			(meld as CardPile).OnCardAdded += (card) => { turnData.LayOffCount++; };
			turnData.MeldCount++;
			turnData.Melds.Add(meld);
			return true;
		}
		return false;
	}

	private class TurnData {
		public int DrawCountDeck = 0, DrawCountDiscard = 0, MeldCount = 0, LayOffCount = 0, DiscardCount = 0;
		public Card BottomCard, TopCard;
		public List<IMeld> Melds = new();
	}
	private TurnData turnData = new();

	public Round(List<Player> players, int numPacks = 1) {
		_players = players;
		foreach (Player player in Players) {
			player.Hand.Reset();
			player.Melds.Clear();
		}
		
		DiscardPile.OnCardAdded += (card) => { turnData.DiscardCount++; };
		Deck.OnCardDrawn += (card) => {
			turnData.DrawCountDeck++;
			turnData.BottomCard = card; turnData.TopCard = card;
		};
		DiscardPile.OnCardDrawn += (card) => {
			if (turnData.DrawCountDiscard == 0) { turnData.TopCard = card; }
			turnData.DrawCountDiscard++;
			turnData.BottomCard = card;
		};

		Deck.OnEmptied += () => {
			Deck.Append(DiscardPile);
			Deck.Flip();
			DiscardPile.Clear();
			DiscardPile.Discard((Card)Deck.Draw());
			turnData.DrawCountDeck--;
		};

		for (int i = 0; i < numPacks; ++i) { Deck.AddPack(); }
		Deck.Shuffle();

		// Deal to players
		int handSize = 10;
		if (Players.Count >= 4) { handSize = 7; }
		else if (Players.Count >= 6) { handSize = 6; }
		
		for (int i = 0; i < handSize; ++i) {
			foreach (Player player in Players) {
				player.Hand.Add((Card)Deck.Draw());
			}
		}

		// Put one card into discard pile
		DiscardPile.Discard((Card)Deck.Draw());
	}

	public void ProgressTurn() {
		turnData = new TurnData(); var priorMelds = CurrentPlayer.Melds.Count;
		CurrentPlayer.TakeTurn(this);

		// Checks
		if (turnData.DrawCountDeck > 0 && turnData.DrawCountDiscard > 0) { GD.PushError(CurrentPlayer.Name, " drew from both deck and discard pile."); }
		else if (turnData.DrawCountDeck > 1) { GD.PushError(CurrentPlayer.Name, " drew from deck ", turnData.DrawCountDeck, " times."); }
		if (turnData.DrawCountDiscard > 0 && turnData.TopCard == DiscardPile.Cards[0] && !!CurrentPlayer.Hand.Empty) { GD.PushError(CurrentPlayer.Name, " discarded top picked up card while not going out"); }
		if (turnData.DrawCountDiscard > 1 && turnData.MeldCount == 0) { GD.PushError(CurrentPlayer.Name," picked up ", turnData.DrawCountDiscard, " cards but did not meld."); }
		if (turnData.DrawCountDiscard > 1) {
			bool usedBottomCard = false;
			turnData.Melds.ForEach((meld) => { usedBottomCard |= meld.Cards.Contains(turnData.BottomCard); });
			if (!usedBottomCard) { GD.PushError(CurrentPlayer.Name, " picked up ", turnData.DrawCountDiscard, " cards but did not use bottomost card."); }
		}

		if (turnData.LayOffCount > 0 && CurrentPlayer.Melds.Count == 0) { GD.PushError(CurrentPlayer.Name, " layed off before melding."); }

		if (turnData.DiscardCount > 1) { GD.PushError(CurrentPlayer.Name, " discarded ", turnData.DiscardCount, " times in one round"); }
		else if (turnData.DiscardCount == 0 && !CurrentPlayer.Hand.Empty) { GD.PushError(CurrentPlayer.Name, " ended turn without discarding"); }

		if (turnData.MeldCount > 1 && !CurrentPlayer.Hand.Empty) { GD.PushError(CurrentPlayer.Name, " multiple melds without rummying"); }
		else if (turnData.MeldCount > 1 && priorMelds != 0) { GD.PushError(CurrentPlayer.Name, "attempted rummy with a meld already down"); }

		// Game End
		if (CurrentPlayer.Hand.Empty) {
			Finished = true;
			Winner = CurrentPlayer;
			int roundScore = 0;
			foreach (Player player in Players) {
				if (player == Winner) { continue; }
				roundScore += player.Hand.Score();
			}
			Winner.Score += roundScore;
		}
		Turn++;
	}	
}