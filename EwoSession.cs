using System;
using System.Text;
using UnityEngine;

namespace EF24GAutoEWO
{
    public class EwoSession
    {
        public readonly ThreatBoard Threats = new ThreatBoard();
        public readonly JammingPlanner Jammer = new JammingPlanner();
        public readonly AutoCmsController CmsController = new AutoCmsController();
        public readonly TgpPoller Tgp = new TgpPoller();
        public readonly ArmDirector Arm = new ArmDirector();
        public readonly MaldSetup Mald = new MaldSetup();
        public readonly StringBuilder HudBuilder = new StringBuilder(512);

        public EwoSettings Settings;
        public WeaponManager WeaponManager { get; private set; }
        public ElectronicWarfareController Ew { get; private set; }
        public Actor Actor { get; private set; }
        public Battery Battery { get; private set; }
        public ModuleEngine[] Engines { get; private set; }
        public AuxilliaryPowerUnit Apu { get; private set; }
        public CountermeasureManager Cms { get; private set; }
        public MissileDetector Detector { get; private set; }
        public OpticalTargeter Optical { get; private set; }
        public ModuleRWR Rwr { get; private set; }
        public bool IsReady { get; private set; }
        public bool TerminalHoj;
        public bool ShowPanel;
        public string Hud = "EF-24G Auto-EWO  待机";
        public string MaldResult = "尚未配置诱饵";
        public string BindState = "等待进入飞行";
        public Rect WindowRect = new Rect(24f, 24f, 520f, 700f);
        public Vector2 Scroll;

        int hooked = -1;
        float bindTimer;
        float fastAcc;
        float medAcc;
        float slowAcc;
        float lastError = -10f;
        bool shutDown;
        bool ewHooked;

