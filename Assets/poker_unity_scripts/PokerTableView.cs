using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PokerSocial.Data;
using PokerSocial.Systems;

namespace PokerSocial.Core
{
    /// <summary>
    /// Playable, visual poker table. Builds its own UI at runtime (no prefabs
    /// needed): shuffles a real deck, deals animated cards to seats around a
    /// table, lets the human player (seat 0) fold or check, use the social
    /// systems (reads/alliances/tells), then runs a showdown with the real
    /// HandEvaluator and pays out the pot.
    ///
    /// AI: if an LLMOpponentBrain component is attached to this same
    /// GameObject, opponents route their fold/call decision through it
    /// (async, one request per still-in opponent) after your decision and
    /// before showdown. If none is attached, opponents just auto-call, same
    /// as before - nothing breaks either way.
    ///
    /// Drop this on an empty GameObject and press Play.
    /// </summary>
    public class PokerTableView : MonoBehaviour
    {
        [SerializeField] private int playerCount = 4;
        [SerializeField] private int startingChips = 1000;
        [SerializeField] private int ante = 10;

        private RectTransform tableFelt;
        private RectTransform deckAnchor;
        private Text potText;
        private Text repText;
        private Text logText;
        private Transform actionPanel;
        private int buttonPlacementIndex = 0;

        private readonly List<PlayerData> players = new();
        private readonly Dictionary<string, List<CardView>> seatCards = new();
        private readonly Dictionary<string, RectTransform> seatAnchors = new();
        private readonly Dictionary<string, bool> stillIn = new();

        private TableState table;
        private readonly ReadSystem readSystem = new();
        private readonly AllianceSystem allianceSystem = new();
        private readonly TellSystem tellSystem = new();
        private readonly ReputationManager reputationManager = new();
        private LLMOpponentBrain llmBrain; // optional - auto-found on this GameObject, null is fine

        private int pot;
        private bool waitingForHumanDecision;
        private bool humanFolded;

        private Button foldBtn, checkBtn, bluffBtn, strongBtn, allianceBtn, tellBtn, newHandBtn;

        private void Start()
        {
            llmBrain = GetComponent<LLMOpponentBrain>(); // add this component to enable LLM opponents (inactive)

            BuildUI();
            SetupPlayers();
            table = new TableState { tableId = "table-1", tableName = "Table 1" };
            table.seatedPlayers = players;

            WireLogging();
            StartCoroutine(RunHand());
        }

        private void WireLogging()
        {
            readSystem.OnReadDeclared += r =>
            {
                var reader = players.First(p => p.playerId == r.readerId);
                var target = players.First(p => p.playerId == r.targetId);
                Log($"{reader.displayName} calls {(r.claimedBluff ? "BLUFF" : "STRONG")} on {target.displayName}");
            };
            allianceSystem.OnWhisperDetected += Log;
            tellSystem.OnTellBroadcast += (id, tell) =>
                Log($"{players.First(p => p.playerId == id).displayName} broadcasts a fake tell: {tell}");
            reputationManager.OnReputationChanged += (t, rep) =>
                repText.text = $"Table reputation: {rep:F2}  |  Stakes cap: {t.stakesCap}";
        }

        // ---------------- UI ----------------

        private void BuildUI()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }

