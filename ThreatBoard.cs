using System.Collections.Generic;
using UnityEngine;

namespace EF24GAutoEWO
{
    public class ThreatContact
    {
        public Actor Actor;
        public TacticalSituationController.TSActorTargetInfo Info;
        public Vector3 Position;
        public Vector3 Velocity;
        public float Frequency;
        public EMBands Band;
        public float Signal;
        public float Distance;
        public float Score;
        public bool HasRwr;
        public bool Locked;
        public bool MissileSupport;
        public bool TerrainMasked;
        public bool IsSam;
        public bool IsAir;
        public string Symbol;

        public void Clear()
        {
            Actor = null;
            Info = null;
            Position = Vector3.zero;
            Velocity = Vector3.zero;
            Frequency = 0f;
            Band = EMBands.Mid;
            Signal = 0f;
            Distance = 0f;
            Score = 0f;
            HasRwr = false;
            Locked = false;
            MissileSupport = false;
            TerrainMasked = false;
            IsSam = false;
            IsAir = false;
            Symbol = "R";
        }
    }

    public class ThreatBoard
    {
        readonly List<ThreatContact> pool = new List<ThreatContact>();
        readonly Dictionary<int, ThreatContact> byActor = new Dictionary<int, ThreatContact>();
        readonly Dictionary<int, bool> terrain = new Dictionary<int, bool>();
        readonly List<TacticalSituationController.TSActorTargetInfo> terrainQueue = new List<TacticalSituationController.TSActorTargetInfo>();
        int terrainCursor;

        public readonly List<ThreatContact> Items = new List<ThreatContact>();
        public string HeadingText = "三角定位：无待收敛辐射源";

        public void Reset()
        {
            Items.Clear();
            byActor.Clear();
            terrain.Clear();
            terrainQueue.Clear();
            terrainCursor = 0;
            HeadingText = "三角定位：无待收敛辐射源";
        }

        public void Rebuild(EwoSession session)
        {
            Items.Clear();
            byActor.Clear();
            if (session == null || session.Settings == null || !session.IsReady)
            {
                return;
            }

            Actor self = session.Actor;
            WeaponManager wm = session.WeaponManager;
            if (!self || !wm)
            {
                return;
            }
            ModuleRWR rwr = session.Rwr;
            int rented = 0;
            if (rwr != null && rwr.contacts != null)
            {
                for (int i = 0; i < rwr.contacts.Length; i++)
                {
                    ModuleRWR.RWRContact contact = rwr.contacts[i];
                    if (contact == null || !contact.active || !contact.radarActor)
                    {
                        continue;
                    }

                    if (!EwoUtil.IsHostile(self, contact.radarActor))
                    {
                        continue;
                    }

                    ThreatContact threat = GetOrRent(contact.radarActor, ref rented);
                    threat.HasRwr = true;
                    threat.Locked |= contact.locked;
                    threat.MissileSupport |= contact.supportingMissile;
                    if (contact.signalStrength >= threat.Signal)
                    {
                        threat.Signal = contact.signalStrength;
                        threat.Frequency = contact.frequency;
                        threat.Position = contact.estimatedPosition;
                        threat.Velocity = contact.detectedVelocity;
                        if (!string.IsNullOrEmpty(contact.radarSymbol))
                        {
                            threat.Symbol = contact.radarSymbol;
                        }
                    }
                }
            }

            TacticalSituationController tsc = wm.tsc;
            if (tsc != null && tsc.infos != null)
            {
                foreach (KeyValuePair<int, TacticalSituationController.TSTargetInfo> pair in tsc.infos)
                {
                    TacticalSituationController.TSActorTargetInfo info = pair.Value as TacticalSituationController.TSActorTargetInfo;
                    if (info == null || !info.actor || !info.actor.alive)
                    {
                        continue;
                    }

                    if (tsc.GetTargetState(info) == TacticalSituationController.TargetStates.Lost)
                    {
                        continue;
                    }

                    if (!info.radar && !byActor.ContainsKey(info.actor.GetInstanceID()))
                    {
                        continue;
                    }

                    if (!EwoUtil.IsHostile(self, info.actor))
                    {
                        continue;
                    }

                    ThreatContact threat = GetOrRent(info.actor, ref rented);
                    threat.Info = info;
                    if (!threat.HasRwr || info.bestSource > TacticalSituationController.DataSources.RWR)
                    {
                        threat.Position = info.estimatedPosition;
                        threat.Velocity = info.velocity;
                    }

                    if (threat.Frequency <= 0f && info.radarFrequency > 0f)
                    {
                        threat.Frequency = info.radarFrequency;
                    }
                }
            }

            Vector3 selfPosition = self.position;
            EwoPriority priority = session.Settings.PriorityProfile;
            for (int i = 0; i < rented; i++)
            {
                ThreatContact threat = pool[i];
                if (!threat.Actor)
                {
                    continue;
                }

                threat.Band = Radar.GetEMBand(threat.Frequency);
                threat.Distance = Vector3.Distance(selfPosition, threat.Position);
                threat.IsSam = EwoUtil.IsSam(threat.Actor);
                threat.IsAir = EwoUtil.IsAir(threat.Actor);
                bool masked;
                threat.TerrainMasked = terrain.TryGetValue(threat.Actor.GetInstanceID(), out masked) && masked;
                threat.Score = ScoreOf(threat, priority);
                if (threat.Info != null && tsc != null && tsc.GetTargetState(threat.Info) == TacticalSituationController.TargetStates.Uncertain)
                {
                    threat.Score *= 0.55f;
                }

                Items.Add(threat);
            }
        }