        public void Tick()
        {
            if (shutDown || Settings == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            if (dt > 0.25f)
            {
                dt = 0.25f;
            }

            try
            {
                if (Input.GetKeyDown((KeyCode)Settings.ToggleKey))
                {
                    ShowPanel = !ShowPanel;
                }

                EnsureVehicle(dt);
                fastAcc += dt;
                medAcc += dt;
                slowAcc += dt;
                bool doSlow = slowAcc >= EwoTune.SlowSeconds;
                bool doMedium = medAcc >= EwoTune.MediumSeconds;
                bool doFast = fastAcc >= EwoTune.FastSeconds;
                if (doSlow)
                {
                    slowAcc = 0f;
                }

                if (doMedium)
                {
                    medAcc = 0f;
                }

                if (doFast)
                {
                    fastAcc = 0f;
                }

                if (doSlow && IsReady)
                {
                    Threats.RefreshTerrain(this);
                }

                if (doMedium && IsReady)
                {
                    int liveCount = Jammer.TransmitterCount(Ew);
                    if (liveCount != hooked)
                    {
                        Jammer.Rehook(this);
                        hooked = Jammer.Slots.Count;
                    }

                    Threats.Rebuild(this);
                    Jammer.TickMedium(this);
                }

                if (doSlow && IsReady)
                {
                    Threats.UpdateHeading(this);
                    Tgp.TickSlow(this);
                    Arm.TickSlow(this);
                }

                if (doFast)
                {
                    if (IsReady)
                    {
                        CmsController.TickFast(this);
                        Jammer.TickFast(this);
                        Arm.TickFast(this);
                        if (Jammer.LocksDirty)
                        {
                            Settings.LockedChannelLabels = Jammer.ExportLocks();
                        }
                    }

                    if (Settings.ShowHud || ShowPanel)
                    {
                        RefreshHud();
                    }
                }

                Tgp.HoldAim(this);
            }
            catch (Exception ex)
            {
                if (Time.unscaledTime - lastError > 5f)
                {
                    lastError = Time.unscaledTime;
                    EwoLog.Warn(ex.ToString());
                }
            }
        }

        public void SetChannelManual(ChannelSlot slot, bool manual)
        {
            Jammer.SetManual(slot, manual);
            Settings.LockedChannelLabels = Jammer.ExportLocks();
        }

        public void ClearManualLocks()
        {
            Jammer.ClearManual();
            Settings.LockedChannelLabels = "";
        }

        public void ConfigureMald()
        {
            try
            {
                MaldResult = Mald.Configure(this);
            }
            catch (Exception ex)
            {
                MaldResult = "诱饵配置失败: " + ex.Message;
                EwoLog.Warn(ex.ToString());
            }
        }

        public void SaveSettings()
        {
            if (Settings == null)
            {
                return;
            }

            Settings.Sanitize();
            Settings.LockedChannelLabels = Jammer.ExportLocks();
            try
            {
                Settings.Save();
                BindState = "设置已保存";
            }
            catch (Exception ex)
            {
                BindState = "设置保存失败";
                EwoLog.Warn(ex.Message);
            }
        }

        public void Shutdown()
        {
            if (shutDown)
            {
                return;
            }

            shutDown = true;
            try
            {
                SaveSettings();
            }
            catch (Exception)
            {
            }

            Unbind();
        }

        void EnsureVehicle(float dt)
        {
            Actor player = null;
            if (FlightSceneManager.instance != null)
            {
                player = FlightSceneManager.instance.playerActor;
            }

            if (!player)
            {
                if (IsReady || (object)Actor != null)
                {
                    Unbind();
                    Actor = null;
                    BindState = "等待进入飞行";
                }

                return;
            }

            if (player != Actor)
            {
                Unbind();
                Actor = player;
                bindTimer = 0f;
                TryBind(player);
                return;
            }

            if (IsReady && (!Actor || !WeaponManager || !Ew))
            {
                Unbind();
                BindState = "载机引用已失效";
                return;
            }

            if (!IsReady)
            {
                bindTimer += dt;
                if (bindTimer >= 0.5f)
                {
                    bindTimer = 0f;
                    TryBind(player);
                }
            }
        }

        void TryBind(Actor player)
        {
            if (!player)
            {
                return;
            }

            WeaponManager wm = player.GetComponentInChildren<WeaponManager>(true);
            if (!wm || !wm.ewCon)
            {
                IsReady = false;
                BindState = "当前载具没有电子战控制器";
                return;
            }

            UnhookEw();
            WeaponManager = wm;
            Ew = wm.ewCon;
            Battery = wm.battery;
            Optical = wm.opticalTargeter;
            Cms = player.GetComponentInChildren<CountermeasureManager>(true);
            Detector = Cms != null ? Cms.launchDetector : player.GetComponentInChildren<MissileDetector>(true);
            Rwr = wm.tsc != null ? wm.tsc.rwr : null;
            if (!Rwr && Cms != null)
            {
                Rwr = Cms.rwr;
            }

            Engines = player.GetComponentsInChildren<ModuleEngine>(true);
            Apu = player.GetComponentInChildren<AuxilliaryPowerUnit>(true);
            HookEw();
            try
            {
                Jammer.Rehook(this);
                hooked = Jammer.Slots.Count;
                Tgp.Bind(Optical);
                IsReady = true;
                BindState = "已接管 " + EwoUtil.ActorLabel(player);
                EwoLog.Info(BindState + "，通道 " + hooked);
            }
            catch (Exception ex)
            {
                EwoLog.Warn("接管失败: " + ex.Message);
                Unbind();
                Actor = player;
                BindState = "接管失败，稍后重试";
            }
        }

        void HookEw()
        {
            if (ewHooked || Ew == null)
            {
                return;
            }

            Ew.OnJammerEqChanged += OnJammersChanged;
            ewHooked = true;
        }

        void UnhookEw()
        {
            if (ewHooked && Ew != null)
            {
                Ew.OnJammerEqChanged -= OnJammersChanged;
            }

            ewHooked = false;
        }

        void OnJammersChanged()
        {
            if (!IsReady || Ew == null)
            {
                return;
            }

            Jammer.Rehook(this);
            hooked = Jammer.Slots.Count;
            Settings.LockedChannelLabels = Jammer.ExportLocks();
        }

        void Unbind()
        {
            UnhookEw();
            Jammer.Reset();
            Tgp.Reset();
            Arm.Reset();
            CmsController.Reset();
            Threats.Reset();
            WeaponManager = null;
            Ew = null;
            Battery = null;
            Engines = null;
            Apu = null;
            Cms = null;
            Detector = null;
            Optical = null;
            Rwr = null;
            IsReady = false;
            hooked = -1;
            TerminalHoj = false;
        }

        void RefreshHud()
        {
            HudBuilder.Length = 0;
            HudBuilder.Append("EF-24G Auto-EWO  ");
            HudBuilder.Append(EwoUtil.ModeName(Settings.Mode));
            if (!IsReady)
            {
                HudBuilder.Append("  ");
                HudBuilder.Append(BindState);
                Hud = HudBuilder.ToString();
                return;
            }

            if (Battery != null && Battery.maxCharge > 0.01f)
            {
                HudBuilder.Append("  电量 ");
                HudBuilder.Append((100f * Battery.currentCharge / Battery.maxCharge).ToString("0"));
                HudBuilder.Append('%');
            }

            if (Jammer.BatteryGuard)
            {
                HudBuilder.Append("  电力保护");
            }

            if (TerminalHoj)
            {
                HudBuilder.Append("  末端防HOJ");
            }

            HudBuilder.Append('\n');
            if (!string.IsNullOrEmpty(Jammer.Note))
            {
                HudBuilder.Append(Jammer.Note);
            }

            HudBuilder.Append('\n');
            if (!string.IsNullOrEmpty(Threats.HeadingText))
            {
                HudBuilder.Append(Threats.HeadingText);
            }

            HudBuilder.Append('\n');
            if (!string.IsNullOrEmpty(Arm.Status))
            {
                HudBuilder.Append(Arm.Status);
            }

            HudBuilder.Append('\n');
            if (!string.IsNullOrEmpty(Tgp.Status))
            {
                HudBuilder.Append(Tgp.Status);
            }

            HudBuilder.Append("  |  ");
            if (!string.IsNullOrEmpty(CmsController.Status))
            {
                HudBuilder.Append(CmsController.Status);
            }
            Hud = HudBuilder.ToString();
        }
    }
}
