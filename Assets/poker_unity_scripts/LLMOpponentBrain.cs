using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace PokerSocial.Systems
{
    /// <summary>
    /// Calls an LLM to decide an opponent's fold/call and to generate a short
    /// in-character table-talk line. Drop this in alongside your existing
    /// Systems/ classes - nothing else needs to change to compile.
    ///
    /// SECURITY: this is fine for testing in the Editor with your own key in
    /// the Inspector or an environment variable. Do NOT ship a Steam build
    /// with a real API key embedded in the client - anyone can pull it out
    /// of the binary. Before release, swap the endpoint below for your own
    /// small backend that holds the key server-side and forwards requests.
    ///
    /// Uses JsonUtility for simplicity. If you need anything past this
    /// shape, pull in the com.unity.nuget.newtonsoft-json package instead -
    /// JsonUtility can't handle polymorphic/optional fields well.
    /// </summary>
    public class LLMOpponentBrain : MonoBehaviour
    {
        [SerializeField] private string apiKey = ""; // set via Inspector for testing only - never commit this
        [SerializeField] private string model = "claude-sonnet-5"; // check Anthropic's docs for current model ids

        private const string ENDPOINT = "https://api.anthropic.com/v1/messages";

        [Serializable]
        public class Decision
        {
            public string action;    // "fold" or "call"
            public string tableTalk; // optional in-character line, may be empty
        }

        public void RequestDecision(string opponentName, string handDescription, int potSize, int chipStack,
            Action<Decision> onComplete)
        {
            StartCoroutine(RequestDecisionRoutine(opponentName, handDescription, potSize, chipStack, onComplete));
        }

        private IEnumerator RequestDecisionRoutine(string opponentName, string handDescription, int potSize,
            int chipStack, Action<Decision> onComplete)
        {
            string prompt =
                $"You are {opponentName}, an opponent at a poker table with a distinct personality. " +
                $"Your hand: {handDescription}. Pot: {potSize}. Your chips: {chipStack}. " +
                "Decide whether to fold or call, and optionally say one short line of table talk in character. " +
                "Respond with ONLY compact JSON, no prose, no markdown fences: " +
                "{\"action\":\"fold\"|\"call\",\"tableTalk\":\"...\"}";

            string body = JsonUtility.ToJson(new AnthropicRequest
            {
                model = model,
                max_tokens = 200,
                messages = new[] { new AnthropicMessage { role = "user", content = prompt } }
            });

            using var req = new UnityWebRequest(ENDPOINT, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("x-api-key", apiKey);
            req.SetRequestHeader("anthropic-version", "2023-06-01");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"LLM request failed: {req.error}. Falling back to auto-call.");
                onComplete?.Invoke(new Decision { action = "call", tableTalk = "" });
                yield break;
            }

            Decision decision;
            try
            {
                string text = ExtractTextFromResponse(req.downloadHandler.text);
                decision = JsonUtility.FromJson<Decision>(text);
                if (decision == null || string.IsNullOrEmpty(decision.action))
                    decision = new Decision { action = "call", tableTalk = "" };
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Couldn't parse LLM response: {e.Message}. Falling back to auto-call.");
                decision = new Decision { action = "call", tableTalk = "" };
            }

            onComplete?.Invoke(decision);
        }

        // Anthropic's reply is wrapped in content[0].text - pull that
        // substring out before parsing it against our own tiny JSON schema.
        private string ExtractTextFromResponse(string raw)
        {
            var wrapper = JsonUtility.FromJson<AnthropicResponseWrapper>(raw);
            return wrapper?.content != null && wrapper.content.Length > 0 ? wrapper.content[0].text : "{}";
        }

        [Serializable] private class AnthropicRequest { public string model; public int max_tokens; public AnthropicMessage[] messages; }
        [Serializable] private class AnthropicMessage { public string role; public string content; }
        [Serializable] private class AnthropicResponseWrapper { public AnthropicContentBlock[] content; }
        [Serializable] private class AnthropicContentBlock { public string type; public string text; }
    }
}
