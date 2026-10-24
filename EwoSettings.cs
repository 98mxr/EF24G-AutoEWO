using ModLoader.Framework.Settings;
using UnityEngine;

namespace EF24GAutoEWO
{
    public class EwoSettings : BaseSettings
    {
        public int OperatingMode = (int)EwoMode.Off;
        public int Priority = (int)EwoPriority.Balanced;
        public bool AllowLowBand = true;
        public bool AllowMidBand = true;
        public bool AllowHighBand = true;
        public float MaxPowerBudgetPercent = 100f;
        public float BatterySafeguardPercent = 25f;
        public float BeamWidthDegrees = 8f;
        public float TerminalHojRange = 2000f;
        public float CmsDangerRange = 3500f;
        public float CmsPulseInterval = 0.4f;
        public bool AutoCms = true;
        public bool AutoTgp = false;
        public bool ArmAssist = true;
        public int ArmFireMode = (int)ArmLaunchMode.SemiAuto;
        public bool AllowAutoFire = false;
        public float ArmCooldown = 2f;
        public bool ShowHud = true;
        public int ToggleKey = (int)KeyCode.F8;
        public string LockedChannelLabels = "";
        public int ManualLockVersion = 0;

        public EwoMode Mode
        {
            get { return (EwoMode)OperatingMode; }
            set { OperatingMode = (int)value; }
        }

        public EwoPriority PriorityProfile
        {
            get { return (EwoPriority)Priority; }
            set { Priority = (int)value; }
        }

        public ArmLaunchMode LaunchMode
        {
            get { return (ArmLaunchMode)ArmFireMode; }
            set { ArmFireMode = (int)value; }
        }

        public void Sanitize()
        {
            if (OperatingMode < 0 || OperatingMode > 2)
            {
                OperatingMode = 0;
            }

            if (Priority < 0 || Priority > 2)
            {
                Priority = 0;
            }

            if (ArmFireMode < 0 || ArmFireMode > 1)
            {
                ArmFireMode = 0;
            }

            MaxPowerBudgetPercent = Mathf.Clamp(MaxPowerBudgetPercent, 50f, 100f);
            BatterySafeguardPercent = Mathf.Clamp(BatterySafeguardPercent, 5f, 80f);
            BeamWidthDegrees = Mathf.Clamp(BeamWidthDegrees, 2f, 30f);
            TerminalHojRange = Mathf.Clamp(TerminalHojRange, 400f, 12000f);
            CmsDangerRange = Mathf.Clamp(CmsDangerRange, 500f, 20000f);
            CmsPulseInterval = Mathf.Clamp(CmsPulseInterval, 0.1f, 3f);
            ArmCooldown = Mathf.Clamp(ArmCooldown, 0.4f, 15f);
            if (ToggleKey == (int)KeyCode.None)
            {
                ToggleKey = (int)KeyCode.F8;
            }

            if (LockedChannelLabels == null)
            {
                LockedChannelLabels = "";
            }

            if (ManualLockVersion < 2)
            {
                LockedChannelLabels = "";
                ManualLockVersion = 2;
            }
        }
    }
}