        public void RefreshTerrain(EwoSession session)
        {
            if (session == null || !session.IsReady || !session.Actor || !session.WeaponManager || session.WeaponManager.tsc == null)
            {
                return;
            }

            if (terrain.Count > 128)
            {
                terrain.Clear();
            }

            terrainQueue.Clear();
            TacticalSituationController tsc = session.WeaponManager.tsc;
            if (tsc.infos == null)
            {
                return;
            }
            foreach (KeyValuePair<int, TacticalSituationController.TSTargetInfo> pair in tsc.infos)
            {
                TacticalSituationController.TSActorTargetInfo info = pair.Value as TacticalSituationController.TSActorTargetInfo;
                if (info == null || !info.actor || !info.radar)
                {
                    continue;
                }

                if (!EwoUtil.IsHostile(session.Actor, info.actor))
                {
                    continue;
                }

                terrainQueue.Add(info);
            }

            if (terrainQueue.Count == 0)
            {
                return;
            }

            Actor self = session.Actor;
            int count = Mathf.Min(EwoTune.MaxTerrainTests, terrainQueue.Count);
            for (int n = 0; n < count; n++)
            {
                if (terrainCursor >= terrainQueue.Count)
                {
                    terrainCursor = 0;
                }

                TacticalSituationController.TSActorTargetInfo info = terrainQueue[terrainCursor++];
                if (!info.actor)
                {
                    continue;
                }

                bool blocked = EwoUtil.TerrainBlocks(self.position, info.estimatedPosition, info.actor, self);
                terrain[info.actor.GetInstanceID()] = blocked;
            }
        }

        public void UpdateHeading(EwoSession session)
        {
            HeadingText = "三角定位：无待收敛辐射源";
            if (session == null || !session.IsReady || !session.Actor)
            {
                return;
            }

            ThreatContact best = null;
            for (int i = 0; i < Items.Count; i++)
            {
                ThreatContact threat = Items[i];
                if (threat.Score <= 0f || threat.Info == null)
                {
                    continue;
                }

                if (threat.Info.bestSource > TacticalSituationController.DataSources.RWRTriangulated)
                {
                    continue;
                }

                if (threat.Info.confidence >= EwoTune.OpenEllipseConfidence)
                {
                    continue;
                }

                if (best == null || threat.Score > best.Score)
                {
                    best = threat;
                }
            }

            if (best == null)
            {
                return;
            }

            float bearing;
            float recommended;
            if (!EwoUtil.TryOrthogonalHeading(session.Actor.position, session.Actor.transform.forward, best.Position, out bearing, out recommended))
            {
                return;
            }

            HeadingText = "三角定位 " + EwoUtil.ActorLabel(best.Actor)
                + "  建议航向 " + recommended.ToString("000")
                + "°  方位 " + bearing.ToString("000") + "°";
        }

        readonly ThreatContact liveEmergency = new ThreatContact();

