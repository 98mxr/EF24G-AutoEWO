using System.Collections.Generic;
using UnityEngine;

namespace EF24GAutoEWO
{
    public class TgpPoller
    {
        readonly HashSet<int> written = new HashSet<int>();
        readonly Dictionary<int, float> retryAfter = new Dictionary<int, float>();
        OpticalTargeter bound;
        Actor aimActor;
        Vector3 aimPoint;
        int alignTicks;
        bool weHoldLock;
        float lastPlayerSlew = -100f;
        public string Status = "TGP 关闭";

        public void Reset()
        {
            Unhook();
            written.Clear();
            retryAfter.Clear();
            aimActor = null;
            alignTicks = 0;
            weHoldLock = false;
            lastPlayerSlew = -100f;
            Status = "TGP 关闭";
        }

        public void Bind(OpticalTargeter optical)
        {
            if (bound == optical)
            {
                return;
            }

            Unhook();
            bound = optical;
            if (bound != null)
            {
                bound.OnSlewedDirection += OnPlayerSlew;
            }
        }

        public void HoldAim(EwoSession session)
        {
            if (session == null || session.Settings == null || !session.IsReady || !session.Settings.AutoTgp || weHoldLock || !aimActor)
            {
                return;
            }

            if (PlayerBusy(session))
            {
                return;
            }

            OpticalTargeter optical = session.Optical;
            if (!optical || optical.locked || !aimActor || !optical.cameraTransform)
            {
                return;
            }

            Vector3 direction = aimPoint - optical.cameraTransform.position;
            if (direction.sqrMagnitude < 4f)
            {
                return;
            }

            optical.OverrideAimToDirection(direction.normalized, Vector3.up);
        }

        public void TickSlow(EwoSession session)
        {
            if (session == null || session.Settings == null || !session.IsReady || !session.Actor || !session.WeaponManager)
            {
                aimActor = null;
                Status = "TGP 无载机";
                return;
            }

            Bind(session.Optical);
            OpticalTargeter optical = session.Optical;
            if (!session.Settings.AutoTgp)
            {
                aimActor = null;
                Status = "TGP 轮询关闭";
                return;
            }

            if (!optical || !optical.powered)
            {
                aimActor = null;
                Status = "未挂载或未开机 TGP";
                return;
            }

            if (PlayerBusy(session))
            {
                aimActor = null;
                Status = "玩家正在操作 TGP";
                return;
            }

            ThreatContact candidate = Pick(session, optical);
            if (candidate == null)
            {
                aimActor = null;
                Status = "没有待写入 GPS 的辐射源";
                return;
            }

            if (aimActor != candidate.Actor)
            {
                aimActor = candidate.Actor;
                alignTicks = 0;
            }

            aimPoint = candidate.Actor.position;
            alignTicks++;
            if (!optical.cameraTransform)
            {
                Status = "TGP 没有相机";
                return;
            }

            Vector3 direction = aimPoint - optical.cameraTransform.position;
            float angle = Vector3.Angle(optical.cameraTransform.forward, direction);
            bool gimbalOk = optical.sensorTurret == null || optical.sensorTurret.TargetInGimbalRange(aimPoint);
            if (!gimbalOk || optical.isGimbalLimit)
            {
                if (alignTicks >= 8)
                {
                    Blacklist(candidate.Actor, 20f);
                    aimActor = null;
                    Status = EwoUtil.ActorLabel(candidate.Actor) + " 超出吊舱框架";
                }
                else
                {
                    Status = "转向 " + EwoUtil.ActorLabel(candidate.Actor);
                }

                return;
            }

            if (angle > EwoTune.TgpAlignDegrees)
            {
                if (alignTicks >= 10)
                {
                    Blacklist(candidate.Actor, 12f);
                    aimActor = null;
                    Status = EwoUtil.ActorLabel(candidate.Actor) + " 未能对准";
                }
                else
                {
                    Status = "对准 " + EwoUtil.ActorLabel(candidate.Actor) + "  " + angle.ToString("0") + "°";
                }

                return;
            }

            if (!candidate.Actor)
            {
                aimActor = null;
                Status = "目标已消失";
                return;
            }

            if (EwoUtil.TerrainBlocks(optical.cameraTransform.position, aimPoint, candidate.Actor, session.Actor))
            {
                Blacklist(candidate.Actor, 15f);
                aimActor = null;
                Status = EwoUtil.ActorLabel(candidate.Actor) + " 视线被地形挡住";
                return;
            }

            Actor lockedActor = candidate.Actor;
            weHoldLock = true;
            try
            {
                optical.ForceLockActor(lockedActor);
                bool recorded = WriteGps(session, lockedActor.position);
                written.Add(lockedActor.GetInstanceID());
                Status = (recorded ? "已写入 GPS " : "锁定但 GPS 未写入 ") + EwoUtil.ActorLabel(lockedActor);
            }
            finally
            {
                if (optical)
                {
                    optical.Unlock();
                }

                weHoldLock = false;
            }

            aimActor = null;
            alignTicks = 0;
        }

