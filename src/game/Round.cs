
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Rummy.Util;
using static Rummy.Util.Option;
using static Rummy.Util.Result;
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
	public Result<Unit, string> Meld(IMeld meld) {
		if (meld.Valid) {
			CurrentPlayer.Melds.Add(meld);
			Player meldPlayer = CurrentPlayer;
			(meld as CardPile).OnCardAdded += (card) => {
				if (!turnData.LayOffs.ContainsKey(meldPlayer)) { turnData.LayOffs.Add(meldPlayer, new()); }
				var index = meldPlayer.Melds.FindIndex(x => x == meld);
				if (!turnData.LayOffs[meldPlayer].ContainsKey(index)) { turnData.LayOffs[meldPlayer].Add(index, new()); }
				turnData.LayOffs[meldPlayer][index].Add(card);
			};
			turnData.Melds.Add(meld.Clone());
			return Ok();
		}
		return Err($"{meld} is not a valid meld.");
	}

	private class TurnData {
		public int PriorMelds = 0;
		public List<Card> DrawnCardsDeck = new(), DrawnCardsDiscardPile = new();
		public List<Card> Discards = new();
		public List<IMeld> Melds = new();
		public Dictionary<Player, Dictionary<int, List<Card>>> LayOffs = new();
		public List<Card> LaidOffCards => LayOffs.SelectMany(kvp => kvp.Value.SelectMany(kvp => kvp.Value)).ToList();

        public override string ToString() {
			//var priorMelds = $"Prior Melds: {PriorMelds}";
			var drawnCards = (DrawnCardsDeck.Count > 0) && (DrawnCardsDiscardPile.Count > 0) ?
				$"Drawn: Deck: ({string.Join(", ", DrawnCardsDeck)}), Discard Pile: ({string.Join(", ", DrawnCardsDiscardPile)})" :
				(DrawnCardsDeck.Count > 0) ? $"Drawn (Deck): ({string.Join(", ", DrawnCardsDeck)})" :
				(DrawnCardsDiscardPile.Count > 0) ? $"Drawn (Discard Pile): ({string.Join(", ", DrawnCardsDiscardPile)})" :
				"Drawn: None";
			var discards = $"Discarded: ({string.Join(", ", Discards)})";
			var melds = (Melds.Count > 0) ? $", Melded: {string.Join(", ", Melds)}" : "";
			var laidOff = (LaidOffCards.Count > 0) ? $", Laid Off: {string.Join(", ", LaidOffCards)}" : "";
			return $"{{{drawnCards}, {discards}{melds}{laidOff}}}";
		}
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
			GD.Print("Flipping over discard pile");
			Deck.Append(DiscardPile);
			Deck.Flip();
			DiscardPile.Clear();
			Deck.Draw().Inspect(card => {
				DiscardPile.Discard(card);
				// Remove these from the turn data since they weren't real, they were just starting the discard pile
				turnData.Discards.Remove(card); turnData.DrawnCardsDeck.Remove(card);
			});
		};

		for (int i = 0; i < numPacks; ++i) { Deck.AddPack(); }
		Deck.Shuffle();

		// Deal to players
		int handSize = 10;
		if (Players.Count >= 4) { handSize = 7; }
		else if (Players.Count >= 6) { handSize = 6; }
		
		for (int i = 0; i < handSize; ++i) {
			foreach (Player player in Players) {
				Deck.Draw().Inspect(card => player.Hand.Add(card));
			}
		}

		// Put one card into discard pile
		Deck.Draw().Inspect(card => DiscardPile.Discard(card));

		turnData = new();
	}

	public void BeginTurn() {
        turnData = new TurnData { PriorMelds = CurrentPlayer.Melds.Count };
		MidTurn = true;
		CurrentPlayer.BeginTurn(this);
	}

	public Result<Unit, string> EndTurn() {
		var turnResult = IsTurnValid();
		if (turnResult.IsErr) { return turnResult; }

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
		return Ok();
	}

	public delegate void NotifyResetAction();
	public event NotifyResetAction NotifyReset;

	public void ResetTurn() {
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
			int index = CurrentPlayer.Melds.FindIndex(x => x.Equals(meld));
			if (index == -1) { GD.PushWarning($"Tried to reset nonexistent meld {meld}."); }
			else {
				CurrentPlayer.Melds.RemoveAt(index);
			}
		});
		turnData.Melds.Clear();

		// Undo draws
		turnData.DrawnCardsDeck.Reverse();
		turnData.DrawnCardsDeck.ForEach(card => {
			Deck.InternalUndoDraw(card);
			(CurrentPlayer.Hand as IAccessibleCardPile).Cards.Remove(card);
		});
		turnData.DrawnCardsDeck.Clear();
		turnData.DrawnCardsDiscardPile.Reverse();
		turnData.DrawnCardsDiscardPile.ForEach(card => {
			DiscardPile.InternalUndoDraw(card);
			(CurrentPlayer.Hand as IAccessibleCardPile).Cards.Remove(card);
		});
		turnData.DrawnCardsDiscardPile.Clear();

		NotifyReset?.Invoke();
	}

	private Result<Unit, string> IsTurnValid() {
		GD.Print($"{Turn} ({CurrentPlayer.Name}[{Players.ToList().FindIndex(x => x == CurrentPlayer)}]) {turnData}");

		if (turnData.DrawnCardsDeck.Count == 0 && turnData.DrawnCardsDiscardPile.Count == 0) {
			return Err("$Did not draw.");
		}

		if (turnData.DrawnCardsDeck.Count > 0 && turnData.DrawnCardsDiscardPile.Count > 0) {
			return Err($"Drew from both deck and discard pile.");
		}
		if (turnData.DrawnCardsDeck.Count > 1) {
			return Err($"Drew {turnData.DrawnCardsDeck.Count} cards from deck.");
		}

		if (turnData.DrawnCardsDiscardPile.Count > 0 && turnData.Discards.Last() == turnData.DrawnCardsDiscardPile.First() && !CurrentPlayer.Hand.Empty) {
			return Err($"Discarded top card picked up from discard pile while not going out.");
		}

		if (turnData.DrawnCardsDiscardPile.Count > 1 &&
			!turnData.Melds.Any(meld => meld.Cards.Contains(turnData.DrawnCardsDiscardPile.Last())) &&
			!turnData.LaidOffCards.Contains(turnData.DrawnCardsDiscardPile.Last())) {
			
			return Err($"Drew {turnData.DrawnCardsDiscardPile.Count} cards but did not use bottommost card ({turnData.DrawnCardsDiscardPile.Last()}).");
		}

		if (turnData.LayOffs.Count > 0 && CurrentPlayer.Melds.Count == 0) {
			return Err($"Layed off before having melded.");
		}

		if (turnData.Discards.Count > 1) {
			return Err($"Discarded more than once.");
		}
		if (turnData.Discards.Count == 0 && !CurrentPlayer.Hand.Empty) {
			return Err($"Ended turn without discarding.");
		}

		if (turnData.Melds.Count > 1 && !CurrentPlayer.Hand.Empty) {
			return Err($"Melded {turnData.Melds.Count} times in one turn without going out.");
		}
		if (turnData.Melds.Count > 1 && turnData.PriorMelds != 0) {
			return Err($"Attempted to rummy with a meld already down.");
		}

		return Ok();
	}
}