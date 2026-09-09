using System;

namespace PokerSocial.Data
{
    public enum Suit { Clubs, Diamonds, Hearts, Spades }
    public enum Rank { Two = 2, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack, Queen, King, Ace }

    [Serializable]
    public class Card
    {
        public Suit suit;
        public Rank rank;

        // Horizontal upgrade path: cards unlock SOCIAL abilities, never raw power.
        // This keeps upgrades out of gambling-regulation territory (no win% change).
        public CardAbility equippedAbility;

        public Card(Suit s, Rank r)
        {
            suit = s;
            rank = r;
            equippedAbility = CardAbility.None;
        }

        public override string ToString() => $"{rank} of {suit}";
    }

    public enum CardAbility
    {
        None,
        PeekOpponentCard,   // reveal one card of a chosen opponent, once per hand
        BroadcastFakeTell,  // send a fake tell to the table (costs a resource, logged to history)
        AllianceSlotExpand, // unlock an extra alliance-chip slot
        ReadAccuracyBoost   // your declared reads render with higher visual confidence (cosmetic/social only)
    }

    public class Deck
    {
        private System.Collections.Generic.List<Card> cards = new();
        private System.Random rng = new();

        public Deck()
        {
            foreach (Suit s in Enum.GetValues(typeof(Suit)))
                foreach (Rank r in Enum.GetValues(typeof(Rank)))
                    cards.Add(new Card(s, r));
            Shuffle();
        }

        public void Shuffle()
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
        }

        public Card Draw()
        {
            if (cards.Count == 0) throw new InvalidOperationException("Deck is empty");
            var c = cards[^1];
            cards.RemoveAt(cards.Count - 1);
            return c;
        }

        public int RemainingCount => cards.Count;
    }
}
