using System;
using PokerSocial.Data;

namespace PokerSocial.Systems
{
    /// <summary>
    /// Wire this to your chat/report pipeline. The point: bad behavior has an
    /// economic consequence baked into the game (lower stakes cap, fewer
    /// matchmaking joins) instead of relying purely on human moderators.
    /// This does NOT replace moderation/reporting tools - it supplements them.
    /// </summary>
    public class ReputationManager
    {
        public event Action<TableState, float> OnReputationChanged;

        private const float REPORT_PENALTY = 0.05f;
        private const float HEALTHY_HAND_BONUS = 0.002f; // slow recovery for clean play

        public void ApplyPlayerReport(TableState table, string reportedPlayerId)
        {
            table.ApplyReputationDelta(-REPORT_PENALTY);
            OnReputationChanged?.Invoke(table, table.tableReputation);

            // Real implementation: also track per-player report counts and
            // apply matchmaking friction to repeat offenders specifically,
            // not just the table as a whole.
        }

        public void ApplyCleanHandBonus(TableState table)
        {
            table.ApplyReputationDelta(HEALTHY_HAND_BONUS);
        }

        public int GetEffectiveStakesCap(TableState table) => table.stakesCap;

        public bool ShouldWarnJoiningPlayer(TableState table) => table.tableReputation < 0.4f;
    }
}
