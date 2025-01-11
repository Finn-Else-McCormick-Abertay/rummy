
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Rummy.Util;
using static Rummy.Util.Option;
using static Rummy.Util.Result;
using Godot;
using System.Collections.Immutable;
using System;

namespace Rummy.Game;

public class Round
{
	private class TurnData {
		public TurnData(Player player) {
			Player = player;
			PriorMelds = Player.Melds.Count;
		}

		public readonly Player Player;
		public readonly int PriorMelds;
		public List<Card> DrawnCardsDeck = new(), DrawnCardsDiscardPile = new();
		public List<Card> Discards = new();
		public List<IMeld> Melds = new();
		public Dictionary<Player, Dictionary<int, List<Card>>> LayOffs = new();

		public List<Card> LaidOffCards => LayOffs.SelectMany(kvp => kvp.Value.SelectMany(kvp => kvp.Value)).ToList();

        public override string ToString() {
			var info = new List<string>();
			var drawnCardsDeckString = string.Join(", ", DrawnCardsDeck);
			var drawnCardsDiscardPileString = string.Join(", ", DrawnCardsDiscardPile);
			if (DrawnCardsDeck.Count > 0 && DrawnCardsDiscardPile.Count > 0) {
				info.Add($"Drawn: [Deck: ({drawnCardsDeckString}), Discard Pile: ({drawnCardsDiscardPileString})]");
			}
			else if (DrawnCardsDeck.Count > 0) {
				info.Add($"Drawn (Deck): {(DrawnCardsDeck.Count > 1 ? $"({drawnCardsDeckString})" : $"{drawnCardsDeckString}")}");
			}
			else if (DrawnCardsDiscardPile.Count > 0) {
				info.Add($"Drawn (Discard Pile): {(DrawnCardsDiscardPile.Count > 1 ? $"({drawnCardsDiscardPileString})" : $"{drawnCardsDiscardPileString}")}");
			}

			var discardsString = string.Join(", ", Discards);
			info.Add($"Discarded: {(Discards.Count == 0 ? "None" : Discards.Count == 1 ? $"{discardsString}" : $"({discardsString})")}");

			if (Melds.Count > 0) { info.Add($"Melded: {string.Join(", ", Melds)}"); }
			if (LaidOffCards.Count > 0) { info.Add($"Laid Off: {string.Join(", ", LaidOffCards)}"); }
			if (Melds.Count > 1) { info.Add($"Prior Melds: {PriorMelds}"); }

			if (Player.Hand.Count == 0) { info.Add($"Went out{(Melds.Count > 0 ? " with rummy" : "")}"); }
			else { info.Add($"{Player.Hand.Count} left in hand"); }

			IsValid().InspectErr(err => info.Add($"Invalid: {err}"));

			return $"{{{string.Join(", ", info)}}}";
		}

		// Is valid (completed) turn?
		public Result<Unit, string> IsValid() {
			var errs = new List<string>();
			if (DrawnCardsDeck.Count == 0 && DrawnCardsDiscardPile.Count == 0) { errs.Add("$Did not draw."); }
			if (DrawnCardsDeck.Count > 0 && DrawnCardsDiscardPile.Count > 0) { errs.Add($"Drew from both deck and discard pile."); }
			if (DrawnCardsDeck.Count > 1) { errs.Add($"Drew {DrawnCardsDeck.Count} cards from deck."); }

			if (DrawnCardsDiscardPile.Count > 0 && Discards.Last() == DrawnCardsDiscardPile.First() && !Player.Hand.Empty) {
				errs.Add($"Discarded top card drawn from discard pile without going out.");
			}
			if (DrawnCardsDiscardPile.Count > 1) {
				var meldedLast = Melds.Any(meld => meld.Cards.Contains(DrawnCardsDiscardPile.Last()));
				var laidOffLast = LaidOffCards.Contains(DrawnCardsDiscardPile.Last());
				if (!meldedLast && !laidOffLast) {
					errs.Add($"Drew {DrawnCardsDiscardPile.Count} cards but did not use bottommost card ({DrawnCardsDiscardPile.Last()}).");
				}
			}

			if (LayOffs.Count > 0 && Player.Melds.Count == 0) { errs.Add($"Laid off before having melded."); }
			
			if (Melds.Count > 1 && !Player.Hand.Empty) { errs.Add($"Melded {Melds.Count} times in one turn without going out."); }
			if (Melds.Count > 1 && PriorMelds != 0) { errs.Add($"Attempted to rummy with a meld already down."); }

			if (Discards.Count > 1) { errs.Add($"Discarded more than once."); }
			if (Discards.Count == 0 && !Player.Hand.Empty) { errs.Add($"Ended turn without discarding or going out."); }

			if (errs.Count > 0) { return Err(string.Join(" ", errs)); } 
			return Ok();
		}

