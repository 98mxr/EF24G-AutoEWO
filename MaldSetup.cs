using System.Collections.Generic;
using UnityEngine;
using VTOLVR.DLC.EW;

namespace EF24GAutoEWO
{
    public class MaldSetup
    {
        public string Configure(EwoSession session)
        {
            if (session == null || !session.IsReady || !session.Actor || !session.WeaponManager)
            {
                return "没有可用的载机";
            }

            int dispensers = 0;
            int configured = 0;
            Vector3 centroid;
            int neighbors;
            if (!TryDensePoint(session, out centroid, out neighbors))
            {
                return "TSD 上没有可用于诱饵航路的敌方雷达";
            }

            GPSTargetSystem gps = session.WeaponManager.gpsSystem;
            if (gps == null || gps.remoteOnly)
            {
                return "GPS 系统不可写";
            }

            GPSTargetGroup path = gps.CreateGroup("MALD", 1);
            if (path == null)
            {
                return "无法创建 MALD 航路";
            }

            if (path.targets == null)
            {
                return "航路数据无效";
            }

            path.RemoveAllTargets();
            Vector3 self = session.Actor.position;
            Vector3 flat = centroid - self;
            flat.y = 0f;
            if (flat.sqrMagnitude > 1000f * 1000f)
            {
                Vector3 ingress = centroid - flat.normalized * Mathf.Min(8000f, flat.magnitude * 0.5f);
                ingress.y = centroid.y;
                path.AddTarget(new GPSTarget(ingress, "MALD", 1));
                path.AddTarget(new GPSTarget(centroid, "MALD", 2));
                path.isPath = true;
            }
            else
            {
                path.AddTarget(new GPSTarget(centroid, "MALD", 1));
                path.isPath = false;
            }

            path.currentTargetIdx = 0;
            gps.TargetsChanged();
            TargetIdentity identity = FirstDecoyIdentity(session.WeaponManager);

            for (int i = 0; i < session.WeaponManager.equipCount; i++)
            {
                HPEquipDecoyMissile dispenser = session.WeaponManager.GetEquip(i) as HPEquipDecoyMissile;
                if (!dispenser || dispenser.ml == null || dispenser.ml.missiles == null)
                {
                    continue;
                }

                dispensers++;
                for (int m = 0; m < dispenser.ml.missiles.Length; m++)
                {
                    Missile missile = dispenser.ml.missiles[m];
                    if (!missile || missile.fired)
                    {
                        continue;
                    }

                    AirLaunchedDecoyGuidance guidance = missile.guidanceUnit as AirLaunchedDecoyGuidance;
                    if (guidance == null)
                    {
                        continue;
                    }

                    guidance.tsc = session.WeaponManager.tsc;
                    guidance.uploadedPath = path;
                    if (identity != null)
                    {
                        guidance.SetDecoy(identity);
                    }

                    configured++;
                }
            }

            if (dispensers == 0)
            {
                return "已生成密集区航路，但没有挂载 ADM-160";
            }

            string sas = identity != null ? "SAS " + identity.targetName : "未找到 SAS 型号";
            return "已配置 " + configured + " 枚诱饵，邻居 " + neighbors + "，" + sas;
        }

        static bool TryDensePoint(EwoSession session, out Vector3 centroid, out int neighbors)
        {
            centroid = Vector3.zero;
            neighbors = 0;
            List<ThreatContact> radars = new List<ThreatContact>();
            for (int i = 0; i < session.Threats.Items.Count; i++)
            {
                ThreatContact threat = session.Threats.Items[i];
                if (threat.Score <= 0f || !threat.Actor)
                {
                    continue;
                }

                if (!threat.HasRwr && (threat.Info == null || !threat.Info.radar))
                {
                    continue;
                }

                radars.Add(threat);
            }

            List<ThreatContact> pool = PreferSam(radars);
            if (pool.Count == 0)
            {
                pool = radars;
            }

            if (pool.Count == 0)
            {
                return false;
            }

            int bestIndex = 0;
            int bestCount = -1;
            float limit = EwoTune.MaldNeighborMeters * EwoTune.MaldNeighborMeters;
            for (int i = 0; i < pool.Count; i++)
            {
                int count = 0;
                for (int j = 0; j < pool.Count; j++)
                {
                    if ((pool[i].Position - pool[j].Position).sqrMagnitude <= limit)
                    {
                        count++;
                    }
                }

                if (count > bestCount)
                {
                    bestCount = count;
                    bestIndex = i;
                }
            }

            Vector3 sum = Vector3.zero;
            int used = 0;
            for (int j = 0; j < pool.Count; j++)
            {
                if ((pool[bestIndex].Position - pool[j].Position).sqrMagnitude <= limit)
                {
                    sum += pool[j].Position;
                    used++;
                }
            }

            centroid = sum / Mathf.Max(1, used);
            neighbors = used;
            return true;
        }

        static List<ThreatContact> PreferSam(List<ThreatContact> radars)
        {
            List<ThreatContact> sam = new List<ThreatContact>();
            for (int i = 0; i < radars.Count; i++)
            {
                if (radars[i].IsSam)
                {
                    sam.Add(radars[i]);
                }
            }

            return sam;
        }

        static TargetIdentity FirstDecoyIdentity(WeaponManager wm)
        {
            for (int i = 0; i < wm.equipCount; i++)
            {
                HPEquipDecoyMissile dispenser = wm.GetEquip(i) as HPEquipDecoyMissile;
                if (!dispenser || dispenser.ml == null || dispenser.ml.missiles == null)
                {
                    continue;
                }

                for (int m = 0; m < dispenser.ml.missiles.Length; m++)
                {
                    Missile missile = dispenser.ml.missiles[m];
                    if (!missile)
                    {
                        continue;
                    }

                    AirLaunchedDecoyGuidance guidance = missile.guidanceUnit as AirLaunchedDecoyGuidance;
                    if (guidance == null || guidance.decoyModelOptions == null)
                    {
                        continue;
                    }

                    for (int o = 0; o < guidance.decoyModelOptions.Length; o++)
                    {
                        TargetIdentity identity = TargetIdentityManager.GetIdentity(guidance.decoyModelOptions[o]);
                        if (identity != null)
                        {
                            return identity;
                        }
                    }
                }
            }

            return null;
        }
    }
}
