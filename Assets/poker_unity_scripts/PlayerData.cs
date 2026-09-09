using System;
using System.Collections.Generic;

namespace PokerSocial.Data
{
    [Serializable]
    public class PlayerData
    {
        public string playerId;
        public string displayName;
        public int chipStack;

        // Social stats — these ARE the progression system, not win rate.
        public int readsDeclared;
        public int readsCorrect;
        public float ReadAccuracy => readsDeclared == 0 ? 0f : (float)readsCorrect / readsDeclared;

        public int fakeTellsBroadcast;
        public List<string> unlockedAbilities = new(); // CardAbility names, cosmetic/social unlocks only

        // Per-opponent history feeds the rivalry graph
        public Dictionary<string, HeadToHeadRecord> headToHead = new();

        public PlayerData(string id, string name, int startingChips)
        {
            playerId = id;
            displayName = name;
            chipStack = startingChips;
        }
    }

    [Serializable]
    public class HeadToHeadRecord
    {
        public string opponentId;
        public int winsAgainst;
        public int lossesAgainst;
        public bool IsRivalry => winsAgainst + lossesAgainst >= 5; // tune threshold as needed
    }
}