		public Result<TurnRecord, string> AsTurnRecord() {
			var result = IsValid();
			if (result.IsErr) { return result; }

			return Ok(new TurnRecord(
				Player, Discards.Count == 0 ? None : Some(Discards.Last()),
				(DrawnCardsDeck.Count == 1 ? new List<Option<Card>>{ None } : DrawnCardsDiscardPile.ConvertAll(x => Some(x))).ToImmutableArray(),
				Melds.ConvertAll(x => x.Clone()).ToImmutableArray(),
				LaidOffCards.ToImmutableArray()
			));
		}
    }
	private TurnData turnData;

	// Record of a (valid) turn
	public class TurnRecord {
		public TurnRecord(Player player, Option<Card> discardedCard,
							ImmutableArray<Option<Card>> drawnCards, ImmutableArray<IMeld> melds, ImmutableArray<Card> laidOffCards) {
			Player = player; DiscardedCard = discardedCard; DrawnCards = drawnCards; Melds = melds; LaidOffCards = laidOffCards;
		}

		public readonly Player Player;
		public readonly Option<Card> DiscardedCard; // It is valid for the final turn to end without a discard
		public readonly ImmutableArray<Option<Card>> DrawnCards; // 'None' in this instance is an unknown card, ie: a card drawn from the deck
		public readonly ImmutableArray<IMeld> Melds;
		public readonly ImmutableArray<Card> LaidOffCards;
	}
	
	public delegate void NotifyTurnBeganAction(Player player);
	public delegate void NotifyTurnEndedAction(Player player, Result<TurnRecord, string> result);
	public delegate void NotifyTurnResetAction();
	public delegate void NotifyGameEndedAction(Player winner, int roundScore, bool wasRummy);
	
	public delegate void NotifyDiscardPileRanOutAction();

	public delegate void NotifyDrewFromDeckAction(Player player);
	public delegate void NotifyDrewFromDiscardPileAction(Player player, ReadOnlyCollection<Card> cards);
	public delegate void NotifyMeldedAction(Player player, ReadOnlyCollection<Card> cards);
	public delegate void NotifyLaidOffAction(Player player, Card card);
	public delegate void NotifyDiscardedAction(Player player, Card card);

	// These events are mainly for the frontend, and fire exactly when it happens
	public event NotifyTurnBeganAction NotifyTurnBegan;
	public event NotifyTurnEndedAction NotifyTurnEnded;
	public event NotifyTurnResetAction NotifyTurnReset;
	public event NotifyGameEndedAction NotifyGameEnded;
	
	public event NotifyDiscardPileRanOutAction NotifyDiscardPileRanOut;

	// These events are intended for computer players and fire on turn end rather than when the event happens
	// so as to avoid having to deal with actions which are undone by turn resetting
	public event NotifyDrewFromDeckAction NotifyDrewFromDeck;
	public event NotifyDrewFromDiscardPileAction NotifyDrewFromDiscardPile;
	public event NotifyMeldedAction NotifyMelded;
	public event NotifyLaidOffAction NotifyLaidOff;
	public event NotifyDiscardedAction NotifyDiscarded;

	private readonly List<Player> _players;
	public ReadOnlyCollection<Player> Players { get => _players.AsReadOnly(); }

	public int Turn { get; private set; } = 0;
	public bool MidTurn { get; private set; } = false;
	public Player CurrentPlayer => Players[Turn % Players.Count];
	public Player NextPlayer => Players[(Turn + 1) % Players.Count];
	
	public bool Finished { get; private set; } = false;
	public Player Winner { get; private set; }

	public Deck Deck { get; init; } = new();
	public DiscardPile DiscardPile { get; init; } = new();

	public bool HasDrawn { get => turnData.DrawnCardsDeck.Count > 0 || turnData.DrawnCardsDiscardPile.Count > 0; }

	private readonly Dictionary<(Player, int), int> _meldOrder = new();
	public ReadOnlyCollection<IMeld> Melds => Players.ToList()
		.Aggregate(new List<(Player, IMeld)>(), (melds, player) => melds.Concat(player.Melds.ConvertAll(x => (player, x))).ToList())
		.OrderBy(pair => _meldOrder[(pair.Item1, pair.Item1.Melds.FindIndex(x => x == pair.Item2))]).ToList()
		.ConvertAll(pair => pair.Item2).AsReadOnly();
	
	public Result<Unit, string> Meld(IMeld meld) {
		if (meld.Valid) {
			CurrentPlayer.Melds.Add(meld);
			Player meldPlayer = CurrentPlayer;
			(meld as CardPile).OnCardAdded += (card) => {
				turnData.LayOffs
					.GetOrCreate(meldPlayer)
					.GetOrCreate(meldPlayer.Melds.FindIndex(x => x == meld))
					.Add(card);
				NotifyLaidOff?.Invoke(CurrentPlayer, card);
			};
			turnData.Melds.Add(meld.Clone());
			var orderKey = (meldPlayer, meldPlayer.Melds.FindIndex(x => x == meld));
			_meldOrder.GetOrAdd(orderKey, 0);
			_meldOrder[orderKey] = Melds.Count;
			return Ok();
		}
		return Err($"{meld} is not a valid meld.");
	}

