
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Rummy.Util;
using static Rummy.Util.Option;
using static Rummy.Util.Result;
using Godot;
using System.Collections.Immutable;
using System;
using Rummy.Interface;
using System.Text;
using System.Threading.Tasks;

namespace Rummy.Game;

public class Round
{
	private class TurnData {
		public TurnData(Player player) { Player = player; PriorMelds = Player.Melds.Count; }

		public readonly Player Player;
		public readonly int PriorMelds;
		public List<Card> DrawnCardsDeck = [], DrawnCardsDiscardPile = [];
		public List<Card> Discards = [];
		public List<Meld> Melds = [];
		public Dictionary<Player, Dictionary<int, List<Card>>> LayOffs = [];

		public List<Card> LaidOffCards => [..LayOffs.SelectMany(kvp => kvp.Value.SelectMany(kvp => kvp.Value))];

        public override string ToString() {
			var info = new List<string>();
			var drawnCardsDeckString = string.Join(", ", DrawnCardsDeck);
			var drawnCardsDiscardPileString = string.Join(", ", DrawnCardsDiscardPile);
			if (DrawnCardsDeck.Count > 0 && DrawnCardsDiscardPile.Count > 0) info.Add($"Drawn: [Deck: ({drawnCardsDeckString}), Discard Pile: ({drawnCardsDiscardPileString})]");
			else if (DrawnCardsDeck.Count > 0) info.Add($"Drawn (Deck): {(DrawnCardsDeck.Count > 1 ? $"({drawnCardsDeckString})" : $"{drawnCardsDeckString}")}");
			else if (DrawnCardsDiscardPile.Count > 0) info.Add($"Drawn (Discard Pile): {(DrawnCardsDiscardPile.Count > 1 ? $"({drawnCardsDiscardPileString})" : $"{drawnCardsDiscardPileString}")}");

			var discardsString = string.Join(", ", Discards);
			info.Add($"Discarded: {(Discards.Count == 0 ? "None" : Discards.Count == 1 ? $"{discardsString}" : $"({discardsString})")}");

			if (Melds.Count > 0) info.Add($"Melded: {string.Join(", ", Melds)}");
			if (LaidOffCards.Count > 0) info.Add($"Laid Off: {string.Join(", ", LaidOffCards)}");
			if (Melds.Count > 1) info.Add($"Prior Melds: {PriorMelds}");

			if (Player.Hand.Count == 0) info.Add($"Went out{(PriorMelds == 0 ? " with rummy" : "")}");
			else info.Add($"{Player.Hand.Count} left in hand");

			IsValid().InspectErr(err => info.Add($"Invalid: {err}"));

			return $"{{{string.Join(", ", info)}}}";
		}

