
using System;

namespace Rummy.Game;

public enum Rank {
	Ace = 1, Two = 2, Three = 3, Four = 4, Five = 5, Six = 6, Seven = 7, Eight = 8, Nine = 9, Ten = 10,
	Jack = 11, Queen = 12, King = 13
}
public enum Suit { Clubs, Hearts, Spades, Diamonds }

public readonly record struct Card(Rank Rank, Suit Suit) : IEquatable<Card> {
	public override string ToString() {
		return $"{Enum.GetName(Rank)} of {Enum.GetName(Suit)}";
	}

	public readonly bool Equals(Card other) => Rank == other.Rank && Suit == other.Suit;
	public override readonly int GetHashCode() => HashCode.Combine(Rank.GetHashCode(), Suit.GetHashCode());
}