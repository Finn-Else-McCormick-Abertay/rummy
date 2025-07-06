
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

public static class RankImpl
{
	public static int DistanceTo(this Rank self, Rank other) => (int)self - (int)other;

	public static bool IsAdjacentBelow(this Rank self, Rank other) => self.DistanceTo(other) == -1;
	public static bool IsAdjacentAbove(this Rank self, Rank other) => self.DistanceTo(other) == 1;
	public static bool IsAdjacent(this Rank self, Rank other) => self.IsAdjacentBelow(other) || self.IsAdjacentAbove(other);
}

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

	public readonly int RankDistance(Rank otherRank) => Rank.DistanceTo(otherRank);
	public readonly int RankDistance(Card other) => Rank.DistanceTo(other.Rank);

	public readonly bool IsAdjacentRank(Rank otherRank) => Rank.IsAdjacent(otherRank);
	public readonly bool IsAdjacentRankBelow(Rank otherRank) => Rank.IsAdjacentBelow(otherRank);
	public readonly bool IsAdjacentRankAbove(Rank otherRank) => Rank.IsAdjacentAbove(otherRank);

	public readonly bool IsAdjacentRank(Card other) => Rank.IsAdjacent(other.Rank);
	public readonly bool IsAdjacentRankBelow(Card other) => Rank.IsAdjacentBelow(other.Rank);
	public readonly bool IsAdjacentRankAbove(Card other) => Rank.IsAdjacentAbove(other.Rank);
}