        ThreatContact Pick(EwoSession session, OpticalTargeter optical)
        {
            ThreatContact best = null;
            float maxDistance = optical.maxLockingDistance > 1f ? optical.maxLockingDistance : 14000f;
            for (int i = 0; i < session.Threats.Items.Count; i++)
            {
                ThreatContact threat = session.Threats.Items[i];
                if (threat.Score <= 0f || !threat.Actor || threat.TerrainMasked)
                {
                    continue;
                }

                if (!threat.HasRwr && (threat.Info == null || !threat.Info.radar))
                {
                    continue;
                }

                if (threat.Distance > maxDistance)
                {
                    continue;
                }

                int id = threat.Actor.GetInstanceID();
                if (written.Contains(id))
                {
                    continue;
                }

                float until;
                if (retryAfter.TryGetValue(id, out until) && Time.time < until)
                {
                    continue;
                }

                if (AlreadyInGps(session, threat.Actor.position))
                {
                    written.Add(id);
                    continue;
                }

                if (best == null || threat.Score > best.Score)
                {
                    best = threat;
                }
            }

            return best;
        }

        static bool AlreadyInGps(EwoSession session, Vector3 position)
        {
            if (session.WeaponManager == null)
            {
                return false;
            }

            GPSTargetSystem gps = session.WeaponManager.gpsSystem;
            if (gps == null || gps.targetGroups == null)
            {
                return false;
            }

            float limit = EwoTune.GpsMatchMeters * EwoTune.GpsMatchMeters;
            foreach (KeyValuePair<string, GPSTargetGroup> pair in gps.targetGroups)
            {
                GPSTargetGroup group = pair.Value;
                if (group == null || group.targets == null)
                {
                    continue;
                }

                for (int i = 0; i < group.targets.Count; i++)
                {
                    GPSTarget target = group.targets[i];
                    if (target != null && (target.worldPosition - position).sqrMagnitude <= limit)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        static bool WriteGps(EwoSession session, Vector3 position)
        {
            if (session.WeaponManager == null)
            {
                return false;
            }

            GPSTargetSystem gps = session.WeaponManager.gpsSystem;
            if (gps == null || gps.remoteOnly)
            {
                return false;
            }

            GPSTargetGroup group = gps.CreateGroup("EWO", 1);
            if (group == null)
            {
                return false;
            }

            if (group.targets == null)
            {
                return false;
            }

            int numeral = group.targets.Count + 1;
            group.AddTarget(new GPSTarget(position, "EWO", numeral));
            if (group.currentTargetIdx < 0)
            {
                group.currentTargetIdx = 0;
            }

            gps.TargetsChanged();
            return true;
        }

        bool PlayerBusy(EwoSession session)
        {
            OpticalTargeter optical = session.Optical;
            if (!optical)
            {
                return false;
            }

            if (Time.time - lastPlayerSlew < EwoTune.PlayerSlewGrace)
            {
                return true;
            }

            return optical.locked && !weHoldLock;
        }

        void Blacklist(Actor actor, float seconds)
        {
            if (actor)
            {
                retryAfter[actor.GetInstanceID()] = Time.time + seconds;
            }
        }

        void OnPlayerSlew(Vector3 direction)
        {
            lastPlayerSlew = Time.time;
        }

        void Unhook()
        {
            OpticalTargeter previous = bound;
            bound = null;
            if ((object)previous != null)
            {
                previous.OnSlewedDirection -= OnPlayerSlew;
            }
        }
    }
}