		// Is valid (completed) turn?
		public Result<Unit, string> IsValid() {
			var errs = new List<string>();
			if (DrawnCardsDeck.Count == 0 && DrawnCardsDiscardPile.Count == 0) errs.Add("Did not draw.");
			if (DrawnCardsDeck.Count > 0 && DrawnCardsDiscardPile.Count > 0) errs.Add($"Drew from both deck and discard pile.");
			if (DrawnCardsDeck.Count > 1) errs.Add($"Drew {DrawnCardsDeck.Count} cards from deck.");

			if (DrawnCardsDiscardPile.Count > 0 && Discards.Count > 0 && Discards.Last() == DrawnCardsDiscardPile.First() && !Player.Hand.Empty)
				errs.Add($"Discarded top card drawn from discard pile without going out.");
			
			if (DrawnCardsDiscardPile.Count > 1) {
				var meldedLast = Melds.Any(meld => meld.Cards.Contains(DrawnCardsDiscardPile.Last()));
				var laidOffLast = LaidOffCards.Contains(DrawnCardsDiscardPile.Last());
				if (!meldedLast && !laidOffLast) errs.Add($"Drew {DrawnCardsDiscardPile.Count} cards but did not use bottommost card ({DrawnCardsDiscardPile.Last()}).");
			}

			if (LayOffs.Count > 0 && Player.Melds.Count == 0) errs.Add($"Laid off before having melded.");
			
			if (Melds.Count > 1 && !Player.Hand.Empty) errs.Add($"Melded {Melds.Count} times in one turn without going out.");
			if (Melds.Count > 1 && PriorMelds != 0) errs.Add($"Attempted to rummy with a meld already down.");

			if (Discards.Count > 1) errs.Add($"Discarded more than once.");
			if (Discards.Count == 0 && !Player.Hand.Empty) errs.Add($"Ended turn without discarding or going out.");

			if (errs.Count > 0) return Err(string.Join(" ", errs));
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
	public class TurnRecord(Player player, Option<Card> discardedCard, ImmutableArray<Option<Card>> drawnCards, ImmutableArray<Meld> melds, ImmutableArray<Card> laidOffCards) {
        public readonly Player Player = player;
		public readonly Option<Card> DiscardedCard = discardedCard; // It is valid for the final turn to end without a discard
		public readonly ImmutableArray<Option<Card>> DrawnCards = drawnCards; // 'None' in this instance is an unknown card, ie: a card drawn from the deck
		public readonly ImmutableArray<Meld> Melds = melds;
		public readonly ImmutableArray<Card> LaidOffCards = laidOffCards;
		
        public override string ToString() {
			StringBuilder builder = new();
			builder.Append($"({Player.Name}) ");
			builder.Append($"Drew {DrawnCards.Length} card{(DrawnCards.Length > 1 ? "s" : "")}");
			if (DrawnCards.Any(x => x.IsSome)) {
				builder.Append(": [");
				builder.AppendJoin(", ", DrawnCards.Select(x => x.AndThen(x => Some(x.ToString())).Or("?")));
				builder.Append("]. ");
			}
			else { builder.Append(". "); }
			if (Melds.Any()) {
				builder.Append("Melded: [");
				builder.AppendJoin(", ", Melds);
				builder.Append("]. ");
			}
			if (LaidOffCards.Any()) {
				builder.Append("Laid off: [");
				builder.AppendJoin(", ", LaidOffCards);
				builder.Append("]. ");
			}
			builder.Append(DiscardedCard.IsSome ? "Discarded." : "Did not discard.");
			return builder.ToString();
		}
	}

	// These events are mainly for the frontend, and fire exactly when it happens
	public event Action<Player>								NotifyTurnBegan;
	public event Action<Player, Result<TurnRecord, string>> NotifyTurnEnded;
	public event Action 									NotifyTurnReset;
	public event Action<Player, int, bool> 					NotifyGameEnded;
	
	public event Action 									ImmediateDisplayNotifyDeckRanOut;
	public event Action										ImmediateDisplayNotifyDeckShuffled;
	public event Action<Player, int, int> 					ImmediateDisplayNotifyDrewDuringInitialisation;
	public event Action<Card> 								ImmediateDisplayNotifyInitialCardPlaceOnDiscard;
	
	public event Action<Player> 							ImmediateDisplayNotifyDrewFromDeck;
	public event Action<Player, Card> 						ImmediateDisplayNotifyDrewFromDiscardPile;
	public event Action<Player, Meld> 						ImmediateDisplayNotifyMelded;
	public event Action<Player, Meld, Card> 				ImmediateDisplayNotifyLaidOff;
	public event Action<Player, Card> 						ImmediateDisplayNotifyDiscarded;

	// These events are intended for computer players and fire on turn end rather than when the event happens
	// so as to avoid having to deal with actions which are undone by turn resetting
	public event Action<Player> 							NotifyDrewFromDeck;
	public event Action<Player, ReadOnlyCollection<Card>> 	NotifyDrewFromDiscardPile;
	public event Action<Player, ReadOnlyCollection<Card>> 	NotifyDrew;
	public event Action<Player, ReadOnlyCollection<Card>> 	NotifyMelded;
	public event Action<Player, Card> 						NotifyLaidOff;
	public event Action<Player, Card> 						NotifyDiscarded;

	private readonly List<Player> _players;
	public ReadOnlyCollection<Player> Players => _players.AsReadOnly();

	public int Turn { get; private set; } = -1;
	public bool MidTurn { get; private set; } = false;
	public Player CurrentPlayer => Turn >= 0 ? Players[Turn % Players.Count] : null;
	public Player NextPlayer => Players[(Turn + 1) % Players.Count];

	private Task _currentTurnTask = null;
	
	public bool Finished { get; private set; } = false;
	public Player Winner { get; private set; }

	public Deck Deck { get; init; } = new();
	public DiscardPile DiscardPile { get; init; } = new();

	private bool _currentlyFlippingOverDiscard = false;

	public bool HasDrawn { get => turnData.DrawnCardsDeck.Count > 0 || turnData.DrawnCardsDiscardPile.Count > 0; }

	private readonly Dictionary<(Player, int), int> _meldOrder = new();
	public ReadOnlyCollection<Meld> Melds => Players.ToList()
		.Aggregate(new List<(Player, Meld)>(), (melds, player) => melds.Concat(player.Melds.ConvertAll(melds => (player, melds))).ToList())
		.OrderBy(pair => _meldOrder[(pair.Item1, pair.Item1.Melds.FindIndex(x => x == pair.Item2))]).ToList()
		.ConvertAll(pair => pair.Item2).AsReadOnly();
	
	public Result<Unit, string> Meld(Meld meld) {
		if (meld.Valid) {
			CurrentPlayer.Melds.Add(meld);
			Player meldPlayer = CurrentPlayer;
			meld.OnCardAdded += (card) => {
				turnData.LayOffs
					.GetOrCreate(meldPlayer)
					.GetOrCreate(meldPlayer.Melds.FindIndex(x => x == meld))
					.Add(card);
				ImmediateDisplayNotifyLaidOff?.Invoke(CurrentPlayer, meld, card);
			};
			turnData.Melds.Add(meld.Clone());
			var orderKey = (meldPlayer, meldPlayer.Melds.FindIndex(x => x == meld));
			_meldOrder.GetOrAdd(orderKey, 0);
			_meldOrder[orderKey] = Melds.Count;
			ImmediateDisplayNotifyMelded?.Invoke(meldPlayer, meld);
			return Ok();
		}
		return Err($"{meld} is not a valid meld.");
	}

	public Round(List<Player> players) {
		_players = players;
		foreach (Player player in Players) { player.Hand.Reset(); player.Melds.Clear(); }
		turnData = new(players.First());
		// This is done as a second step because it triggers logic on the players, where they may inspect other players
		foreach (Player player in Players) player.Round = this;
		
		// Add required callbacks
		DiscardPile.OnCardAdded += (card) => {
			if (_currentlyFlippingOverDiscard || Turn < 0) { return; }
			turnData.Discards.Add(card); ImmediateDisplayNotifyDiscarded?.Invoke(CurrentPlayer, card);
		};
		Deck.OnCardDrawn += (card) => {
			if (_currentlyFlippingOverDiscard || Turn < 0) { return; }
			turnData.DrawnCardsDeck.Add(card); ImmediateDisplayNotifyDrewFromDeck?.Invoke(CurrentPlayer);
		};
		DiscardPile.OnCardDrawn += (card) => {
			if (_currentlyFlippingOverDiscard || Turn < 0) { return; }
			turnData.DrawnCardsDiscardPile.Add(card); ImmediateDisplayNotifyDrewFromDiscardPile?.Invoke(CurrentPlayer, card);
		};

		Deck.OnEmptied += () => {
			ImmediateDisplayNotifyDeckRanOut?.Invoke();
			_currentlyFlippingOverDiscard = true;
			Deck.Append(DiscardPile);
			Deck.Flip();
			DiscardPile.Clear();
			Deck.Draw().Inspect(card => { DiscardPile.Discard(card); ImmediateDisplayNotifyInitialCardPlaceOnDiscard?.Invoke(card); });
			_currentlyFlippingOverDiscard = false;
		};
	}

	~Round() { foreach (Player player in Players) { player.Round = null; } }

	// Run round start to finish in one go
	public Result<(List<TurnRecord> History, (Player Winner, int Score, bool WasRummy) Win), string> Simulate(Random random, int turnCutoff = 5000, int repeatInvalidTurnCutoff = 100) {
		if (Turn >= 0) return Err("Round has already begun.");
		if (Players.Any(player => player is UserPlayer)) return Err("Round contains UserPlayer.");

		List<TurnRecord> turnHistory = new();
		void onTurnEndedAction(Player player, Result<TurnRecord, string> result) => result.Inspect(x => turnHistory.Add(x));

		(Player Winner, int Score, bool WasRummy) winData = (null, -1, false);
        void onGameEndedAction(Player winner, int score, bool wasRummy) => winData = (winner, score, wasRummy);

		NotifyTurnEnded += onTurnEndedAction;
        NotifyGameEnded += onGameEndedAction;

		CreateAndShuffleDeck();
		DealCardsAndInitialiseRound();
        while (!Finished && Turn < turnCutoff) {
			Result<Unit, string> turnResult; int tryAtThisTurn = 0; string lastErr = null;
			do {
            	BeginTurn().Wait();
				turnResult = EndTurn();
				turnResult.InspectErr(err => { lastErr = err; ResetTurn(); });
				tryAtThisTurn++;
			} while (turnResult.IsErr && tryAtThisTurn < repeatInvalidTurnCutoff);
			if (tryAtThisTurn >= repeatInvalidTurnCutoff) {
				NotifyTurnEnded -= onTurnEndedAction;
				NotifyGameEnded -= onGameEndedAction;
				return Err($"Overran turn failure limit of {repeatInvalidTurnCutoff} ({CurrentPlayer.Name}, Turn {Turn}). Last err: {lastErr}");
			}
        }

		NotifyTurnEnded -= onTurnEndedAction;
		NotifyGameEnded -= onGameEndedAction;
		if (!Finished) { return Err($"Overran turn limit of {turnCutoff}."); }
		return Ok((turnHistory, winData));
	}
	public Result<(List<TurnRecord> History, (Player Winner, int Score, bool WasRummy) Win), string> Simulate(int turnCutoff = 5000, int repeatInvalidTurnCutoff = 100) => Simulate(Random.Shared, turnCutoff, repeatInvalidTurnCutoff);

	public void CreateAndShuffleDeck(Random random) {
		Deck.AddPack();
		Deck.Shuffle(random);
		ImmediateDisplayNotifyDeckShuffled?.Invoke();
	}
	public void CreateAndShuffleDeck() => CreateAndShuffleDeck(Random.Shared);

	public void DealCardsAndInitialiseRound() {
		if (Turn >= 0) return;
		
		int handSize = Players.Count switch {
			< 4 => 10,
			< 6 => 7,
			_ => 6
		};

		foreach (int i in Util.Range.To(handSize)) foreach (var player in Players) {
			Deck.Draw().Inspect(player.Hand.Add);
			ImmediateDisplayNotifyDrewDuringInitialisation?.Invoke(player, i, handSize);	
		}

		// Put one card into discard pile
		Deck.Draw().Inspect(card => { DiscardPile.Discard(card); ImmediateDisplayNotifyInitialCardPlaceOnDiscard?.Invoke(card); });

		Turn = 0;
	}

	public Task BeginTurn() {
		if (Finished || Turn == -1) throw new Exception("Attempted to begin turn when round either uninitialised or finished");
        turnData = new TurnData(CurrentPlayer);
		MidTurn = true;
		NotifyTurnBegan?.Invoke(CurrentPlayer);
		_currentTurnTask = CurrentPlayer.TakeTurn();
		return _currentTurnTask;
	}

	public Result<Unit, string> EndTurn() {
		string playerIndexString = $"[{Players.FindIndex(x => x == CurrentPlayer)}]";
		int nameWidth = 20 - playerIndexString.Length;
		string name = CurrentPlayer.Name.Length > nameWidth ? $"{CurrentPlayer.Name[..(nameWidth - 1)]}…" : CurrentPlayer.Name;
		GD.Print($"{Turn} {$"{name}{playerIndexString}".PadRight(nameWidth + playerIndexString.Length, '.')}{turnData}");

		var turnRecordResult = turnData.AsTurnRecord();
		NotifyTurnEnded?.Invoke(CurrentPlayer, turnRecordResult);
		if (turnRecordResult.IsErr) { return Err(turnRecordResult.Error); }

		// Turn action events (we're after the validity check so we can assume this is a valid turn)
		if (turnData.DrawnCardsDeck.Count > 0) {
			NotifyDrewFromDeck?.Invoke(CurrentPlayer);
			NotifyDrew?.Invoke(CurrentPlayer, new List<Card>().AsReadOnly());
		}
		else {
			NotifyDrewFromDiscardPile?.Invoke(CurrentPlayer, turnData.DrawnCardsDiscardPile.AsReadOnly());
			NotifyDrew?.Invoke(CurrentPlayer, turnData.DrawnCardsDiscardPile.AsReadOnly());
		}

		turnData.Melds.ForEach(meld => NotifyMelded?.Invoke(CurrentPlayer, meld.Cards));
		turnData.LaidOffCards.ForEach(card => NotifyLaidOff?.Invoke(CurrentPlayer, card));

		if (turnData.Discards.Count > 0) NotifyDiscarded?.Invoke(CurrentPlayer, turnData.Discards.Last());

		// Game End
		if (CurrentPlayer.Hand.Empty) {
			Finished = true;
			Winner = CurrentPlayer;
			int roundScore = 0;
			foreach (Player player in Players) {
				if (player == Winner) continue;
				roundScore += player.Hand.Score();
			}
			// Rummied, score doubled
			bool wasRummy = turnData.PriorMelds == 0;
			if (wasRummy) roundScore *= 2;
			Winner.Score += roundScore;
			NotifyGameEnded?.Invoke(Winner, roundScore, wasRummy);
		}
		Turn++; MidTurn = false;
		return Ok();
	}

	public void ResetTurn() {
		// Undo discards
		foreach (var card in turnData.Discards) { DiscardPile.InternalUndoDiscard(card); CurrentPlayer.Hand.Add(card); }
		turnData.Discards.Clear();

		// Undo layoffs
		foreach (var (player, layoffs) in turnData.LayOffs) foreach (var (index, cards) in layoffs) foreach (var card in cards) {
			player.Melds.ElementAt(index).InternalUndoLayOff(card); player.Hand.Add(card);
		}
		turnData.LayOffs.Clear();

		// Undo melds
		foreach (var meld in turnData.Melds) {
			foreach (var card in meld.Cards) CurrentPlayer.Hand.Add(card);
			_meldOrder.Remove((CurrentPlayer, CurrentPlayer.Melds.FindIndex(x => x == meld)));

			int index = CurrentPlayer.Melds.FindIndex(x => x.Equals(meld));
			if (index != -1) CurrentPlayer.Melds.RemoveAt(index); else GD.PushWarning($"Tried to reset nonexistent meld {meld}.");
		}
		turnData.Melds.Clear();

		// Undo draws
		foreach (var card in turnData.DrawnCardsDeck.Reversed()) { Deck.InternalUndoDraw(card); (CurrentPlayer.Hand as IAccessibleCardPile).Cards.Remove(card); }
		turnData.DrawnCardsDeck.Clear();

		foreach (var card in turnData.DrawnCardsDiscardPile.Reversed()) { DiscardPile.InternalUndoDraw(card); (CurrentPlayer.Hand as IAccessibleCardPile).Cards.Remove(card); }
		turnData.DrawnCardsDiscardPile.Clear();

		NotifyTurnReset?.Invoke();
	}
}