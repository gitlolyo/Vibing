using System;
using System.Collections.Generic;
using PokerSocial.Data;

namespace PokerSocial.Systems
{
    /// <summary>
    /// Two players can secretly pool a side bet against a third player.
    /// The CONTENT of the alliance is hidden, but its EXISTENCE is broadcast
    /// as ambient suspicion ("seat 2 and 4 are whispering") - this is the
    /// key design lever: information asymmetry as the actual game, not stat bonuses.
    /// </summary>
    public class AllianceSystem
    {
        public event Action<AllianceEvent> OnAllianceFormed;
        public event Action<string> OnWhisperDetected; // fires the ambient UI hint, not the content

        private Dictionary<string, int> allianceSlotsUsed = new();
        private const int DEFAULT_MAX_SLOTS = 1;

        public AllianceEvent FormAlliance(PlayerData playerA, PlayerData playerB, int sidePotAmount, TableState table)
        {
            int maxSlots = DEFAULT_MAX_SLOTS + CountUpgrade(playerA, CardAbility.AllianceSlotExpand);
            int usedA = allianceSlotsUsed.GetValueOrDefault(playerA.playerId, 0);

            if (usedA >= maxSlots)
                throw new InvalidOperationException($"{playerA.displayName} has no free alliance slots");

            if (playerA.chipStack < sidePotAmount || playerB.chipStack < sidePotAmount)
                throw new InvalidOperationException("Insufficient chips for side pot");

            playerA.chipStack -= sidePotAmount;
            playerB.chipStack -= sidePotAmount;
            allianceSlotsUsed[playerA.playerId] = usedA + 1;

            var ev = new AllianceEvent
            {
                playerAId = playerA.playerId,
                playerBId = playerB.playerId,
                sidePotAmount = sidePotAmount,
                wasVisibleAsWhispering = true
            };

            OnAllianceFormed?.Invoke(ev);
            // Broadcast ambient suspicion to everyone else at the table, not the deal terms.
            OnWhisperDetected?.Invoke($"Seats involving {playerA.displayName} and {playerB.displayName} are whispering...");

            return ev;
        }

        private int CountUpgrade(PlayerData player, CardAbility ability)
        {
            return player.unlockedAbilities.Contains(ability.ToString()) ? 1 : 0;
        }

        public void ResetSlotsForNewHand()
        {
            allianceSlotsUsed.Clear();
        }
    }
}