            var canvasGO = new GameObject("PokerCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            canvasGO.AddComponent<GraphicRaycaster>();

            // Table Felt & Deck Anchor
            tableFelt = CreatePanel(canvasGO.transform, Vector2.zero, new Vector2(1100, 550), new Color(0.1f, 0.4f, 0.2f, 1f));
            deckAnchor = CreateAnchor(tableFelt, new Vector2(0, 40));

            // Pot & Reputation Labels
            potText = CreateText(tableFelt, new Vector2(0, -20), new Vector2(300, 40), 28, "Pot: 0");
            repText = CreateText(canvasGO.transform, new Vector2(0, -420), new Vector2(800, 30), 18, "Table reputation: 1.00 | Stakes cap: 1000");

            // Action Panel & UI Buttons
            actionPanel = CreatePanel(canvasGO.transform, new Vector2(600, 0), new Vector2(300, 360), new Color(0.15f, 0.15f, 0.18f, 0.95f));

            buttonPlacementIndex = 0;
            foldBtn = CreateButton(actionPanel, "Fold", OnFoldClicked);
            checkBtn = CreateButton(actionPanel, "Check", OnCheckClicked);
            bluffBtn = CreateButton(actionPanel, "Call Bluff (Seat 2)", OnBluffClicked);
            strongBtn = CreateButton(actionPanel, "Call Strong (Seat 2)", OnStrongClicked);
            allianceBtn = CreateButton(actionPanel, "Alliance (Seat 3)", OnAllianceClicked);
            tellBtn = CreateButton(actionPanel, "Broadcast Fake Tell", OnTellClicked);
            newHandBtn = CreateButton(actionPanel, "Next Hand", OnNewHandClicked);

            // Activity Log Panel
            var logPanel = CreatePanel(canvasGO.transform, new Vector2(-580, 0), new Vector2(340, 500), new Color(0.05f, 0.05f, 0.05f, 0.85f));
            logText = CreateText(logPanel, Vector2.zero, new Vector2(320, 480), 14, "");
            logText.alignment = TextAnchor.UpperLeft;
        }

        private RectTransform CreatePanel(Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return rect;
        }

        private RectTransform CreateAnchor(Transform parent, Vector2 localPos)
        {
            var go = new GameObject("Anchor");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = localPos;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        private Text CreateText(Transform parent, Vector2 pos, Vector2 size, int fontSize, string content)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperCenter;
            text.text = content;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return text;
        }

        private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button_" + label);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.55f, 0.55f, 0.6f, 1f);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(260, 34);
            rect.anchoredPosition = new Vector2(0, -10 - buttonPlacementIndex * 40);
            buttonPlacementIndex++;

            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None; // stop Unity from auto-overriding our manual color
            btn.onClick.AddListener(onClick);

