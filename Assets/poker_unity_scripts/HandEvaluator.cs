using System;
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
        public List<int> tiebreakers;
        public string description;
    }

    public static class HandEvaluator
    {
        public static HandEvaluation Evaluate(List<Card> cards)
        {
            if (cards == null || cards.Count == 0)
                return new HandEvaluation { rank = HandRank.HighCard, description = "No cards" };

            // If less than 5 cards total, evaluate based on what's available
            if (cards.Count < 5)
            {
                var sortedRanks = cards.Select(c => (int)c.rank).OrderByDescending(r => r).ToList();
                return new HandEvaluation
                {
                    rank = HandRank.HighCard,
                    tiebreakers = sortedRanks,
                    description = $"High Card, {(Rank)sortedRanks[0]}"
                };
            }

            // Generate all 5-card combinations from available cards
            var combos = GetCombinations(cards, 5);
            HandEvaluation bestEval = default;
            bool first = true;

            foreach (var combo in combos)
            {
                var eval = EvaluateFiveCards(combo);
                if (first || Compare(eval, bestEval) > 0)
                {
                    bestEval = eval;
                    first = false;
                }
            }

            return bestEval;
        }

        private static HandEvaluation EvaluateFiveCards(List<Card> hand)
        {
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

            if (distinct.SequenceEqual(new List<int> { 14, 5, 4, 3, 2 }))
            {
                high = 5;
                return true;
            }
            return false;
        }

        public static int Compare(HandEvaluation a, HandEvaluation b)
        {
            if (a.rank != b.rank) return a.rank > b.rank ? 1 : -1;
            for (int i = 0; i < Math.Min(a.tiebreakers.Count, b.tiebreakers.Count); i++)
            {
                if (a.tiebreakers[i] != b.tiebreakers[i])
                    return a.tiebreakers[i] > b.tiebreakers[i] ? 1 : -1;
            }
            return 0;
        }

        private static IEnumerable<List<T>> GetCombinations<T>(List<T> list, int length)
        {
            if (length == 1) return list.Select(t => new List<T> { t });
            return GetCombinations(list, length - 1)
                .SelectMany(t => list.Where(e => !t.Contains(e)), (t1, t2) => new List<T>(t1) { t2 })
                .Where(c => c.Count == length);
        }
    }
}