	public Round(List<Player> players, int numPacks = 1) : this(players, Random.Shared, numPacks) {}

	public Round(List<Player> players, Random random, int numPacks = 1) {
		_players = players;
		foreach (Player player in Players) {
			player.Hand.Reset();
			player.Melds.Clear();
			player.OnAddedToRound(this);
		}
		
		// Set up deck
		for (int i = 0; i < numPacks; ++i) { Deck.AddPack(); }
		Deck.Shuffle(random);
		
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
		
		// Add required callbacks
		DiscardPile.OnCardAdded += (card) => { turnData.Discards.Add(card); };
		Deck.OnCardDrawn += (card) => { turnData.DrawnCardsDeck.Add(card); };
		DiscardPile.OnCardDrawn += (card) => { turnData.DrawnCardsDiscardPile.Add(card); };

		Deck.OnEmptied += () => {
			NotifyDiscardPileRanOut?.Invoke();
			Deck.Append(DiscardPile);
			Deck.Flip();
			DiscardPile.Clear();
			Deck.Draw().Inspect(card => {
				DiscardPile.Discard(card);
				// Remove these from the turn data since they weren't real, they were just starting the discard pile
				turnData.Discards.Remove(card); turnData.DrawnCardsDeck.Remove(card);
			});
		};
	}

	~Round() {
		foreach (Player player in Players) {
			player.OnRemovedFromRound(this);
		}
	}

	public void BeginTurn() {
		if (Finished) { return; }
        turnData = new TurnData(CurrentPlayer);
		MidTurn = true;
		CurrentPlayer.BeginTurn(this);
		NotifyTurnBegan?.Invoke(CurrentPlayer);
	}

	public Result<Unit, string> EndTurn() {
		string playerIndexString = $"[{Players.ToList().FindIndex(x => x == CurrentPlayer)}]";
		int nameWidth = 20 - playerIndexString.Length;
		string name = CurrentPlayer.Name.Length > nameWidth ? $"{CurrentPlayer.Name[..(nameWidth - 1)]}…" : CurrentPlayer.Name;
		GD.Print($"{Turn} {$"{name}{playerIndexString}".PadRight(nameWidth + playerIndexString.Length, '.')}{turnData}");

		var turnRecordResult = turnData.AsTurnRecord();
		NotifyTurnEnded?.Invoke(CurrentPlayer, turnRecordResult);
		if (turnRecordResult.IsErr) { return Err(turnRecordResult.Error); }

		// Turn action events (we're after the validity check so we can assume this is a valid turn)
		if (turnData.DrawnCardsDeck.Count > 0) { NotifyDrewFromDeck?.Invoke(CurrentPlayer); }
		else { NotifyDrewFromDiscardPile?.Invoke(CurrentPlayer, turnData.DrawnCardsDiscardPile.AsReadOnly()); }

		turnData.Melds.ForEach(meld => NotifyMelded?.Invoke(CurrentPlayer, meld.Cards));
		turnData.LaidOffCards.ForEach(card => NotifyLaidOff?.Invoke(CurrentPlayer, card));

		NotifyDiscarded?.Invoke(CurrentPlayer, turnData.Discards.Last());

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
			bool wasRummy = turnData.Melds.Count > 1;
			if (wasRummy) { roundScore *= 2; }
			Winner.Score += roundScore;
			NotifyGameEnded?.Invoke(Winner, roundScore, wasRummy);
		}
		Turn++; MidTurn = false;
		return Ok();
	}

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
			_meldOrder.Remove((CurrentPlayer, CurrentPlayer.Melds.FindIndex(x => x == meld)));
			int index = CurrentPlayer.Melds.FindIndex(x => x.Equals(meld));
			if (index == -1) { GD.PushWarning($"Tried to reset nonexistent meld {meld}."); }
			else { CurrentPlayer.Melds.RemoveAt(index); }
		});
		turnData.Melds.Clear();

		// Undo draws
		turnData.DrawnCardsDeck.Reverse();
		turnData.DrawnCardsDeck.ForEach(card => {
			Deck.InternalUndoDraw(card); (CurrentPlayer.Hand as IAccessibleCardPile).Cards.Remove(card);
		});
		turnData.DrawnCardsDeck.Clear();
		turnData.DrawnCardsDiscardPile.Reverse();
		turnData.DrawnCardsDiscardPile.ForEach(card => {
			DiscardPile.InternalUndoDraw(card); (CurrentPlayer.Hand as IAccessibleCardPile).Cards.Remove(card);
		});
		turnData.DrawnCardsDiscardPile.Clear();

		NotifyTurnReset?.Invoke();
	}
}