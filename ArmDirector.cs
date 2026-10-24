using System.Collections.Generic;
using UnityEngine;

namespace EF24GAutoEWO
{
    public class ArmDirector
    {
        readonly HashSet<int> engaged = new HashSet<int>();
        HPEquipARML selected;
        bool wantFire;
        float nextFire;
        public string Status = "反辐射辅助关闭";

        public void Reset()
        {
            engaged.Clear();
            selected = null;
            wantFire = false;
            nextFire = 0f;
            Status = "反辐射辅助关闭";
        }

        public void TickSlow(EwoSession session)
        {
            wantFire = false;
            selected = null;
            if (session == null || session.Settings == null || !session.IsReady || !session.Actor || !session.WeaponManager)
            {
                Status = "反辐射辅助无载机";
                return;
            }

            if (!session.Settings.ArmAssist)
            {
                Status = "反辐射辅助关闭";
                return;
            }

            CollectEngaged(session.Actor);
            HPEquipARML bestLauncher = null;
            ThreatContact bestThreat = null;
            float bestAngle = 999f;
            float bestMin = 0f;
            float bestMax = 0f;
            float bestRange = 0f;
            bool anyLauncher = false;
            bool anyDlz = false;
            for (int i = 0; i < session.WeaponManager.equipCount; i++)
            {
                HPEquipARML launcher = session.WeaponManager.GetEquip(i) as HPEquipARML;
                if (!launcher || launcher.markedForJettison || launcher.ml == null || launcher.ml.missileCount <= 0)
                {
                    continue;
                }

                anyLauncher = true;
                if (!launcher.dlz)
                {
                    continue;
                }

                anyDlz = true;
                float limit = SeekerLimit(launcher);
                for (int t = 0; t < session.Threats.Items.Count; t++)
                {
                    ThreatContact threat = session.Threats.Items[t];
                    if (threat.Score <= 0f || !threat.Actor || IsEngaged(threat.Actor))
                    {
                        continue;
                    }

                    if (!threat.HasRwr && (threat.Info == null || !threat.Info.radar))
                    {
                        continue;
                    }

                    Vector3 offset = threat.Position - launcher.transform.position;
                    float range = offset.magnitude;
                    float angle = Vector3.Angle(launcher.transform.forward, offset);
                    if (limit > 0f && angle > limit)
                    {
                        continue;
                    }

                    DynamicLaunchZone.LaunchParams launch = launcher.dlz.GetDynamicLaunchParams(session.Actor.velocity, threat.Position, threat.Velocity);
                    if (range <= launch.minLaunchRange || range >= launch.maxLaunchRange)
                    {
                        continue;
                    }

                    if (bestThreat == null || threat.Score > bestThreat.Score || (Mathf.Approximately(threat.Score, bestThreat.Score) && angle < bestAngle))
                    {
                        bestLauncher = launcher;
                        bestThreat = threat;
                        bestAngle = angle;
                        bestMin = launch.minLaunchRange;
                        bestMax = launch.maxLaunchRange;
                        bestRange = range;
                    }
                }
            }

            if (!anyLauncher)
            {
                Status = "未挂载反辐射弹";
                return;
            }

            if (!anyDlz)
            {
                Status = "反辐射挂架没有 DLZ";
                return;
            }

            if (bestLauncher == null || bestThreat == null)
            {
                Status = engaged.Count > 0 ? "包线内目标都已有友弹" : "没有进入包线的辐射源";
                return;
            }

            selected = bestLauncher;
            string rangeText = bestRange.ToString("0") + "m  RMin " + bestMin.ToString("0") + " / RMax " + bestMax.ToString("0");
            bool armedWeapon = session.WeaponManager.currentEquip == bestLauncher;
            bool fullAuto = session.Settings.LaunchMode == ArmLaunchMode.FullAuto && session.Settings.AllowAutoFire;
            if (armedWeapon || fullAuto)
            {
                Handoff(session, bestThreat);
            }
            if (session.Settings.LaunchMode == ArmLaunchMode.FullAuto && session.Settings.AllowAutoFire)
            {
                if (session.WeaponManager.isMasterArmed)
                {
                    int index = EquipIndex(session.WeaponManager, bestLauncher);
                    if (index >= 0 && session.WeaponManager.currentEquip != bestLauncher)
                    {
                        session.WeaponManager.SetWeapon(index);
                    }
                }

                wantFire = Time.time >= nextFire;
                Status = "全自动 " + EwoUtil.ActorLabel(bestThreat.Actor) + "  " + rangeText
                    + (session.WeaponManager.isMasterArmed ? "" : "  等待主臂");
            }
            else
            {
                Status = "半自动 " + EwoUtil.ActorLabel(bestThreat.Actor) + "  " + rangeText
                    + (armedWeapon && bestLauncher.LaunchAuthorized() ? "  导引头已交接，等待扳机" : "  请选择该反辐射弹");
            }
        }

