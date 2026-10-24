using HarmonyLib;
using VTOLVR.DLC.EW;

namespace EF24GAutoEWO
{
    static class PlayerJammerGuard
    {
        public static void MarkSelected(ElectronicWarfareController ew)
        {
            if (ew == null)
            {
                return;
            }

            Mark(ew.selectedTransmitter);
        }

        public static void MarkPod(ElectronicWarfareController ew)
        {
            if (ew == null || ew.selectedTransmitter == null)
            {
                return;
            }

            JammingPlanner planner = Planner();
            if (planner == null)
            {
                return;
            }

            planner.MarkPlayerPod(ew.selectedTransmitter.rj);
        }

        public static void MarkAll(ElectronicWarfareController ew)
        {
            JammingPlanner planner = Planner();
            if (planner == null)
            {
                return;
            }

            planner.MarkAllPlayerTransmitters(ew);
        }

        static void Mark(RadarJammer.JTransmitter transmitter)
        {
            JammingPlanner planner = Planner();
            if (planner == null)
            {
                return;
            }

            planner.MarkPlayerTransmitter(transmitter);
        }

        static JammingPlanner Planner()
        {
            Ef24AutoEwoMod mod = Ef24AutoEwoMod.Instance;
            if (mod == null || mod.Session == null)
            {
                return null;
            }

            return mod.Session.Jammer;
        }
    }

    [HarmonyPatch(typeof(MFDElectronicAttackDisplay), "MFD_StartJam")]
    static class PatchMfdStartJam
    {
        static void Prefix(MFDElectronicAttackDisplay __instance)
        {
            PlayerJammerGuard.MarkSelected(__instance.ewCon);
        }
    }

    [HarmonyPatch(typeof(MFDElectronicAttackDisplay), "MFD_StopJam")]
    static class PatchMfdStopJam
    {
        static void Prefix(MFDElectronicAttackDisplay __instance)
        {
            PlayerJammerGuard.MarkSelected(__instance.ewCon);
        }
    }

    [HarmonyPatch(typeof(MFDElectronicAttackDisplay), "MFD_TransmitMode")]
    static class PatchMfdTransmitMode
    {
        static void Prefix(MFDElectronicAttackDisplay __instance)
        {
            PlayerJammerGuard.MarkSelected(__instance.ewCon);
        }
    }

    [HarmonyPatch(typeof(MFDElectronicAttackDisplay), "MFD_CycleEMBand")]
    static class PatchMfdCycleBand
    {
        static void Prefix(MFDElectronicAttackDisplay __instance)
        {
            PlayerJammerGuard.MarkSelected(__instance.ewCon);
        }
    }

    [HarmonyPatch(typeof(MFDElectronicAttackDisplay), "MFD_ClearTarget")]
    static class PatchMfdClearTarget
    {
        static void Prefix(MFDElectronicAttackDisplay __instance)
        {
            PlayerJammerGuard.MarkSelected(__instance.ewCon);
        }
    }

    [HarmonyPatch(typeof(MFDElectronicAttackDisplay), "MFD_ARADTarget")]
    static class PatchMfdArad
    {
        static void Prefix(MFDElectronicAttackDisplay __instance)
        {
            PlayerJammerGuard.MarkSelected(__instance.ewCon);
        }
    }

    [HarmonyPatch(typeof(MFDElectronicAttackDisplay), "MFD_TGPTarget")]
    static class PatchMfdTgp
    {
        static void Prefix(MFDElectronicAttackDisplay __instance)
        {
            PlayerJammerGuard.MarkSelected(__instance.ewCon);
        }
    }

    [HarmonyPatch(typeof(MFDElectronicAttackDisplay), "MFD_TSDTarget")]
    static class PatchMfdTsd
    {
        static void Prefix(MFDElectronicAttackDisplay __instance)
        {
            PlayerJammerGuard.MarkSelected(__instance.ewCon);
        }
    }

    [HarmonyPatch(typeof(MFDElectronicAttackDisplay), "MFD_GPSTarget")]
    static class PatchMfdGps
    {
        static void Prefix(MFDElectronicAttackDisplay __instance)
        {
            PlayerJammerGuard.MarkSelected(__instance.ewCon);
        }
    }

    [HarmonyPatch(typeof(MFDElectronicAttackDisplay), "MFD_StopAll")]
    static class PatchMfdStopAll
    {
        static void Prefix(MFDElectronicAttackDisplay __instance)
        {
            PlayerJammerGuard.MarkAll(__instance.ewCon);
        }
    }

    [HarmonyPatch(typeof(MFDElectronicAttackDisplay), "MFD_TwoWayPowerAdjust")]
    static class PatchMfdPower
    {
        static void Prefix(MFDElectronicAttackDisplay __instance)
        {
            PlayerJammerGuard.MarkPod(__instance.ewCon);
        }
    }

    [HarmonyPatch(typeof(MFDElectronicAttackDisplay), "HOTASPowerAdjust")]
    static class PatchHotasPower
    {
        static void Prefix(MFDElectronicAttackDisplay __instance)
        {
            PlayerJammerGuard.MarkPod(__instance.ewCon);
        }
    }

    [HarmonyPatch(typeof(ElectronicWarfareController), "StopAllTransmission")]
    static class PatchStopAllTransmission
    {
        static void Prefix(ElectronicWarfareController __instance)
        {
            PlayerJammerGuard.MarkAll(__instance);
        }
    }

    [HarmonyPatch(typeof(EF24Hotas), "OnJoyTriggerDown")]
    static class PatchEwTrigger
    {
        static void Prefix(EF24Hotas __instance)
        {
            if (__instance.mode != EF24Hotas.MasterModes.EW)
            {
                return;
            }

            PlayerJammerGuard.MarkSelected(__instance.ewarCon);
        }
    }

    [HarmonyPatch(typeof(EF24Hotas), "Tsc_OnUnitSelectedEW")]
    static class PatchEwTsdSelect
    {
        static void Prefix(EF24Hotas __instance)
        {
            if (__instance.mode != EF24Hotas.MasterModes.EW)
            {
                return;
            }

            PlayerJammerGuard.MarkSelected(__instance.ewarCon);
        }
    }
}
