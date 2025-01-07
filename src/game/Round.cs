
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Rummy.Util;
using Godot;

namespace Rummy.Game;

public class Round
{
	private readonly List<Player> _players;
	public ReadOnlyCollection<Player> Players { get => _players.AsReadOnly(); }

	public int Turn { get; private set; } = 0;
	public bool MidTurn { get; private set; } = false;
	public Player CurrentPlayer => Players[Turn % Players.Count];
	
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
	public Result Meld(IMeld meld) {
		if (meld.Valid) {
			CurrentPlayer.Melds.Add(meld);
			Player meldPlayer = CurrentPlayer;
			(meld as CardPile).OnCardAdded += (card) => {
				if (!turnData.LayOffs.ContainsKey(meldPlayer)) { turnData.LayOffs.Add(meldPlayer, new()); }
				var index = meldPlayer.Melds.FindIndex(x => x == meld);
				if (!turnData.LayOffs[meldPlayer].ContainsKey(index)) { turnData.LayOffs[meldPlayer].Add(index, new()); }
				turnData.LayOffs[meldPlayer][index].Add(card);
			};
			turnData.Melds.Add(meld);
			return Result.Ok();
		}
		return Result.Err($"{meld} is not a valid meld.");
	}

	private class TurnData {
		public int PriorMelds = 0;
		public List<Card> DrawnCardsDeck = new(), DrawnCardsDiscardPile = new();
		public List<Card> Discards = new();
		public List<IMeld> Melds = new();
		public Dictionary<Player, Dictionary<int, List<Card>>> LayOffs = new();
		public List<Card> LaidOffCards => LayOffs.SelectMany(kvp => kvp.Value.SelectMany(kvp => kvp.Value)).ToList();
	}
	private TurnData turnData = new();

	public bool HasDrawn { get => turnData.DrawnCardsDeck.Count > 0 || turnData.DrawnCardsDiscardPile.Count > 0; }

	public Round(List<Player> players, int numPacks = 1) {
		_players = players;
		foreach (Player player in Players) {
			player.Hand.Reset();
			player.Melds.Clear();
		}
		
		DiscardPile.OnCardAdded += (card) => { turnData.Discards.Add(card); };
		Deck.OnCardDrawn += (card) => { turnData.DrawnCardsDeck.Add(card); };
		DiscardPile.OnCardDrawn += (card) => { turnData.DrawnCardsDiscardPile.Add(card); };

		Deck.OnEmptied += () => {
			Deck.Append(DiscardPile);
			Deck.Flip();
			DiscardPile.Clear();
			var starterCard = Deck.Draw();
			DiscardPile.Discard(starterCard);
			// Remove those from the turn data since they weren't real, they were just starting the discard pile
			turnData.Discards.Remove(starterCard);
			turnData.DrawnCardsDeck.Remove(starterCard);
		};

		for (int i = 0; i < numPacks; ++i) { Deck.AddPack(); }
		Deck.Shuffle();

		// Deal to players
		int handSize = 10;
		if (Players.Count >= 4) { handSize = 7; }
		else if (Players.Count >= 6) { handSize = 6; }
		
		for (int i = 0; i < handSize; ++i) {
			foreach (Player player in Players) {
				player.Hand.Add(Deck.Draw());
			}
		}

		// Put one card into discard pile
		DiscardPile.Discard(Deck.Draw());
	}

	public void BeginTurn() {
        turnData = new TurnData { PriorMelds = CurrentPlayer.Melds.Count };
		MidTurn = true;
		CurrentPlayer.BeginTurn(this);
	}