        public void TickFast(EwoSession session)
        {
            if (!wantFire || session == null || session.Settings == null || !session.IsReady || !session.WeaponManager || !selected)
            {
                wantFire = false;
                return;
            }

            if (session.Settings.LaunchMode != ArmLaunchMode.FullAuto || !session.Settings.AllowAutoFire)
            {
                wantFire = false;
                return;
            }

            if (Time.time < nextFire || !session.WeaponManager.isMasterArmed)
            {
                return;
            }

            if (session.WeaponManager.currentEquip != selected || !selected.itemActivated || !selected.LaunchAuthorized())
            {
                return;
            }

            session.WeaponManager.StartFire();
            session.WeaponManager.EndFire();
            nextFire = Time.time + session.Settings.ArmCooldown;
            wantFire = false;
            Status = "已发射，冷却 " + session.Settings.ArmCooldown.ToString("0.0") + "s";
        }

        static void Handoff(EwoSession session, ThreatContact threat)
        {
            if (session == null || session.WeaponManager == null || threat == null)
            {
                return;
            }

            TacticalSituationController tsc = session.WeaponManager.tsc;
            if (tsc == null || threat.Info == null || threat.Info.tscIdx < 0)
            {
                return;
            }

            if (tsc.SelectedIndex() == threat.Info.tscIdx)
            {
                return;
            }

            try
            {
                tsc.SelectTarget(threat.Info.tscIdx);
            }
            catch (System.Exception ex)
            {
                EwoLog.Warn("TSD 交接失败: " + ex.Message);
            }
        }

        static int EquipIndex(WeaponManager wm, HPEquippable equip)
        {
            if (!wm || !equip)
            {
                return -1;
            }

            for (int i = 0; i < wm.equipCount; i++)
            {
                if (wm.GetEquip(i) == equip)
                {
                    return i;
                }
            }

            return -1;
        }

        static float SeekerLimit(HPEquipARML launcher)
        {
            if (!launcher || launcher.ml == null)
            {
                return -1f;
            }

            Missile missile = launcher.ml.GetNextMissile();
            if (!missile || !missile.antiRadRWR || !missile.antiRadRWR.useAntennae)
            {
                return -1f;
            }

            if (missile.antiRadRWR.antennaFov <= 1f)
            {
                return -1f;
            }

            return missile.antiRadRWR.antennaFov * 0.5f;
        }

        void CollectEngaged(Actor self)
        {
            engaged.Clear();
            if (Missile.allFiredMissiles == null)
            {
                return;
            }

            for (int i = 0; i < Missile.allFiredMissiles.Count; i++)
            {
                Missile missile = Missile.allFiredMissiles[i];
                if (!missile || missile.hasDetonated || missile.guidanceMode != Missile.GuidanceModes.AntiRad || !missile.antiRadTargetActor)
                {
                    continue;
                }

                Actor owner = missile.launchedByActor ? missile.launchedByActor : missile.actor;
                if (!owner || !self || owner.team != self.team)
                {
                    continue;
                }

                engaged.Add(missile.antiRadTargetActor.GetInstanceID());
                if (missile.antiRadTargetActor.parentActor)
                {
                    engaged.Add(missile.antiRadTargetActor.parentActor.GetInstanceID());
                }
            }
        }

        bool IsEngaged(Actor actor)
        {
            if (!actor)
            {
                return false;
            }

            if (engaged.Contains(actor.GetInstanceID()))
            {
                return true;
            }

            return actor.parentActor && engaged.Contains(actor.parentActor.GetInstanceID());
        }
    }
}
