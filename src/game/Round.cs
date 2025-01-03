
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Godot;

namespace Rummy.Game;

public class Round
{
	private readonly List<Player> _players;
	public ReadOnlyCollection<Player> Players { get => _players.AsReadOnly(); }

	public int Turn { get; private set; } = 0;
	public bool MidTurn { get; private set; } = false;
	public Player CurrentPlayer { get => Players[Turn % Players.Count]; }
	
	public bool Finished { get; private set; } = false;
	public Player Winner { get; private set; }

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
			(meld as CardPile).OnCardAdded += (card) => {
				turnData.LayOffCount++;
				turnData.LayOffs.Add(card);
			};
			turnData.MeldCount++;
			turnData.Melds.Add(meld);
			return true;
		}
		return false;
	}

	private class TurnData {
		public int DrawCountDeck = 0, DrawCountDiscard = 0, MeldCount = 0, PriorMelds = 0, LayOffCount = 0, DiscardCount = 0;
		public Card BottomCard, TopCard;
		public List<IMeld> Melds = new();
		public List<Card> LayOffs = new();
	}
	private TurnData turnData = new();

	public bool HasDrawn { get => turnData.DrawCountDeck > 0 || turnData.DrawCountDiscard > 0; }

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

	public void BeginTurn() {
        turnData = new TurnData { PriorMelds = CurrentPlayer.Melds.Count };
		MidTurn = true;
		CurrentPlayer.BeginTurn(this);
	}

	public bool EndTurn() {
		if (!IsTurnValid()) { return false; } 

		// Game End
		if (CurrentPlayer.Hand.Empty) {
			Finished = true;
			Winner = CurrentPlayer;
			int roundScore = 0;
			foreach (Player player in Players) {
				if (player == Winner) { continue; }
				roundScore += player.Hand.Score();
			}
			// Rummied, score doubled
			if (turnData.MeldCount > 1) { roundScore *= 2; }
			Winner.Score += roundScore;
		}
		Turn++; MidTurn = false;
		return true;
	}

	private bool IsTurnValid() {
		// Drew from deck and discard
		if (turnData.DrawCountDeck > 0 && turnData.DrawCountDiscard > 0) {
			GD.PushWarning(CurrentPlayer.Name, " drew from both deck and discard pile.");
			return false;
		}
		// Drew multiple from deck
		if (turnData.DrawCountDeck > 1) {
			GD.PushWarning(CurrentPlayer.Name, " drew from deck ", turnData.DrawCountDeck, " times.");
			return false;
		}
		// Discarded top card from discard pickup while not going out
		if (turnData.DrawCountDiscard > 0 && turnData.TopCard == DiscardPile.Cards[0] && !!CurrentPlayer.Hand.Empty) {
			GD.PushWarning(CurrentPlayer.Name, " discarded top picked up card while not going out");
			return false;
		}

		// Drew multiple...
		if (turnData.DrawCountDiscard > 1) {
			bool usedBottomCard = turnData.LayOffs.Contains(turnData.BottomCard);
			turnData.Melds.ForEach((meld) => { usedBottomCard |= meld.Cards.Contains(turnData.BottomCard); });
			// ...but did not use bottomost card
			if (!usedBottomCard) {
				GD.PushWarning(CurrentPlayer.Name, " drew ", turnData.DrawCountDiscard, " cards but did not use bottomost card.");
				return false;
			}
		}

		// Laid off before having melded
		if (turnData.LayOffCount > 0 && CurrentPlayer.Melds.Count == 0) {
			GD.PushWarning(CurrentPlayer.Name, " layed off before melding.");
			return false;
		}

		// Discarded more than once
		if (turnData.DiscardCount > 1) {
			GD.PushWarning(CurrentPlayer.Name, " discarded ", turnData.DiscardCount, " times in one round");
			return false;
		}
		// Ended turn without discarding or going out
		if (turnData.DiscardCount == 0 && !CurrentPlayer.Hand.Empty) {
			GD.PushWarning(CurrentPlayer.Name, " ended turn without discarding");
			return false;
		}

		// Melded more than once but did not go out, to constitute a rummy
		if (turnData.MeldCount > 1 && !CurrentPlayer.Hand.Empty) {
			GD.PushWarning(CurrentPlayer.Name, " attempted multiple melds without rummying");
			return false;
		}
		// Rummied (melded multiple) but had already melded
		if (turnData.MeldCount > 1 && turnData.PriorMelds != 0) {
			GD.PushWarning(CurrentPlayer.Name, " attempted rummy with a meld already down");
			return false;
		}

		return true;
	}
}