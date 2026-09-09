using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerSocial.Data
{
    /// <summary>
    /// The table is the persistent unit of your game, not the hand.
    /// This object is what should get serialized to your backend (or a
    /// networking framework's persistent object) between sessions.
    /// </summary>
    [Serializable]
    public class TableState
    {
        public string tableId;
        public string tableName;
        public List<PlayerData> seatedPlayers = new();
        public List<HandRecord> handHistory = new();

        public int stakesCap = 1000; // dynamically lowered if reputation decays
        public float tableReputation = 1.0f; // 0.0 (toxic/dying) to 1.0 (healthy)

        public void RecordHandResult(HandRecord record)
        {
            handHistory.Add(record);
            UpdateHeadToHead(record);
        }

        private void UpdateHeadToHead(HandRecord record)
        {
            foreach (var winnerId in record.winnerIds)
            {
                var winner = seatedPlayers.FirstOrDefault(p => p.playerId == winnerId);
                if (winner == null) continue;

                foreach (var loserId in record.participantIds.Except(record.winnerIds))
                {
                    if (!winner.headToHead.ContainsKey(loserId))
                        winner.headToHead[loserId] = new HeadToHeadRecord { opponentId = loserId };
                    winner.headToHead[loserId].winsAgainst++;

                    var loser = seatedPlayers.FirstOrDefault(p => p.playerId == loserId);
                    if (loser == null) continue;
                    if (!loser.headToHead.ContainsKey(winnerId))
                        loser.headToHead[winnerId] = new HeadToHeadRecord { opponentId = winnerId };
                    loser.headToHead[winnerId].lossesAgainst++;
                }
            }
        }

        /// <summary>
        /// Reputation is a mechanic, not a moderation bolt-on: toxic tables
        /// literally become worse tables to play at (lower stakes, fewer joiners).
        /// Call this from your report/moderation pipeline and from fake-tell spam detection.
        /// </summary>
        public void ApplyReputationDelta(float delta)
        {
            tableReputation = Math.Clamp(tableReputation + delta, 0f, 1f);
            stakesCap = (int)(1000 * tableReputation);
        }

        public List<(string playerA, string playerB, int intensity)> GetActiveRivalries()
        {
            var rivalries = new List<(string, string, int)>();
            foreach (var player in seatedPlayers)
            {
                foreach (var kvp in player.headToHead)
                {
                    if (kvp.Value.IsRivalry)
                    {
                        int intensity = kvp.Value.winsAgainst + kvp.Value.lossesAgainst;
                        rivalries.Add((player.playerId, kvp.Key, intensity));
                    }
                }
            }
            return rivalries;
        }
    }

    [Serializable]
    public class HandRecord
    {
        public string handId;
        public DateTime timestamp;
        public List<string> participantIds = new();
        public List<string> winnerIds = new();
        public List<DeclaredRead> declaredReads = new();
        public List<AllianceEvent> allianceEvents = new();
    }

    [Serializable]
    public class DeclaredRead
    {
        public string readerId;
        public string targetId;
        public bool claimedBluff; // "I think seat 3 is bluffing"
        public bool wasCorrect;
    }

    [Serializable]
    public class AllianceEvent
    {
        public string playerAId;
        public string playerBId;
        public int sidePotAmount;
        public bool wasVisibleAsWhispering; // other players see "seat 2 and 4 are whispering"
    }
}