            var textGO = new GameObject("Label");
            textGO.transform.SetParent(go.transform, false);
            var text = textGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 15;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            var tRect = textGO.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            return btn;
        }

        private void SetupPlayers()
        {
            string[] names = { "Alice (You)", "Bob", "Carla", "Dev", "Eli", "Fay" };
            players.Clear();
            for (int i = 0; i < playerCount; i++)
                players.Add(new PlayerData($"p{i}", names[i % names.Length], startingChips));

            float radius = 380f;
            for (int i = 0; i < players.Count; i++)
            {
                float angle = Mathf.PI * (0.12f + 0.76f * i / Mathf.Max(1, players.Count - 1));
                Vector2 pos = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius - 60);
                var anchor = CreateAnchor(tableFelt, pos);
                seatAnchors[players[i].playerId] = anchor;

                var nameText = CreateText(anchor, new Vector2(0, 70), new Vector2(180, 24), 15,
                    $"{players[i].displayName} ({players[i].chipStack})");
                nameText.gameObject.name = "SeatLabel_" + players[i].playerId;
            }
        }

        // ---------------- Hand flow ----------------

        private IEnumerator RunHand()
        {
            Log("=== NEW HAND ===");
            newHandBtn.gameObject.SetActive(false);
            ClearSeats();

            pot = 0;
            foreach (var p in players)
            {
                stillIn[p.playerId] = true;
                int a = Mathf.Min(ante, p.chipStack);
                p.chipStack -= a;
                pot += a;
                UpdateSeatLabel(p);
            }
            allianceSystem.ResetSlotsForNewHand();
            UpdatePotText();

            var deck = new Deck();
            yield return StartCoroutine(ShuffleAnimation());
            yield return StartCoroutine(DealAnimation(deck));

            foreach (var cv in seatCards[players[0].playerId])
                yield return StartCoroutine(cv.FlipRoutine());

            Log($"{players[0].displayName}: fold, or check to use the social tools and go to showdown.");
            SetActionButtonsInteractable(true);
            humanFolded = false;
            waitingForHumanDecision = true;
            yield return new WaitUntil(() => !waitingForHumanDecision);
            SetActionButtonsInteractable(false);

            if (humanFolded)
            {
                stillIn[players[0].playerId] = false;
                Log($"{players[0].displayName} folds.");
            }

            yield return StartCoroutine(OpponentDecisions());
            yield return StartCoroutine(Showdown());
        }

        /// <summary>
        /// Each still-in opponent decides fold/call. If llmBrain is attached,
        /// this asks it (one async request per opponent, in turn) and honors
        /// the fold/tableTalk it returns. If not, everyone just calls -
        /// identical to the old always-call behavior.
        /// </summary>
        private IEnumerator OpponentDecisions()
        {
            if (llmBrain == null) yield break;

            foreach (var p in players)
            {
                if (p.playerId == players[0].playerId) continue; // human already decided
                if (!stillIn[p.playerId]) continue;

                string handDesc = HandEvaluator.Evaluate(seatCards[p.playerId].Select(cv => cv.Card).ToList()).description;

                bool done = false;
                LLMOpponentBrain.Decision decision = null;
                llmBrain.RequestDecision(p.displayName, handDesc, pot, p.chipStack, d => { decision = d; done = true; });
                yield return new WaitUntil(() => done);

                if (decision != null && decision.action == "fold")
                {
                    stillIn[p.playerId] = false;
                    Log($"{p.displayName} folds.");
                }
                else if (decision != null && !string.IsNullOrEmpty(decision.tableTalk))
                {
                    Log($"{p.displayName}: \"{decision.tableTalk}\"");
                }
            }
        }

        private IEnumerator ShuffleAnimation()
        {
            Log("Shuffling...");
            var backs = new List<CardView>();
            for (int i = 0; i < 6; i++)
            {
                var cv = CardView.Create(tableFelt, new Vector2(70, 100));
                cv.Rect.anchoredPosition = deckAnchor.anchoredPosition +
                    new Vector2(UnityEngine.Random.Range(-8, 8), UnityEngine.Random.Range(-4, 4));
                backs.Add(cv);
            }
            for (int r = 0; r < 8; r++)
            {
                foreach (var cv in backs)
                {
                    Vector2 jitter = new Vector2(UnityEngine.Random.Range(-14, 14), UnityEngine.Random.Range(-8, 8));
                    yield return StartCoroutine(cv.MoveRoutine(deckAnchor.anchoredPosition + jitter, 0.04f));
                }
            }
            foreach (var cv in backs)
            {
                yield return StartCoroutine(cv.MoveRoutine(deckAnchor.anchoredPosition, 0.06f));
                Destroy(cv.gameObject);
            }
        }

        private IEnumerator DealAnimation(Deck deck)
        {
            foreach (var p in players)
                seatCards[p.playerId] = new List<CardView>();

            for (int round = 0; round < 5; round++)
            {
                foreach (var p in players)
                {
                    var card = deck.Draw();
                    var cv = CardView.Create(tableFelt, new Vector2(70, 100));
                    cv.Setup(card);
                    cv.Rect.anchoredPosition = deckAnchor.anchoredPosition;

                    var seat = seatAnchors[p.playerId];
                    int idx = seatCards[p.playerId].Count;
                    Vector2 offset = new Vector2((idx - 2) * 40, -10);
                    Vector2 target = seat.anchoredPosition + offset;

                    seatCards[p.playerId].Add(cv);
                    yield return StartCoroutine(cv.MoveRoutine(target, 0.16f));
                }
            }
        }

        private IEnumerator Showdown()
        {
            Log("\n--- SHOWDOWN ---");
            foreach (var p in players)
            {
                if (!stillIn[p.playerId] || p.playerId == players[0].playerId) continue;
                foreach (var cv in seatCards[p.playerId])
                    yield return StartCoroutine(cv.FlipRoutine());
            }

            PlayerData winner = null;
            HandEvaluation bestEval = default;
            foreach (var p in players)
            {
                if (!stillIn[p.playerId]) continue;
                var cards = seatCards[p.playerId].Select(cv => cv.Card).ToList();
                var eval = HandEvaluator.Evaluate(cards);
                Log($"{p.displayName}: {eval.description}");
                if (winner == null || HandEvaluator.Compare(eval, bestEval) > 0)
                {
                    winner = p;
                    bestEval = eval;
                }
            }

            winner.chipStack += pot;
            Log($"\nWINNER: {winner.displayName} takes the pot of {pot} with {bestEval.description}");
            foreach (var p in players) UpdateSeatLabel(p);

            table.RecordHandResult(new HandRecord
            {
                handId = Guid.NewGuid().ToString(),
                timestamp = DateTime.UtcNow,
                participantIds = players.Where(p => stillIn[p.playerId]).Select(p => p.playerId).ToList(),
                winnerIds = new List<string> { winner.playerId }
            });
            reputationManager.ApplyCleanHandBonus(table);

            newHandBtn.gameObject.SetActive(true);
        }

        private void ClearSeats()
        {
            foreach (var kvp in seatCards)
                foreach (var cv in kvp.Value)
                    if (cv != null) Destroy(cv.gameObject);
            seatCards.Clear();
        }

        // ---------------- Button handlers ----------------

        private void OnFoldClicked() { humanFolded = true; waitingForHumanDecision = false; }
        private void OnCheckClicked() { waitingForHumanDecision = false; }

        private void OnBluffClicked() => DeclareReadOnSeatTwo(claimingBluff: true);
        private void OnStrongClicked() => DeclareReadOnSeatTwo(claimingBluff: false);

        private void DeclareReadOnSeatTwo(bool claimingBluff)
        {
            if (players.Count < 2) return;
            try
            {
                var reader = players[0];
                var target = players[1];
                var read = readSystem.DeclareRead(reader, target, claimingBluff);
                bool actuallyBluffing =
                    HandEvaluator.Evaluate(seatCards[target.playerId].Select(cv => cv.Card).ToList()).rank <= HandRank.Pair;
                readSystem.ResolveRead(read, reader, actuallyBluffing);
                Log($"Read was {(read.wasCorrect ? "CORRECT" : "WRONG")} — {reader.displayName}'s accuracy now {reader.ReadAccuracy:P0}");
                UpdateSeatLabel(reader);
            }
            catch (Exception e) { Log(e.Message); }
        }

        private void OnAllianceClicked()
        {
            if (players.Count < 3) return;
            try
            {
                allianceSystem.FormAlliance(players[0], players[2], 20, table);
                UpdateSeatLabel(players[0]);
                UpdateSeatLabel(players[2]);
            }
            catch (Exception e) { Log(e.Message); }
        }

        private void OnTellClicked()
        {
            try
            {
                var handRecord = new HandRecord { handId = "current" };
                tellSystem.BroadcastFakeTell(players[0], TellType.FoldAnimationWhileNotFolding, table, handRecord);
                UpdateSeatLabel(players[0]);
            }
            catch (Exception e) { Log(e.Message); }
        }

        private void OnNewHandClicked() => StartCoroutine(RunHand());

        // ---------------- helpers ----------------

        private void SetActionButtonsInteractable(bool value)
        {
            foldBtn.interactable = value;
            checkBtn.interactable = value;
            bluffBtn.interactable = value;
            strongBtn.interactable = value;
            allianceBtn.interactable = value;
            tellBtn.interactable = value;
        }

        private void UpdateSeatLabel(PlayerData p)
        {
            var labelTransform = seatAnchors[p.playerId].Find("SeatLabel_" + p.playerId);
            var label = labelTransform != null ? labelTransform.GetComponent<Text>() : null;
            if (label != null) label.text = $"{p.displayName} ({p.chipStack})";
        }

        private void UpdatePotText() => potText.text = $"Pot: {pot}";

        private void Log(string line)
        {
            logText.text += line + "\n";
            Debug.Log(line);
        }
    }
}
