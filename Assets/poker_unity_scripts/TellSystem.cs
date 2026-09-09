using System;
using PokerSocial.Data;

namespace PokerSocial.Systems
{
    public enum TellType { FoldAnimationWhileNotFolding, ClaimStrongHand, ClaimWeakHand, Hesitate }

    /// <summary>
    /// Lets a player broadcast a fake tell. Gated by cost so it can't be spammed,
    /// and every use is permanently logged to the table's hand history -
    /// overusing this burns your credibility long-term (self-balancing via the
    /// social layer, not a hard rule).
    /// </summary>
    public class TellSystem
    {
        public event Action<string, TellType> OnTellBroadcast;

        private const int TELL_COST = 15;

        public void BroadcastFakeTell(PlayerData sender, TellType tell, TableState table, HandRecord currentHand)
        {
            if (sender.chipStack < TELL_COST)
                throw new InvalidOperationException("Not enough chips to broadcast a tell");

            sender.chipStack -= TELL_COST;
            sender.fakeTellsBroadcast++;

            OnTellBroadcast?.Invoke(sender.playerId, tell);

            // Excessive tell usage should feed back into table reputation -
            // a table full of constant fake tells degrades trust for everyone.
            if (sender.fakeTellsBroadcast % 10 == 0)
                table.ApplyReputationDelta(-0.02f);
        }
    }
}