        public bool TryLiveEmergency(EwoSession session, out ThreatContact best)
        {
            best = null;
            if (session == null || session.Settings == null || !session.IsReady || !session.Actor || session.Rwr == null || session.Rwr.contacts == null)
            {
                return false;
            }

            ModuleRWR.RWRContact winner = null;
            float winnerScore = 0f;
            ModuleRWR.RWRContact[] contacts = session.Rwr.contacts;
            for (int i = 0; i < contacts.Length; i++)
            {
                ModuleRWR.RWRContact contact = contacts[i];
                if (contact == null || !contact.active || !contact.radarActor)
                {
                    continue;
                }

                if (!contact.locked && !contact.supportingMissile)
                {
                    continue;
                }

                if (!EwoUtil.IsHostile(session.Actor, contact.radarActor))
                {
                    continue;
                }

                float score = (contact.supportingMissile ? 1000f : 700f) * PriorityWeight(session, contact.radarActor);
                if (score > winnerScore)
                {
                    winner = contact;
                    winnerScore = score;
                }
            }

            if (winner == null)
            {
                return false;
            }

            for (int i = 0; i < Items.Count; i++)
            {
                ThreatContact existing = Items[i];
                if (!existing.Actor || !EwoUtil.SameUnit(existing.Actor, winner.radarActor))
                {
                    continue;
                }

                existing.Locked = winner.locked;
                existing.MissileSupport = winner.supportingMissile;
                existing.HasRwr = true;
                existing.Frequency = winner.frequency;
                existing.Band = Radar.GetEMBand(winner.frequency);
                existing.Position = winner.estimatedPosition;
                existing.Velocity = winner.detectedVelocity;
                existing.Signal = winner.signalStrength;
                existing.Score = Mathf.Max(existing.Score, winnerScore);
                if (!string.IsNullOrEmpty(winner.radarSymbol))
                {
                    existing.Symbol = winner.radarSymbol;
                }

                best = existing;
                return true;
            }

            FillLive(session, winner, winnerScore);
            best = liveEmergency;
            return true;
        }

        void FillLive(EwoSession session, ModuleRWR.RWRContact contact, float score)
        {
            liveEmergency.Clear();
            liveEmergency.Actor = contact.radarActor;
            liveEmergency.HasRwr = true;
            liveEmergency.Locked = contact.locked;
            liveEmergency.MissileSupport = contact.supportingMissile;
            liveEmergency.Frequency = contact.frequency;
            liveEmergency.Band = Radar.GetEMBand(contact.frequency);
            liveEmergency.Position = contact.estimatedPosition;
            liveEmergency.Velocity = contact.detectedVelocity;
            liveEmergency.Signal = contact.signalStrength;
            liveEmergency.Score = score;
            liveEmergency.Symbol = string.IsNullOrEmpty(contact.radarSymbol) ? "R" : contact.radarSymbol;
            liveEmergency.IsSam = EwoUtil.IsSam(contact.radarActor);
            liveEmergency.IsAir = EwoUtil.IsAir(contact.radarActor);
            if (session.WeaponManager != null && session.WeaponManager.tsc != null)
            {
                liveEmergency.Info = session.WeaponManager.tsc.GetActorInfo(contact.radarActor);
            }
        }

        static float PriorityWeight(EwoSession session, Actor actor)
        {
            EwoPriority priority = session.Settings.PriorityProfile;
            if (priority == EwoPriority.Sam && EwoUtil.IsSam(actor))
            {
                return 2f;
            }

            if (priority == EwoPriority.Air && EwoUtil.IsAir(actor))
            {
                return 2f;
            }

            return 1f;
        }

        public bool TryBestEmergency(out ThreatContact best)
        {
            best = null;
            for (int i = 0; i < Items.Count; i++)
            {
                ThreatContact threat = Items[i];
                if (threat.Score <= 0f || (!threat.Locked && !threat.MissileSupport))
                {
                    continue;
                }

                if (best == null || threat.Score > best.Score)
                {
                    best = threat;
                }
            }

            return best != null;
        }

        ThreatContact GetOrRent(Actor actor, ref int rented)
        {
            int id = actor.GetInstanceID();
            ThreatContact existing;
            if (byActor.TryGetValue(id, out existing))
            {
                return existing;
            }

            if (rented >= pool.Count)
            {
                pool.Add(new ThreatContact());
            }

            ThreatContact threat = pool[rented++];
            threat.Clear();
            threat.Actor = actor;
            byActor.Add(id, threat);
            return threat;
        }

        static float ScoreOf(ThreatContact threat, EwoPriority priority)
        {
            if (threat.TerrainMasked && !threat.HasRwr)
            {
                return 0f;
            }

            float tier = threat.MissileSupport ? 1000f : (threat.Locked ? 700f : 250f);
            float distance = 8000f / (8000f + Mathf.Max(0f, threat.Distance));
            float signal = threat.HasRwr ? Mathf.Clamp(threat.Signal / 4000f, 0.35f, 2f) : 0.55f;
            float weight = 1f;
            if (priority == EwoPriority.Sam && threat.IsSam)
            {
                weight = 2f;
            }
            else if (priority == EwoPriority.Air && threat.IsAir)
            {
                weight = 2f;
            }

            float score = tier * distance * signal * weight;
            if (threat.Info != null && threat.Info.bestSource <= TacticalSituationController.DataSources.RWRTriangulated)
            {
                score *= Mathf.Lerp(0.65f, 1f, threat.Info.confidence);
            }

            return score;
        }
    }
}
