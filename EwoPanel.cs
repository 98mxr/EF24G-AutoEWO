using UnityEngine;

namespace EF24GAutoEWO
{
    public static class EwoPanel
    {
        static bool stylesReady;
        static int displayFrame = -1;
        static EwoSession drawn;
        static readonly GUI.WindowFunction windowFunction = DrawWindowId;
        static GUIStyle title;
        static GUIStyle body;
        static GUIStyle hud;
        static GUIStyle buttonOn;

        public static void Draw(EwoSession session)
        {
            if (session == null || session.Settings == null)
            {
                return;
            }

            EnsureStyles();
            EwoSettings settings = session.Settings;
            if (settings.ShowHud && !session.ShowPanel && !string.IsNullOrEmpty(session.Hud))
            {
                GUI.Label(new Rect(16f, 16f, 760f, 130f), session.Hud, hud);
            }

            if (!session.ShowPanel)
            {
                return;
            }

            if (displayFrame != Time.frameCount)
            {
                displayFrame = Time.frameCount;
                session.Jammer.RefreshDisplays();
            }

            drawn = session;
            session.WindowRect = GUI.Window(0xE424, session.WindowRect, windowFunction, "EF-24G Auto-EWO", title);
        }

        static void DrawWindowId(int id)
        {
            if (drawn != null && drawn.Settings != null)
            {
                DrawWindow(id, drawn);
            }
        }

        static void DrawWindow(int id, EwoSession session)
        {
            EwoSettings settings = session.Settings;
            session.Scroll = GUILayout.BeginScrollView(session.Scroll);
            GUILayout.Label(session.BindState, body);
            GUILayout.Label(session.Hud, body);
            GUILayout.Space(6f);
            GUILayout.Label("工作模式（OFF 只停止干扰接管）", body);
            GUILayout.BeginHorizontal();
            settings.Mode = ModeButton(settings.Mode, EwoMode.Off, "OFF");
            settings.Mode = ModeButton(settings.Mode, EwoMode.Defensive, "DEFENSIVE");
            settings.Mode = ModeButton(settings.Mode, EwoMode.Offensive, "OFFENSIVE");
            GUILayout.EndHorizontal();

            GUILayout.Label("目标优先级", body);
            GUILayout.BeginHorizontal();
            settings.PriorityProfile = PriorityButton(settings.PriorityProfile, EwoPriority.Sam, "SAM 优先");
            settings.PriorityProfile = PriorityButton(settings.PriorityProfile, EwoPriority.Air, "空中优先");
            settings.PriorityProfile = PriorityButton(settings.PriorityProfile, EwoPriority.Balanced, "均衡");
            GUILayout.EndHorizontal();

            GUILayout.Label("频段授权", body);
            settings.AllowLowBand = GUILayout.Toggle(settings.AllowLowBand, "允许 Low");
            settings.AllowMidBand = GUILayout.Toggle(settings.AllowMidBand, "允许 Mid");
            settings.AllowHighBand = GUILayout.Toggle(settings.AllowHighBand, "允许 High");

            settings.MaxPowerBudgetPercent = Slider("功率上限", settings.MaxPowerBudgetPercent, 50f, 100f, "0");
            settings.BatterySafeguardPercent = Slider("电量保护阈值", settings.BatterySafeguardPercent, 5f, 80f, "0");
            settings.BeamWidthDegrees = Slider("同频簇夹角", settings.BeamWidthDegrees, 2f, 30f, "0.0");
            settings.TerminalHojRange = Slider("末端防 HOJ 距离 m", settings.TerminalHojRange, 400f, 12000f, "0");
            settings.CmsDangerRange = Slider("CMS 危险距离 m", settings.CmsDangerRange, 500f, 20000f, "0");
            settings.CmsPulseInterval = Slider("CMS 脉冲间隔 s", settings.CmsPulseInterval, 0.1f, 3f, "0.00");
            settings.ArmCooldown = Slider("发射冷却 s", settings.ArmCooldown, 0.4f, 15f, "0.0");

            settings.AutoCms = GUILayout.Toggle(settings.AutoCms, "自动箔条 / 热焰弹");
            settings.AutoTgp = GUILayout.Toggle(settings.AutoTgp, "TGP 自动轮询并写入 GPS");
            settings.ArmAssist = GUILayout.Toggle(settings.ArmAssist, "反辐射交接");
            settings.AllowAutoFire = GUILayout.Toggle(settings.AllowAutoFire, "允许全自动发射（仍需要主臂）");
            settings.ShowHud = GUILayout.Toggle(settings.ShowHud, "显示简洁 HUD");

            GUILayout.BeginHorizontal();
            settings.LaunchMode = ArmButton(settings.LaunchMode, ArmLaunchMode.SemiAuto, "SEMI 半自动");
            settings.LaunchMode = ArmButton(settings.LaunchMode, ArmLaunchMode.FullAuto, "FULL 全自动");
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("通道锁定：勾选后 Auto-EWO 不再调度该通道", body);
            for (int i = 0; i < session.Jammer.Slots.Count; i++)
            {
                ChannelSlot slot = session.Jammer.Slots[i];
                if (slot == null)
                {
                    continue;
                }

                string caption = string.IsNullOrEmpty(slot.Display) ? slot.Label : slot.Display;
                bool manual = GUILayout.Toggle(slot.Manual, caption);
                if (manual != slot.Manual)
                {
                    session.SetChannelManual(slot, manual);
                    slot.RefreshDisplay();
                }
            }

            if (session.Jammer.Slots.Count == 0)
            {
                GUILayout.Label("当前没有干扰通道", body);
            }

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("清除手动锁"))
            {
                session.ClearManualLocks();
            }