	public Result EndTurn() {
		var valid = IsTurnValid();
		if (!valid) { return valid; }

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
			if (turnData.Melds.Count > 1) { roundScore *= 2; }
			Winner.Score += roundScore;
		}
		Turn++; MidTurn = false;
		return Result.Ok();
	}

	public void ResetTurn() {
		// Undo draws
		turnData.DrawnCardsDeck.ForEach(card => {
			Deck.InternalUndoDraw(card);
			(CurrentPlayer.Hand as IAccessibleCardPile).Cards.Remove(card);
		});
		turnData.DrawnCardsDeck.Clear();
		turnData.DrawnCardsDiscardPile.ForEach(card => {
			DiscardPile.InternalUndoDraw(card);
			(CurrentPlayer.Hand as IAccessibleCardPile).Cards.Remove(card);
		});
		turnData.DrawnCardsDiscardPile.Clear();

		// Undo discards
		turnData.Discards.ForEach(card => {
			DiscardPile.InternalUndoDiscard(card);
			CurrentPlayer.Hand.Add(card);
		});
		turnData.Discards.Clear();

		// Undo layoffs
		turnData.LayOffs.Keys.ToList().ForEach(player => {
			turnData.LayOffs[player].Keys.ToList().ForEach(index => {
				turnData.LayOffs[player][index].ForEach(card => {
					player.Melds.ElementAt(index).InternalUndoLayOff(card);
					CurrentPlayer.Hand.Add(card);
				});
			});
		});
		turnData.LayOffs.Clear();

		// Undo melds
		turnData.Melds.ForEach(meld => {
			meld.Cards.ToList().ForEach(card => {
				CurrentPlayer.Hand.Add(card);
			});
			CurrentPlayer.Melds.Remove(meld);
		});
	}

	private Result IsTurnValid() {
		// Drew from deck and discard
		if (turnData.DrawnCardsDeck.Count > 0 && turnData.DrawnCardsDiscardPile.Count > 0) {
			return Result.Err($"{CurrentPlayer.Name} drew from both deck and discard pile.");
		}
		// Drew multiple from deck
		if (turnData.DrawnCardsDeck.Count > 1) {
			return Result.Err($"{CurrentPlayer.Name} drew from {turnData.DrawnCardsDeck.Count} cards from deck.");
		}

		// Discarded top card from discard pickup while not going out
		if (turnData.DrawnCardsDiscardPile.Count > 0 && turnData.Discards.Last() == DiscardPile.Cards.First() && !CurrentPlayer.Hand.Empty) {
			return Result.Err($"{CurrentPlayer.Name} discarded top card pick up from discard pile while not going out.");
		}

		// Drew multiple but did not use bottomost card
		if (turnData.DrawnCardsDiscardPile.Count > 1 &&
			!turnData.Melds.Any(meld => meld.Cards.Contains(turnData.Discards.First())) &&
			!turnData.LaidOffCards.Contains(turnData.Discards.First())) {
			
			return Result.Err($"{CurrentPlayer.Name} drew {turnData.DrawnCardsDiscardPile.Count} cards but did not use bottomost card ({turnData.Discards.First()}).");
		}

		// Laid off before having melded
		if (turnData.LayOffs.Count > 0 && CurrentPlayer.Melds.Count == 0) {
			return Result.Err($"{CurrentPlayer.Name} layed off before having melded.");
		}

		// Discarded more than once
		if (turnData.Discards.Count > 1) {
			return Result.Err($"{CurrentPlayer.Name} discarded more than once.");
		}
		// Ended turn without discarding or going out
		if (turnData.Discards.Count == 0 && !CurrentPlayer.Hand.Empty) {
			return Result.Err($"{CurrentPlayer.Name} ended turn without discarding.");
		}

		// Melded more than once but did not go out, to constitute a rummy
		if (turnData.Melds.Count > 1 && !CurrentPlayer.Hand.Empty) {
			return Result.Err($"{CurrentPlayer.Name} melded {turnData.Melds.Count} times in one turn without going out.");
		}
		// Rummied (melded multiple) but had already melded
		if (turnData.Melds.Count > 1 && turnData.PriorMelds != 0) {
			return Result.Err($"{CurrentPlayer.Name} attempted to rummy with a meld already down.");
		}

		return Result.Ok();
	}
}