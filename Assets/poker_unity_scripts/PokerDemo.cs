using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using PokerSocial.Data;
using PokerSocial.Systems;

namespace PokerSocial.Core
{
    /// <summary>
    /// Drop this on an empty GameObject and press Play. Builds a minimal
    /// on-screen UI at runtime (no manual Canvas setup needed), deals a
    /// 5-card hand to each demo player, shows them, evaluates a winner,
    /// and fires a declared read + an alliance + a fake tell so you can
    /// see the whole stack working together in one pass.
    /// </summary>
    public class PokerDemo : MonoBehaviour
    {
        [SerializeField] private int playerCount = 4;

        private Canvas canvas;
        private Text logText;
        private List<PlayerData> players = new();
        private TableState table;
        private ReadSystem readSystem = new();
        private AllianceSystem allianceSystem = new();
        private TellSystem tellSystem = new();

        private void Start()
        {
            BuildUI();
            SetupPlayers();
            table = new TableState { tableId = "demo-table", tableName = "Demo Table" };
            table.seatedPlayers = players;

            RunDemoHand();
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("DemoCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            var textGO = new GameObject("LogText");
            textGO.transform.SetParent(canvasGO.transform, false);
            logText = textGO.AddComponent<Text>();
            logText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            logText.fontSize = 22;
            logText.alignment = TextAnchor.UpperLeft;
            logText.color = Color.white;
            logText.horizontalOverflow = HorizontalWrapMode.Overflow;
            logText.verticalOverflow = VerticalWrapMode.Overflow;

            var rect = textGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = new Vector2(20, 20);
            rect.offsetMax = new Vector2(-20, -20);
        }

        private void SetupPlayers()
        {
            string[] names = { "Alice", "Bob", "Carla", "Dev", "Eli", "Fay" };
            for (int i = 0; i < playerCount; i++)
                players.Add(new PlayerData($"p{i}", names[i % names.Length], 1000));
        }

        private void RunDemoHand()
        {
            var deck = new Deck();
            var log = new System.Text.StringBuilder();
            log.AppendLine("=== DEMO HAND ===\n");

            var hands = new Dictionary<PlayerData, List<Card>>();
            foreach (var p in players)
            {
                var hand = new List<Card> { deck.Draw(), deck.Draw(), deck.Draw(), deck.Draw(), deck.Draw() };
                hands[p] = hand;
                log.AppendLine($"{p.displayName}: {string.Join(", ", hand)}");
            }

            log.AppendLine("\n--- Evaluating ---");
            PlayerData winner = null;
            HandEvaluation bestEval = default;
            foreach (var kvp in hands)
            {
                var eval = HandEvaluator.Evaluate(kvp.Value);
                log.AppendLine($"{kvp.Key.displayName}: {eval.description}");
                if (winner == null || HandEvaluator.Compare(eval, bestEval) > 0)
                {
                    winner = kvp.Key;
                    bestEval = eval;
                }
            }
            log.AppendLine($"\nWINNER: {winner.displayName} with {bestEval.description}");

            // --- exercise the social systems so you see them fire ---
            log.AppendLine("\n--- Social layer ---");

            var reader = players[0];
            var target = players[1];
            readSystem.OnReadDeclared += r => log.AppendLine($"[Read] {reader.displayName} calls {(r.claimedBluff ? "BLUFF" : "STRONG")} on {target.displayName}");
            var read = readSystem.DeclareRead(reader, target, claimingBluff: true);
            bool targetWasBluffing = HandEvaluator.Evaluate(hands[target]).rank <= HandRank.Pair;
            readSystem.ResolveRead(read, reader, targetWasBluffing);
            log.AppendLine($"[Read Result] {(read.wasCorrect ? "CORRECT" : "WRONG")} — {reader.displayName}'s accuracy now {reader.ReadAccuracy:P0}");

            if (playerCount >= 3)
            {
                allianceSystem.OnWhisperDetected += msg => log.AppendLine($"[Ambient] {msg}");
                allianceSystem.FormAlliance(players[0], players[2], 20, table);
            }

            tellSystem.OnTellBroadcast += (id, tell) => log.AppendLine($"[Tell] {players.First(p => p.playerId == id).displayName} broadcasts fake tell: {tell}");
            var handRecord = new HandRecord { handId = "demo-1" };
            tellSystem.BroadcastFakeTell(players[^1], TellType.FoldAnimationWhileNotFolding, table, handRecord);

            table.RecordHandResult(new HandRecord
            {
                handId = "demo-1",
                participantIds = players.Select(p => p.playerId).ToList(),
                winnerIds = new List<string> { winner.playerId }
            });
            log.AppendLine($"\nTable reputation: {table.tableReputation:F2} | Stakes cap: {table.stakesCap}");

            logText.text = log.ToString();
            Debug.Log(log.ToString());
        }
    }
}
