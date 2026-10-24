using System;
using ModLoader.Framework;
using ModLoader.Framework.Attributes;
using ModLoader.Framework.Settings;
using UnityEngine;

namespace EF24GAutoEWO
{
    [ItemId("ef24g.autoewo")]
    public class Ef24AutoEwoMod : VtolMod
    {
        public static Ef24AutoEwoMod Instance { get; private set; }
        public EwoSession Session { get; private set; }

        void Awake()
        {
            Instance = this;
            Session = new EwoSession();
            try
            {
                Session.Settings = BaseSettings.New<EwoSettings>(this);
            }
            catch (Exception ex)
            {
                EwoLog.Warn("读取设置失败，使用默认值: " + ex.Message);
                Session.Settings = new EwoSettings();
            }

            if (Session.Settings == null)
            {
                Session.Settings = new EwoSettings();
            }

            Session.Settings.Sanitize();
            EwoLog.Info("模组已加载。飞行中按 F8 打开控制面板。");
        }

        void Update()
        {
            if (Session != null)
            {
                Session.Tick();
            }
        }

        void OnGUI()
        {
            if (Session != null)
            {
                EwoPanel.Draw(Session);
            }
        }

        public override void UnLoad()
        {
            if (Session != null)
            {
                Session.Shutdown();
                Session = null;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        void OnDestroy()
        {
            UnLoad();
        }
    }
}