            if (GUILayout.Button("配置 ADM-160"))
            {
                session.ConfigureMald();
            }

            if (GUILayout.Button("保存设置"))
            {
                session.SaveSettings();
            }

            GUILayout.EndHorizontal();
            GUILayout.Label(session.MaldResult, body);
            GUILayout.Label(session.Arm.Status, body);
            GUILayout.Label(session.Tgp.Status, body);

            GUILayout.Space(6f);
            GUILayout.Label("面板热键", body);
            GUILayout.BeginHorizontal();
            KeyButton(settings, "F8", KeyCode.F8);
            KeyButton(settings, "F9", KeyCode.F9);
            KeyButton(settings, "F10", KeyCode.F10);
            KeyButton(settings, "Ins", KeyCode.Insert);
            KeyButton(settings, "Home", KeyCode.Home);
            GUILayout.EndHorizontal();
            GUILayout.Label("鼠标可拖动标题栏并点击以上控件。热键当前为 " + ((KeyCode)settings.ToggleKey), body);
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        static float Slider(string label, float value, float min, float max, string format)
        {
            GUILayout.Label(label + "  " + value.ToString(format), body);
            return GUILayout.HorizontalSlider(value, min, max);
        }

        static EwoMode ModeButton(EwoMode current, EwoMode mode, string label)
        {
            GUIStyle style = current == mode ? buttonOn : GUI.skin.button;
            if (GUILayout.Button(label, style))
            {
                return mode;
            }

            return current;
        }

        static EwoPriority PriorityButton(EwoPriority current, EwoPriority priority, string label)
        {
            GUIStyle style = current == priority ? buttonOn : GUI.skin.button;
            if (GUILayout.Button(label, style))
            {
                return priority;
            }

            return current;
        }

        static ArmLaunchMode ArmButton(ArmLaunchMode current, ArmLaunchMode mode, string label)
        {
            GUIStyle style = current == mode ? buttonOn : GUI.skin.button;
            if (GUILayout.Button(label, style))
            {
                return mode;
            }

            return current;
        }

        static void KeyButton(EwoSettings settings, string label, KeyCode key)
        {
            GUIStyle style = settings.ToggleKey == (int)key ? buttonOn : GUI.skin.button;
            if (GUILayout.Button(label, style, GUILayout.Width(64f)))
            {
                settings.ToggleKey = (int)key;
            }
        }

        static void EnsureStyles()
        {
            if (stylesReady)
            {
                return;
            }

            Font font = null;
            try
            {
                font = Font.CreateDynamicFontFromOSFont(new string[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei" }, 15);
            }
            catch (System.Exception)
            {
                font = null;
            }

            title = new GUIStyle(GUI.skin.window);
            body = new GUIStyle(GUI.skin.label);
            hud = new GUIStyle(GUI.skin.label);
            buttonOn = new GUIStyle(GUI.skin.button);
            if (font != null)
            {
                title.font = font;
                body.font = font;
                hud.font = font;
                buttonOn.font = font;
            }

            title.fontSize = 15;
            body.fontSize = 14;
            body.wordWrap = true;
            hud.fontSize = 15;
            hud.normal.textColor = new Color(0.55f, 1f, 0.65f);
            buttonOn.fontSize = 14;
            buttonOn.normal.textColor = new Color(0.15f, 0.15f, 0.15f);
            buttonOn.normal.background = buttonOn.active.background;
            stylesReady = true;
        }
    }
}
