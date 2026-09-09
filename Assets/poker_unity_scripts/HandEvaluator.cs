using System.Collections.Generic;
using System.Linq;

namespace PokerSocial.Data
{
    public enum HandRank
    {
        HighCard = 0, Pair, TwoPair, ThreeOfAKind, Straight,
        Flush, FullHouse, FourOfAKind, StraightFlush
    }

    public struct HandEvaluation
    {
        public HandRank rank;
        public List<int> tiebreakers; // descending, for comparing same-rank hands
        public string description;
    }

    public static class HandEvaluator
    {
        public static HandEvaluation Evaluate(List<Card> hand)
        {
            if (hand.Count != 5)
                throw new System.ArgumentException("Evaluator expects exactly 5 cards");

            var ranks = hand.Select(c => (int)c.rank).OrderByDescending(r => r).ToList();
            var suits = hand.Select(c => c.suit).ToList();
            bool isFlush = suits.Distinct().Count() == 1;

            var grouped = ranks.GroupBy(r => r)
                                .OrderByDescending(g => g.Count())
                                .ThenByDescending(g => g.Key)
                                .ToList();

            bool isStraight = IsStraight(ranks, out int straightHigh);

            if (isStraight && isFlush)
                return new HandEvaluation { rank = HandRank.StraightFlush, tiebreakers = new() { straightHigh }, description = $"Straight Flush, {(Rank)straightHigh} high" };

            if (grouped[0].Count() == 4)
                return new HandEvaluation { rank = HandRank.FourOfAKind, tiebreakers = grouped.Select(g => g.Key).ToList(), description = $"Four of a Kind, {(Rank)grouped[0].Key}s" };

            if (grouped[0].Count() == 3 && grouped[1].Count() == 2)
                return new HandEvaluation { rank = HandRank.FullHouse, tiebreakers = grouped.Select(g => g.Key).ToList(), description = $"Full House, {(Rank)grouped[0].Key}s over {(Rank)grouped[1].Key}s" };

            if (isFlush)
                return new HandEvaluation { rank = HandRank.Flush, tiebreakers = ranks, description = $"Flush, {(Rank)ranks[0]} high" };

            if (isStraight)
                return new HandEvaluation { rank = HandRank.Straight, tiebreakers = new() { straightHigh }, description = $"Straight, {(Rank)straightHigh} high" };

            if (grouped[0].Count() == 3)
                return new HandEvaluation { rank = HandRank.ThreeOfAKind, tiebreakers = grouped.Select(g => g.Key).ToList(), description = $"Three of a Kind, {(Rank)grouped[0].Key}s" };

            if (grouped[0].Count() == 2 && grouped[1].Count() == 2)
                return new HandEvaluation { rank = HandRank.TwoPair, tiebreakers = grouped.Select(g => g.Key).ToList(), description = $"Two Pair, {(Rank)grouped[0].Key}s and {(Rank)grouped[1].Key}s" };

            if (grouped[0].Count() == 2)
                return new HandEvaluation { rank = HandRank.Pair, tiebreakers = grouped.Select(g => g.Key).ToList(), description = $"Pair of {(Rank)grouped[0].Key}s" };

            return new HandEvaluation { rank = HandRank.HighCard, tiebreakers = ranks, description = $"High Card, {(Rank)ranks[0]}" };
        }

        private static bool IsStraight(List<int> descendingRanks, out int high)
        {
            var distinct = descendingRanks.Distinct().ToList();
            high = distinct[0];

            if (distinct.Count == 5 && distinct[0] - distinct[4] == 4)
                return true;

            // Ace-low straight: A,5,4,3,2
            if (distinct.SequenceEqual(new List<int> { 14, 5, 4, 3, 2 }))
            {
                high = 5;
                return true;
            }
            return false;
        }

        /// <summary>Returns 1 if a beats b, -1 if b beats a, 0 if tied.</summary>
        public static int Compare(HandEvaluation a, HandEvaluation b)
        {
            if (a.rank != b.rank) return a.rank > b.rank ? 1 : -1;
            for (int i = 0; i < System.Math.Min(a.tiebreakers.Count, b.tiebreakers.Count); i++)
            {
                if (a.tiebreakers[i] != b.tiebreakers[i])
                    return a.tiebreakers[i] > b.tiebreakers[i] ? 1 : -1;
            }
            return 0;
        }
    }
}
