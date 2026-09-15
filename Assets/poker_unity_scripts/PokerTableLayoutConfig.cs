using UnityEngine;

namespace PokerSocial.Core
{
    public static class PokerTableLayoutConfig
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1600, 900);

        public static readonly Vector2 TableFeltSize = new Vector2(880, 480);
        public static readonly Vector2 TableFeltPosition = Vector2.zero;

        public static readonly Vector2 StartMenuSize = new Vector2(480, 320);

        public static readonly float LogPanelWidth = 320f;
        public static readonly float ActionPanelWidth = 260f;

        public static readonly Vector2 ActionPanelPos = new Vector2(440f + (260f / 2f), 0);
        public static readonly Vector2 LogPanelPos = new Vector2(-440f - (320f / 2f), 0);

        public static readonly Vector2 PotTextPos = new Vector2(0, 220);
        public static readonly Vector2 PotTextSize = new Vector2(300, 32);

        public static readonly Vector2 InfoBarPos = new Vector2(0, 175);
        public static readonly Vector2 InfoBarSize = new Vector2(480, 24);

        public static readonly Vector2 DeckAnchorPos = new Vector2(-360, 0);
        public static readonly Vector2 CommunityAnchorPos = new Vector2(0, 0);

        public static readonly Vector2 SeatBottomPos = new Vector2(0, -155);
        public static readonly Vector2 SeatTopLeftPos = new Vector2(-190, 140);
        public static readonly Vector2 SeatTopRightPos = new Vector2(190, 140);
        public static readonly Vector2 SeatLeftPos = new Vector2(-310, 0);

        public static readonly Vector2 ButtonSize = new Vector2(220, 36);
        public static readonly float ButtonTopPadding = 20f;
        public static readonly float ButtonVerticalSpacing = 40f;

        public static readonly Vector2 CardSize = new Vector2(55, 78);
        public static readonly float HoleCardSpacing = 36f;
        public static readonly float CommunityCardGap = 10f;
        public static readonly int LogFontSize = 13;
    }
}