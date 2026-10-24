using UnityEngine;

namespace EF24GAutoEWO
{
    public class AutoCmsController
    {
        float nextChaff;
        float nextFlare;
        int statusCode = int.MinValue;
        public string Status = "CMS 待机";

        public void Reset()
        {
            nextChaff = 0f;
            nextFlare = 0f;
            statusCode = int.MinValue;
            Status = "CMS 待机";
        }

        public void TickFast(EwoSession session)
        {
            if (session != null)
            {
                session.TerminalHoj = false;
            }

            if (session == null || session.Settings == null || !session.IsReady || !session.Actor)
            {
                return;
            }

            bool radarDanger = false;
            bool radarTerminal = false;
            bool irDanger = false;
            Actor self = session.Actor;
            Vector3 selfPosition = self.position;
            if (Missile.allFiredMissiles != null)
            {
                for (int i = 0; i < Missile.allFiredMissiles.Count; i++)
                {
                    Missile missile = Missile.allFiredMissiles[i];
                    if (!missile || missile.hasDetonated || !missile.fired)
                    {
                        continue;
                    }

                    Actor owner = missile.launchedByActor ? missile.launchedByActor : missile.actor;
                    if (!EwoUtil.IsHostile(self, owner))
                    {
                        continue;
                    }

                    Vector3 missilePosition = missile.transform.position;
                    Vector3 toMe = selfPosition - missilePosition;
                    float distance = toMe.magnitude;
                    if (distance < 1f)
                    {
                        continue;
                    }

                    Vector3 velocity = missile.rb ? missile.rb.velocity : (missile.actor ? missile.actor.velocity : Vector3.zero);
                    float closing = Vector3.Dot(velocity, toMe / distance);
                    if (closing < 30f)
                    {
                        continue;
                    }

                    if (missile.guidanceMode == Missile.GuidanceModes.Heat && distance < session.Settings.CmsDangerRange)
                    {
                        irDanger = true;
                    }

                    if (missile.guidanceMode == Missile.GuidanceModes.Radar)
                    {
                        if (distance < session.Settings.CmsDangerRange)
                        {
                            radarDanger = true;
                        }

                        if (distance < session.Settings.TerminalHojRange)
                        {
                            radarTerminal = true;
                        }
                    }
                }
            }

            if (session.Rwr != null && session.Rwr.isMissileLocked)
            {
                radarDanger = true;
            }

            MissileDetector detector = session.Detector;
            if (detector != null && detector.missileIncomingDetected)
            {
                irDanger = true;
            }

            session.TerminalHoj = radarTerminal;
            bool chaffWanted = false;
            bool flareWanted = false;
            if (session.Settings.AutoCms && radarDanger)
            {
                chaffWanted = true;
            }

            if (session.Settings.AutoCms && irDanger)
            {
                flareWanted = true;
            }

            if (radarTerminal && session.Settings.Mode != EwoMode.Off)
            {
                chaffWanted = true;
            }

            if (chaffWanted && Time.time >= nextChaff)
            {
                Dispense(session.Cms, true);
                nextChaff = Time.time + session.Settings.CmsPulseInterval;
            }

            if (flareWanted && Time.time >= nextFlare)
            {
                Dispense(session.Cms, false);
                nextFlare = Time.time + session.Settings.CmsPulseInterval;
            }

            int code = (radarTerminal ? 1 : 0) | (radarDanger ? 2 : 0) | (irDanger ? 4 : 0) | (chaffWanted ? 8 : 0) | (flareWanted ? 16 : 0);
            if (code == statusCode)
            {
                return;
            }

            statusCode = code;
            Status = "CMS "
                + (radarTerminal ? "末端雷达弹 " : (radarDanger ? "雷达弹 " : ""))
                + (irDanger ? "红外弹 " : "")
                + (chaffWanted || flareWanted ? "抛洒中" : "监视");
        }

        static bool Dispense(CountermeasureManager manager, bool chaff)
        {
            if (manager == null)
            {
                return false;
            }

            if (chaff)
            {
                return FireBest(manager.chaffCMs);
            }

            return FireBest(manager.flareCMs);
        }

        static bool FireBest<T>(System.Collections.Generic.List<T> dispensers) where T : Countermeasure
        {
            if (dispensers == null)
            {
                return false;
            }

            Countermeasure best = null;
            for (int i = 0; i < dispensers.Count; i++)
            {
                Countermeasure dispenser = dispensers[i];
                if (!dispenser || !dispenser.enabled || dispenser.count <= 0)
                {
                    continue;
                }

                if (best == null || dispenser.count > best.count)
                {
                    best = dispenser;
                }
            }

            if (best == null)
            {
                return false;
            }

            return best.FireCM();
        }
    }
}
