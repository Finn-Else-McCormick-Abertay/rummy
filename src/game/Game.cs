
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;

namespace Rummy.Game;

public class Game
{
	public Game(List<Player> playersParam) {
		players = playersParam;

		deck = new List<Card>();
		discardPile = new List<Card>();

		// Create deck
		foreach (Suit suit in Enum.GetValues(typeof(Suit)).Cast<Suit>()) {
			foreach (Rank rank in Enum.GetValues(typeof(Rank)).Cast<Rank>()) {
				deck.Add(new Card(rank, suit));
			}
		}

		// Shuffle
		var random = new Random();
		deck = deck.OrderBy(x => random.Next()).ToList();

		// Deal to players
		int handSize = 10;
		if (players.Count >= 4) { handSize = 7; }
		else if (players.Count >= 6) { handSize = 6; }
		
		for (int i = 0; i < handSize; ++i) {
			foreach (Player player in players) {
				player.Hand.Add(DrawFromDeck());
			}
		}

		// Put one card into discard pile
		Discard(DrawFromDeck());
	}

	private readonly List<Player> players;
	public ReadOnlyCollection<Player> Players { get => players.AsReadOnly(); }

	private readonly List<Card> deck;
	private readonly List<Card> discardPile;

	public int DeckCount { get => deck.Count; }
	public ReadOnlyCollection<Card> DiscardPile { get => discardPile.AsReadOnly(); }

	public Card DrawFromDeck() {
		var card = deck[0];
		deck.RemoveAt(0);
		return card;
	}

	public Card DrawFromDiscardPile() {
		var card = discardPile[0];
		discardPile.RemoveAt(0);
		return card;
	}

	public List<Card> DrawFromDiscardPile(int count) {
		var cards = discardPile.GetRange(0, count);
		discardPile.RemoveRange(0, count);
		return cards;
	}

	public void Discard(Card card) {
		discardPile.Insert(0, card);
	}
}