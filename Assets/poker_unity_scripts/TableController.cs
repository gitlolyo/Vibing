using UnityEngine;
using PokerSocial.Data;
using PokerSocial.Systems;

namespace PokerSocial.Core
{
    /// <summary>
    /// Scene-level entry point. This is intentionally networking-agnostic:
    /// swap the TODO section for Mirror/Netcode/Photon RPCs once you've
    /// picked a framework. Everything above (Data/, Systems/) is plain C#
    /// and has zero Unity or networking dependencies, so it's testable
    /// and portable regardless of that choice.
    /// </summary>
    public class TableController : MonoBehaviour
    {
        [SerializeField] private string tableId;
        [SerializeField] private string tableName;

        public TableState CurrentTable { get; private set; }

        private ReadSystem readSystem;
        private AllianceSystem allianceSystem;
        private TellSystem tellSystem;
        private ReputationManager reputationManager;

        private void Awake()
        {
            CurrentTable = new TableState { tableId = tableId, tableName = tableName };

            readSystem = new ReadSystem();
            allianceSystem = new AllianceSystem();
            tellSystem = new TellSystem();
            reputationManager = new ReputationManager();

            readSystem.OnReadDeclared += read =>
                Debug.Log($"[Read] {read.readerId} called {(read.claimedBluff ? "BLUFF" : "STRONG")} on {read.targetId}");

            allianceSystem.OnWhisperDetected += msg =>
                Debug.Log($"[Ambient] {msg}");

            tellSystem.OnTellBroadcast += (playerId, tell) =>
                Debug.Log($"[Tell] {playerId} broadcast {tell}");

            reputationManager.OnReputationChanged += (table, rep) =>
                Debug.Log($"[Reputation] {table.tableName} now at {rep:F2}, stakes cap {table.stakesCap}");
        }

        // --- Example call sites; wire these to your UI buttons / network RPCs ---

        public void PlayerDeclaresRead(PlayerData reader, PlayerData target, bool claimingBluff)
        {
            try
            {
                readSystem.DeclareRead(reader, target, claimingBluff);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(e.Message);
            }
        }

        public void PlayersFormAlliance(PlayerData a, PlayerData b, int sidePot)
        {
            try
            {
                var ev = allianceSystem.FormAlliance(a, b, sidePot, CurrentTable);
                // TODO: attach `ev` to the in-progress HandRecord once hand-flow is built
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(e.Message);
            }
        }

        public void PlayerReported(string reportedPlayerId)
        {
            reputationManager.ApplyPlayerReport(CurrentTable, reportedPlayerId);
        }

        // TODO (networking layer): replace direct method calls above with
        // RPCs/Commands from your chosen framework so these fire across clients.
        // TODO (persistence): serialize CurrentTable to your backend between
        // sessions so hand history/rivalries/reputation survive restarts.
    }
}
