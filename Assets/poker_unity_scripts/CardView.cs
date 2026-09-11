using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PokerSocial.Data;

namespace PokerSocial.Core
{
    public class CardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public Card Card { get; private set; }
        public bool FaceUp { get; private set; }
        public event Action<CardView> OnClicked;

        public RectTransform Rect { get; private set; }

        private Image bg;
        private Text label;
        private Vector3 originalScale = Vector3.one;

        private static readonly Color FaceColorRed = new Color(1f, 0.93f, 0.93f);
        private static readonly Color FaceColorBlack = new Color(0.93f, 0.93f, 1f);
        private static readonly Color BackColor = new Color(0.15f, 0.25f, 0.55f);

        public static CardView Create(Transform parent, Vector2 size)
        {
            var go = new GameObject("Card");
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.color = BackColor;

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            var textGO = new GameObject("Label");
            textGO.transform.SetParent(go.transform, false);
            var text = textGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = Mathf.RoundToInt(size.y * 0.28f);
            text.color = Color.black;
            var tRect = textGO.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            var cv = go.AddComponent<CardView>();
            cv.bg = img;
            cv.label = text;
            cv.Rect = rect;
            cv.SetFaceDown();
            return cv;
        }

        public void Setup(Card card) => Card = card;

        public void SetFaceDown()
        {
            FaceUp = false;
            bg.color = BackColor;
            label.text = "";
        }

        public void SetFaceUp()
        {
            FaceUp = true;
            bool red = Card.suit == Suit.Hearts || Card.suit == Suit.Diamonds;
            bg.color = red ? FaceColorRed : FaceColorBlack;
            label.color = red ? new Color(0.75f, 0f, 0f) : Color.black;
            label.text = $"{RankSymbol(Card.rank)}{SuitSymbol(Card.suit)}";
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            transform.SetAsLastSibling();
            transform.localScale = originalScale * 1.25f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.localScale = originalScale;
        }

        public IEnumerator FlipRoutine(float duration = 0.25f)
        {
            float half = duration / 2f;
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float x = Mathf.Lerp(1f, 0f, t / half);
                Rect.localScale = new Vector3(x, 1f, 1f);
                yield return null;
            }

            if (FaceUp) SetFaceDown(); else SetFaceUp();

            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float x = Mathf.Lerp(0f, 1f, t / half);
                Rect.localScale = new Vector3(x, 1f, 1f);
                yield return null;
            }
            Rect.localScale = Vector3.one;
            originalScale = Vector3.one;
        }

        public IEnumerator MoveRoutine(Vector2 targetAnchoredPos, float duration)
        {
            Vector2 start = Rect.anchoredPosition;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                Rect.anchoredPosition = Vector2.Lerp(start, targetAnchoredPos, p);
                yield return null;
            }
            Rect.anchoredPosition = targetAnchoredPos;
        }

        public void OnPointerClick(PointerEventData eventData) => OnClicked?.Invoke(this);

        private static string RankSymbol(Rank r) => r switch
        {
            Rank.Ten => "10",
            Rank.Jack => "J",
            Rank.Queen => "Q",
            Rank.King => "K",
            Rank.Ace => "A",
            _ => ((int)r).ToString()
        };

        private static string SuitSymbol(Suit s) => s switch
        {
            Suit.Clubs => "\u2663",
            Suit.Diamonds => "\u2666",
            Suit.Hearts => "\u2665",
            Suit.Spades => "\u2660",
            _ => "?"
        };
    }
}