using RopeEscape.Config;
using RopeEscape.Telemetry;
using RopeEscape.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RopeEscape.Gameplay
{
    /// <summary>
    /// Authentic uGUI (Canvas) controller built with the original sprites extracted from the APK
    /// (GameAtlas, HomeMainAtlas, DialogBase, DialogAtlas, ComAtlas).
    /// Replaces legacy OnGUI debug boxes with a responsive 1080x1920 mobile portrait/landscape HUD.
    /// </summary>
    public class GameUIController : MonoBehaviour
    {
        private SparkyGameNode _board;
        private Font _uiFont;

        // Top HUD elements
        private Text _levelTitleText;
        private GameObject _hardBadgeGo;
        private Text _timerText;
        private Image _timerBgImage;
        private GameObject _iceOverlayGo;
        private Text _coinText;
        private Text _ropesMovesText;
        private Image _comboFillImage;
        private Text _comboText;

        // Modals
        private GameObject _winModalGo;
        private Text _winTitleText;
        private Text _winStatsText;

        private GameObject _failModalGo;
        private Text _failTitleText;

        private GameObject _telemetryModalGo;
        private Text _telemetryStatsText;
        private InputField _endpointInput;
        private InputField _apiKeyInput;
        private Text _netToggleBtnText;
        private Image _netToggleBtnImg;

        public void Initialize(SparkyGameNode board)
        {
            _board = board;
            _uiFont = LoadDefaultFont();

            EnsureEventSystem();
            BuildCanvasUI();

            _board.OnLevelLoaded += HandleLevelLoaded;
            _board.OnLevelWon += HandleLevelWon;
            _board.OnLevelFailed += HandleLevelFailed;

            HandleLevelLoaded(_board.CurrentLevelId);
        }

        private void OnDestroy()
        {
            if (_board != null)
            {
                _board.OnLevelLoaded -= HandleLevelLoaded;
                _board.OnLevelWon -= HandleLevelWon;
                _board.OnLevelFailed -= HandleLevelFailed;
            }
        }

        private static Font LoadDefaultFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null)
                f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f == null)
                f = Font.CreateDynamicFontFromOSFont("Sans", 28);
            return f;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
                return;

            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
        }

        private void BuildCanvasUI()
        {
            GameObject canvasGo = new GameObject("GameUICanvas");
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            RectTransform rootRect = canvasGo.GetComponent<RectTransform>();

            // Icy screen border overlay when Freeze Time skill is active
            _iceOverlayGo = CreateImageObj("IceScreenOverlay", rootRect, "IceTimePanel", Vector2.zero, Vector2.zero);
            RectTransform iceRect = _iceOverlayGo.GetComponent<RectTransform>();
            iceRect.anchorMin = Vector2.zero;
            iceRect.anchorMax = Vector2.one;
            iceRect.offsetMin = Vector2.zero;
            iceRect.offsetMax = Vector2.zero;
            Image iceImg = _iceOverlayGo.GetComponent<Image>();
            iceImg.color = new Color(0.65f, 0.92f, 1.0f, 0.42f);
            iceImg.raycastTarget = false;
            _iceOverlayGo.SetActive(false);

            BuildTopBar(rootRect);
            BuildBottomSkillBar(rootRect);
            BuildWinModal(rootRect);
            BuildFailModal(rootRect);
            BuildTelemetryModal(rootRect);
        }

        private void BuildTopBar(RectTransform root)
        {
            // Top bar container anchored to top edge
            GameObject topBar = new GameObject("Trans_top", typeof(RectTransform));
            topBar.transform.SetParent(root, false);
            RectTransform topRect = topBar.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.anchoredPosition = Vector2.zero;
            topRect.sizeDelta = new Vector2(0f, 230f);

            // Top decorative header backdrop (top_bg_1)
            GameObject topBg = CreateImageObj("TopBg", topRect, "top_bg_1", new Vector2(0f, 0f), new Vector2(1080f, 210f));
            RectTransform topBgRect = topBg.GetComponent<RectTransform>();
            topBgRect.anchorMin = new Vector2(0f, 1f);
            topBgRect.anchorMax = new Vector2(1f, 1f);
            topBgRect.pivot = new Vector2(0.5f, 1f);
            topBgRect.offsetMin = new Vector2(0f, -210f);
            topBgRect.offsetMax = Vector2.zero;
            Image topBgImg = topBg.GetComponent<Image>();
            topBgImg.color = new Color(0.16f, 0.21f, 0.34f, 0.92f);
            topBgImg.raycastTarget = false;

            // Left: Settings / 1P Telemetry button (btn_setting)
            GameObject settingsBtn = CreateSpriteButton(
                "Btn_Setting",
                topRect,
                "btn_setting",
                new Vector2(-445f, -68f),
                new Vector2(88f, 90f),
                () => ToggleTelemetryModal());

            // Left-2: Reset / Retry Level button (btn_reset)
            CreateSpriteButton(
                "Btn_Reset",
                topRect,
                "btn_reset",
                new Vector2(-340f, -68f),
                new Vector2(88f, 90f),
                () => _board.LoadLevel(_board.CurrentLevelId));

            // Center: Level Navigation & Level Badge (levelNumBg)
            CreateSpriteButton(
                "Btn_PrevLevel",
                topRect,
                "btn_back",
                new Vector2(-190f, -68f),
                new Vector2(66f, 68f),
                () =>
                {
                    int prev = _board.CurrentLevelId > 1 ? _board.CurrentLevelId - 1 : LevelDataManager.MaxEmbeddedLevels;
                    _board.LoadLevel(prev);
                });

            GameObject levelPill = CreateImageObj("Img_levelNum", topRect, "levelNumBg", new Vector2(0f, -68f), new Vector2(286f, 76f));
            _levelTitleText = CreateTextObj(
                "Txt_Level",
                levelPill.transform as RectTransform,
                "LEVEL 1",
                36,
                Color.white,
                TextAnchor.MiddleCenter);

            GameObject nextBtn = CreateSpriteButton(
                "Btn_NextLevel",
                topRect,
                "btn_back",
                new Vector2(190f, -68f),
                new Vector2(66f, 68f),
                () =>
                {
                    int next = (_board.CurrentLevelId % LevelDataManager.MaxEmbeddedLevels) + 1;
                    _board.LoadLevel(next);
                });
            nextBtn.transform.localScale = new Vector3(-1f, 1f, 1f);

            // Hard Level indicator badge (hard_title_0 + gt)
            _hardBadgeGo = CreateImageObj("HardBadge", topRect, "hard_flag_1", new Vector2(0f, -122f), new Vector2(160f, 38f));
            CreateTextObj("Txt_Hard", _hardBadgeGo.GetComponent<RectTransform>(), "HARD LEVEL", 20, new Color(1f, 0.92f, 0.35f), TextAnchor.MiddleCenter);
            _hardBadgeGo.SetActive(false);

            // Right: Coin Pill (Main_28 + Main_27 + jia)
            GameObject coinPill = CreateImageObj("ImgCoin", topRect, "Main_28", new Vector2(390f, -68f), new Vector2(226f, 72f));
            RectTransform coinRect = coinPill.GetComponent<RectTransform>();
            CreateImageObj("CoinIcon", coinRect, "Main_27", new Vector2(-82f, 2f), new Vector2(68f, 68f));
            _coinText = CreateTextObj("Txt_Coins", coinRect, "2500", 30, Color.white, TextAnchor.MiddleCenter);
            _coinText.rectTransform.anchoredPosition = new Vector2(14f, 0f);

            // Second Row Left: Countdown Timer Pill (countDown_bg + countDown_icon)
            GameObject timerPill = CreateImageObj("LeftTime", topRect, "countDown_bg", new Vector2(-335f, -158f), new Vector2(246f, 72f));
            _timerBgImage = timerPill.GetComponent<Image>();
            RectTransform timerRect = timerPill.GetComponent<RectTransform>();
            CreateImageObj("ClockIcon", timerRect, "countDown_icon", new Vector2(-82f, 2f), new Vector2(48f, 54f));
            _timerText = CreateTextObj("Txt_Timer", timerRect, "02:00", 32, Color.white, TextAnchor.MiddleCenter);
            _timerText.rectTransform.anchoredPosition = new Vector2(18f, 0f);

            // Second Row Center: Ropes & Moves counter pill (di)
            GameObject statsPill = CreateImageObj("StatsPill", topRect, "di", new Vector2(15f, -158f), new Vector2(340f, 64f));
            statsPill.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.28f, 0.88f);
            _ropesMovesText = CreateTextObj(
                "Txt_RopesMoves",
                statsPill.GetComponent<RectTransform>(),
                "Ropes: 3  |  Moves: 0",
                25,
                new Color(0.92f, 0.96f, 1.0f),
                TextAnchor.MiddleCenter);

            // Second Row Right: Combo Bar (jdtd + jdt + djs)
            GameObject comboTrack = CreateImageObj("ComboTrack", topRect, "jdtd", new Vector2(365f, -158f), new Vector2(260f, 44f));
            RectTransform comboRect = comboTrack.GetComponent<RectTransform>();
            GameObject comboFill = CreateImageObj("ComboFill", comboRect, "jdt", Vector2.zero, new Vector2(248f, 34f));
            _comboFillImage = comboFill.GetComponent<Image>();
            _comboFillImage.type = Image.Type.Filled;
            _comboFillImage.fillMethod = Image.FillMethod.Horizontal;
            _comboFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            _comboFillImage.fillAmount = 0f;

            CreateImageObj("ComboIcon", comboRect, "djs", new Vector2(-115f, 2f), new Vector2(54f, 54f));
            _comboText = CreateTextObj("Txt_Combo", comboRect, "COMBO x0", 23, new Color(1f, 0.95f, 0.4f), TextAnchor.MiddleCenter);
        }

        private void BuildBottomSkillBar(RectTransform root)
        {
            GameObject bottomBar = new GameObject("Trans_Skill", typeof(RectTransform));
            bottomBar.transform.SetParent(root, false);
            RectTransform bottomRect = bottomBar.GetComponent<RectTransform>();
            bottomRect.anchorMin = new Vector2(0f, 0f);
            bottomRect.anchorMax = new Vector2(1f, 0f);
            bottomRect.pivot = new Vector2(0.5f, 0f);
            bottomRect.anchoredPosition = Vector2.zero;
            bottomRect.sizeDelta = new Vector2(0f, 240f);

            // Skill 1: Freeze Time (btn_skill1)
            BuildSkillSlot(
                bottomRect,
                "Skill_Freeze",
                "btn_skill1",
                "FREEZE",
                "15s",
                new Vector2(-280f, 125f),
                () => _board.SkillFreezingTime());

            // Skill 2: Cut Rope / Axe (btn_skill2)
            BuildSkillSlot(
                bottomRect,
                "Skill_CutRope",
                "btn_skill2",
                "CUT ROPE",
                "AXE",
                new Vector2(0f, 125f),
                () => _board.SkillCutRope());

            // Skill 3: Electric Fan / Shuffle (Items_99993)
            BuildSkillSlot(
                bottomRect,
                "Skill_ElectricFan",
                "Items_99993",
                "FAN",
                "MIX",
                new Vector2(280f, 125f),
                () => _board.SkillElectricFan());
        }

        private void BuildSkillSlot(
            RectTransform parent,
            string objName,
            string iconSpriteName,
            string title,
            string badgeLabel,
            Vector2 anchoredPos,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject slotGo = CreateSpriteButton(objName, parent, "skill_bg", anchoredPos, new Vector2(168f, 170f), onClick);
            RectTransform slotRect = slotGo.GetComponent<RectTransform>();

            GameObject iconGo = CreateImageObj("Icon", slotRect, iconSpriteName, new Vector2(0f, 10f), new Vector2(110f, 104f));
            iconGo.GetComponent<Image>().raycastTarget = false;

            // Count / Prop badge (img_propBg)
            GameObject badgeGo = CreateImageObj("PropBadge", slotRect, "img_propBg", new Vector2(58f, -48f), new Vector2(64f, 68f));
            badgeGo.GetComponent<Image>().raycastTarget = false;
            CreateTextObj("Txt_Badge", badgeGo.GetComponent<RectTransform>(), badgeLabel, 21, Color.white, TextAnchor.MiddleCenter);

            // Skill Title Pill below slot
            GameObject labelPill = CreateImageObj("LabelPill", slotRect, "di", new Vector2(0f, -86f), new Vector2(150f, 40f));
            labelPill.GetComponent<Image>().color = new Color(0.14f, 0.19f, 0.32f, 0.92f);
            labelPill.GetComponent<Image>().raycastTarget = false;
            CreateTextObj("Txt_SkillName", labelPill.GetComponent<RectTransform>(), title, 21, new Color(0.95f, 0.98f, 1f), TextAnchor.MiddleCenter);
        }

        private void BuildWinModal(RectTransform root)
        {
            _winModalGo = CreateModalBackdrop("WinModal", root);
            RectTransform backdropRect = _winModalGo.GetComponent<RectTransform>();

            GameObject card = CreateImageObj("DialogCard", backdropRect, "passDialogBg", Vector2.zero, new Vector2(780f, 840f));
            RectTransform cardRect = card.GetComponent<RectTransform>();

            // Victory Banner
            GameObject winBanner = CreateImageObj("WinBanner", cardRect, "1v1_Win", new Vector2(0f, 340f), new Vector2(460f, 140f));
            _winTitleText = CreateTextObj("Txt_WinTitle", cardRect, "LEVEL CLEARED!", 46, new Color(1f, 0.96f, 0.45f), TextAnchor.MiddleCenter);
            _winTitleText.rectTransform.anchoredPosition = new Vector2(0f, 225f);
            _winTitleText.rectTransform.sizeDelta = new Vector2(680f, 70f);

            // 3 Stars Row (icon_star_bg + icon_star)
            Vector2[] starPositions =
            {
                new Vector2(-175f, 105f),
                new Vector2(0f, 130f),
                new Vector2(175f, 105f)
            };
            for (int i = 0; i < 3; i++)
            {
                GameObject sBg = CreateImageObj($"StarBg_{i}", cardRect, "icon_star_bg", starPositions[i], new Vector2(142f, 136f));
                CreateImageObj($"Star_{i}", sBg.GetComponent<RectTransform>(), "icon_star", Vector2.zero, new Vector2(138f, 132f));
            }

            // Stats Content Box (bg_content)
            GameObject statsBox = CreateImageObj("StatsContent", cardRect, "bg_content", new Vector2(0f, -55f), new Vector2(620f, 170f));
            statsBox.GetComponent<Image>().color = new Color(0.15f, 0.20f, 0.34f, 0.92f);
            _winStatsText = CreateTextObj(
                "Txt_WinStats",
                statsBox.GetComponent<RectTransform>(),
                "Time: 12.4s\nMoves: 4   |   Max Combo: x3",
                30,
                Color.white,
                TextAnchor.MiddleCenter);

            // Coin Reward Pill
            GameObject rewardPill = CreateImageObj("RewardPill", cardRect, "Main_28", new Vector2(0f, -185f), new Vector2(260f, 76f));
            CreateImageObj("GoldIcon", rewardPill.GetComponent<RectTransform>(), "icon_gold", new Vector2(-78f, 0f), new Vector2(68f, 68f));
            Text rewardTxt = CreateTextObj("Txt_Reward", rewardPill.GetComponent<RectTransform>(), "+50", 36, new Color(1f, 0.94f, 0.35f), TextAnchor.MiddleCenter);
            rewardTxt.rectTransform.anchoredPosition = new Vector2(22f, 0f);

            // Next Level Button (btn_green)
            GameObject nextBtn = CreateSpriteButton(
                "Btn_NextLevelWin",
                cardRect,
                "btn_green",
                new Vector2(0f, -310f),
                new Vector2(420f, 118f),
                () =>
                {
                    int nextLevel = (_board.CurrentLevelId % LevelDataManager.MaxEmbeddedLevels) + 1;
                    _board.LoadLevel(nextLevel);
                });
            CreateTextObj("Txt_NextBtn", nextBtn.GetComponent<RectTransform>(), "NEXT LEVEL", 38, Color.white, TextAnchor.MiddleCenter);

            _winModalGo.SetActive(false);
        }

        private void BuildFailModal(RectTransform root)
        {
            _failModalGo = CreateModalBackdrop("FailModal", root);
            RectTransform backdropRect = _failModalGo.GetComponent<RectTransform>();

            GameObject card = CreateImageObj("FailCard", backdropRect, "window1", Vector2.zero, new Vector2(760f, 780f));
            RectTransform cardRect = card.GetComponent<RectTransform>();

            _failTitleText = CreateTextObj("Txt_FailTitle", cardRect, "TIME'S UP!", 48, new Color(1f, 0.42f, 0.42f), TextAnchor.MiddleCenter);
            _failTitleText.rectTransform.anchoredPosition = new Vector2(0f, 275f);
            _failTitleText.rectTransform.sizeDelta = new Vector2(640f, 72f);

            CreateImageObj("FailClock", cardRect, "icon_clock", new Vector2(0f, 95f), new Vector2(180f, 180f));

            Text subTxt = CreateTextObj(
                "Txt_FailSub",
                cardRect,
                "Add +30 seconds to finish untangling!",
                28,
                new Color(0.92f, 0.96f, 1f),
                TextAnchor.MiddleCenter);
            subTxt.rectTransform.anchoredPosition = new Vector2(0f, -35f);
            subTxt.rectTransform.sizeDelta = new Vector2(620f, 64f);

            // Revive (+30s) Button (btn_green)
            GameObject reviveBtn = CreateSpriteButton(
                "Btn_Revive",
                cardRect,
                "btn_green",
                new Vector2(0f, -155f),
                new Vector2(430f, 114f),
                () =>
                {
                    _failModalGo.SetActive(false);
                    _board.ReviveWithExtraTime();
                });
            CreateTextObj("Txt_Revive", reviveBtn.GetComponent<RectTransform>(), "REVIVE (+30s)", 34, Color.white, TextAnchor.MiddleCenter);

            // Retry Button (btn_blue_1)
            GameObject retryBtn = CreateSpriteButton(
                "Btn_Retry",
                cardRect,
                "btn_blue_1",
                new Vector2(0f, -285f),
                new Vector2(430f, 114f),
                () => _board.LoadLevel(_board.CurrentLevelId));
            CreateTextObj("Txt_Retry", retryBtn.GetComponent<RectTransform>(), "RETRY LEVEL", 34, Color.white, TextAnchor.MiddleCenter);

            _failModalGo.SetActive(false);
        }

        private void BuildTelemetryModal(RectTransform root)
        {
            _telemetryModalGo = CreateModalBackdrop("TelemetryModal", root);
            RectTransform backdropRect = _telemetryModalGo.GetComponent<RectTransform>();

            GameObject card = CreateImageObj("TelemetryCard", backdropRect, "window1", Vector2.zero, new Vector2(860f, 920f));
            RectTransform cardRect = card.GetComponent<RectTransform>();

            Text title = CreateTextObj(
                "Txt_TelemetryHeader",
                cardRect,
                "1P TELEMETRY & SETTINGS (0 CDN)",
                34,
                new Color(1f, 0.95f, 0.45f),
                TextAnchor.MiddleCenter);
            title.rectTransform.anchoredPosition = new Vector2(0f, 380f);
            title.rectTransform.sizeDelta = new Vector2(720f, 64f);

            // Close Button (Window_Close)
            CreateSpriteButton(
                "Btn_CloseTelemetry",
                cardRect,
                "Window_Close",
                new Vector2(365f, 385f),
                new Vector2(82f, 84f),
                () => _telemetryModalGo.SetActive(false));

            // Telemetry Stats Box
            GameObject statsBox = CreateImageObj("TelemetryStatsBox", cardRect, "bg_content", new Vector2(0f, 200f), new Vector2(740f, 220f));
            statsBox.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.28f, 0.94f);
            _telemetryStatsText = CreateTextObj(
                "Txt_TelemetryStats",
                statsBox.GetComponent<RectTransform>(),
                "Session: -\nLogged: 0 | Queued: 0 | Flushed: 0",
                25,
                new Color(0.90f, 0.96f, 1f),
                TextAnchor.MiddleLeft);
            _telemetryStatsText.rectTransform.sizeDelta = new Vector2(690f, 190f);

            // Endpoint Input
            Text epLabel = CreateTextObj("Lbl_Endpoint", cardRect, "First-Party Endpoint URL:", 24, Color.white, TextAnchor.MiddleLeft);
            epLabel.rectTransform.anchoredPosition = new Vector2(0f, 45f);
            epLabel.rectTransform.sizeDelta = new Vector2(720f, 36f);

            _endpointInput = CreateInputField(cardRect, "EndpointInput", new Vector2(0f, -10f), new Vector2(720f, 64f));

            // API Key Input
            Text keyLabel = CreateTextObj("Lbl_ApiKey", cardRect, "First-Party Bearer / API Key:", 24, Color.white, TextAnchor.MiddleLeft);
            keyLabel.rectTransform.anchoredPosition = new Vector2(0f, -75f);
            keyLabel.rectTransform.sizeDelta = new Vector2(720f, 36f);

            _apiKeyInput = CreateInputField(cardRect, "ApiKeyInput", new Vector2(0f, -130f), new Vector2(720f, 64f));

            TelemetryConfig cfg = TelemetryManager.Instance.Config;
            if (cfg != null)
            {
                _endpointInput.text = cfg.endpointUrl;
                _apiKeyInput.text = cfg.apiKey;
            }

            // Toggle HTTP Flush Button
            GameObject toggleBtn = CreateSpriteButton(
                "Btn_ToggleFlush",
                cardRect,
                "btn_green",
                new Vector2(0f, -240f),
                new Vector2(560f, 92f),
                () =>
                {
                    TelemetryManager tm = TelemetryManager.Instance;
                    bool current = tm.Config != null && tm.Config.enableNetworkFlush;
                    tm.ConfigureEndpoint(_endpointInput.text, _apiKeyInput.text, !current);
                    RefreshTelemetryModalUI();
                });
            _netToggleBtnImg = toggleBtn.GetComponent<Image>();
            _netToggleBtnText = CreateTextObj("Txt_ToggleFlush", toggleBtn.GetComponent<RectTransform>(), "HTTP FLUSH: OFF (LOCAL JSONL)", 26, Color.white, TextAnchor.MiddleCenter);

            // Flush Queue Now Button
            GameObject flushBtn = CreateSpriteButton(
                "Btn_FlushNow",
                cardRect,
                "btn_blue_1",
                new Vector2(0f, -355f),
                new Vector2(460f, 96f),
                () =>
                {
                    TelemetryManager tm = TelemetryManager.Instance;
                    tm.ConfigureEndpoint(_endpointInput.text, _apiKeyInput.text, true);
                    tm.FlushAsync();
                    RefreshTelemetryModalUI();
                });
            CreateTextObj("Txt_FlushNow", flushBtn.GetComponent<RectTransform>(), "SAVE & FLUSH QUEUE NOW", 28, Color.white, TextAnchor.MiddleCenter);

            _telemetryModalGo.SetActive(false);
        }

        private void ToggleTelemetryModal()
        {
            if (_telemetryModalGo == null) return;
            bool nextState = !_telemetryModalGo.activeSelf;
            _telemetryModalGo.SetActive(nextState);
            if (nextState)
            {
                RefreshTelemetryModalUI();
            }
        }

        private void RefreshTelemetryModalUI()
        {
            if (_telemetryModalGo == null || !_telemetryModalGo.activeSelf)
                return;

            TelemetryManager tm = TelemetryManager.Instance;
            TelemetryConfig cfg = tm.Config;
            string sessShort = !string.IsNullOrEmpty(tm.SessionId) && tm.SessionId.Length >= 12
                ? tm.SessionId.Substring(0, 12)
                : tm.SessionId;
            string devShort = !string.IsNullOrEmpty(tm.DeviceId) && tm.DeviceId.Length >= 10
                ? tm.DeviceId.Substring(0, 10)
                : tm.DeviceId;

            _telemetryStatsText.text =
                $"Session: {sessShort}...  |  Device: {devShort}...\n" +
                $"Events Logged: {tm.TotalEventsLogged}  |  Offline Queue: {tm.QueuedEventCount}  |  Flushed: {tm.TotalBatchesFlushed}\n" +
                $"Last Event: {tm.LastEventSummary}\n" +
                $"Third-Party SDKs / CDNs: 0 (100% Isolated)";

            bool enabled = cfg != null && cfg.enableNetworkFlush;
            _netToggleBtnText.text = enabled ? "HTTP BATCH FLUSH: ENABLED" : "HTTP BATCH FLUSH: OFF (LOCAL ONLY)";
            _netToggleBtnImg.sprite = UISpriteLoader.LoadSprite(enabled ? "btn_green" : "btn_gray_1");
        }

        private void HandleLevelLoaded(int levelId)
        {
            if (_winModalGo != null) _winModalGo.SetActive(false);
            if (_failModalGo != null) _failModalGo.SetActive(false);

            if (_levelTitleText != null)
            {
                _levelTitleText.text = $"LEVEL {levelId}";
            }

            if (_hardBadgeGo != null)
            {
                _hardBadgeGo.SetActive(LevelDataManager.IsDifficultLevel(levelId));
            }
        }

        private void HandleLevelWon(int levelId, float duration, int moves)
        {
            if (_failModalGo != null) _failModalGo.SetActive(false);
            if (_winModalGo != null)
            {
                _winTitleText.text = $"LEVEL {levelId} CLEARED!";
                _winStatsText.text = $"Time: {duration:F1}s\nMoves: {moves}   |   Max Combo: x{Mathf.Max(1, _board.MaxCombo)}";
                _winModalGo.SetActive(true);
            }
        }

        private void HandleLevelFailed(int levelId, string reason)
        {
            if (_winModalGo != null) _winModalGo.SetActive(false);
            if (_failModalGo != null)
            {
                _failTitleText.text = $"LEVEL {levelId} FAILED!";
                _failModalGo.SetActive(true);
            }
        }

        private void Update()
        {
            if (_board == null)
                return;

            // Update Timer & Freeze state
            int mins = Mathf.FloorToInt(_board.RemainingTime / 60f);
            int secs = Mathf.FloorToInt(_board.RemainingTime % 60f);
            if (_timerText != null)
            {
                _timerText.text = _board.IsTimeFrozen
                    ? $"ICE {_board.FreezeRemainingTime:F0}s"
                    : $"{mins:00}:{secs:00}";
                _timerText.color = _board.IsTimeFrozen
                    ? new Color(0.55f, 0.95f, 1.0f)
                    : (_board.RemainingTime <= 15f ? new Color(1.0f, 0.42f, 0.42f) : Color.white);
            }

            if (_timerBgImage != null)
            {
                _timerBgImage.color = _board.IsTimeFrozen
                    ? new Color(0.65f, 0.92f, 1.0f, 1.0f)
                    : Color.white;
            }

            if (_iceOverlayGo != null && _iceOverlayGo.activeSelf != _board.IsTimeFrozen)
            {
                _iceOverlayGo.SetActive(_board.IsTimeFrozen);
            }

            if (_coinText != null)
            {
                _coinText.text = _board.Coins.ToString();
            }

            if (_ropesMovesText != null)
            {
                _ropesMovesText.text = $"Ropes: {_board.ActiveRopeGroupCount}   |   Moves: {_board.MoveCount}";
            }

            if (_comboFillImage != null)
            {
                float targetFill = Mathf.Clamp01(_board.ComboCount / 5f);
                _comboFillImage.fillAmount = Mathf.Lerp(_comboFillImage.fillAmount, targetFill, Time.deltaTime * 8f);
            }

            if (_comboText != null)
            {
                _comboText.text = _board.ComboCount > 0 ? $"COMBO x{_board.ComboCount}!" : "COMBO";
            }

            if (_telemetryModalGo != null && _telemetryModalGo.activeSelf)
            {
                RefreshTelemetryModalUI();
            }
        }

        private GameObject CreateModalBackdrop(string name, RectTransform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = go.GetComponent<Image>();
            img.sprite = UISpriteLoader.GetFallbackSprite();
            img.color = new Color(0.03f, 0.05f, 0.10f, 0.78f);
            return go;
        }

        private GameObject CreateImageObj(
            string name,
            RectTransform parent,
            string spriteName,
            Vector2 anchoredPos,
            Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Image img = go.GetComponent<Image>();
            img.sprite = UISpriteLoader.LoadSprite(spriteName);
            img.preserveAspect = false;
            return go;
        }

        private GameObject CreateSpriteButton(
            string name,
            RectTransform parent,
            string spriteName,
            Vector2 anchoredPos,
            Vector2 size,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = CreateImageObj(name, parent, spriteName, anchoredPos, size);
            Button btn = go.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.94f, 0.96f, 1.0f, 1.0f);
            cb.pressedColor = new Color(0.78f, 0.82f, 0.90f, 1.0f);
            btn.colors = cb;
            btn.onClick.AddListener(onClick);
            return go;
        }

        private Text CreateTextObj(
            string name,
            RectTransform parent,
            string content,
            int fontSize,
            Color color,
            TextAnchor align)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline), typeof(Shadow));
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = parent.sizeDelta;

            Text txt = go.GetComponent<Text>();
            txt.font = _uiFont;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.color = color;
            txt.alignment = align;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;

            Outline outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0.06f, 0.09f, 0.18f, 0.92f);
            outline.effectDistance = new Vector2(2f, -2f);

            Shadow shadow = go.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(0f, -3f);

            return txt;
        }

        private InputField CreateInputField(
            RectTransform parent,
            string name,
            Vector2 anchoredPos,
            Vector2 size)
        {
            GameObject go = CreateImageObj(name, parent, "di", anchoredPos, size);
            Image bg = go.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.11f, 0.20f, 0.95f);

            InputField input = go.AddComponent<InputField>();
            Text txt = CreateTextObj("Text", go.GetComponent<RectTransform>(), "", 24, Color.white, TextAnchor.MiddleLeft);
            txt.rectTransform.sizeDelta = new Vector2(size.x - 28f, size.y - 12f);
            input.textComponent = txt;
            return input;
        }
    }
}
