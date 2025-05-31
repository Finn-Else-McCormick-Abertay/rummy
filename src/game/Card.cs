
using System;
using System.Collections;
using Rummy.Util;
using static Rummy.Util.Option;

namespace Rummy.Game;

public enum Rank {
	Ace = 1, Two = 2, Three = 3, Four = 4, Five = 5, Six = 6, Seven = 7, Eight = 8, Nine = 9, Ten = 10,
	Jack = 11, Queen = 12, King = 13
}
public enum Suit { Clubs, Hearts, Spades, Diamonds }

public readonly record struct Card(Rank Rank, Suit Suit) : IEquatable<Card>, IComparable<Card>
{
	public override string ToString() => $"{Enum.GetName(Rank)} of {Enum.GetName(Suit)}";

	public readonly bool Equals(Card other) => MatchesRank(other) && MatchesSuit(other);
	public override readonly int GetHashCode() => HashCode.Combine(Rank.GetHashCode(), Suit.GetHashCode());
    public int CompareTo(Card other) => Suit == other.Suit ? other.Rank - Rank : other.Suit - Suit;

	public readonly bool MatchesSuit(Suit otherSuit) => Suit == otherSuit;
	public readonly bool MatchesSuit(Card other) => MatchesSuit(other.Suit);
	
	public readonly bool MatchesRank(Rank otherRank) => Rank == otherRank;
	public readonly bool MatchesRank(Card other) => MatchesRank(other.Rank);

	public readonly int RankDistance(Rank otherRank) => (int)Rank - (int)otherRank;
	public readonly bool IsAdjacentRank(Rank otherRank) => IsAdjacentRankBelow(otherRank) || IsAdjacentRankAbove(otherRank);
	public readonly bool IsAdjacentRankBelow(Rank otherRank) => RankDistance(otherRank) == -1;
	public readonly bool IsAdjacentRankAbove(Rank otherRank) => RankDistance(otherRank) == 1;

	public readonly bool IsAdjacentRank(Card other) => IsAdjacentRank(other.Rank);
	public readonly bool IsAdjacentRankBelow(Card other) => IsAdjacentRankBelow(other.Rank);
	public readonly bool IsAdjacentRankAbove(Card other) => IsAdjacentRankAbove(other.Rank);
}