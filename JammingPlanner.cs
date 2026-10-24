using System;
using System.Collections.Generic;
using UnityEngine;

namespace EF24GAutoEWO
{
    public class ChannelSlot
    {
        public RadarJammer Jammer;
        public RadarJammer.JTransmitter Tx;
        public int JammerIndex;
        public bool Manual;
        public bool AutoOwned;
        public bool ExpectOn;
        public float SilentSince = -1f;
        public float Score;
        public Actor Threat;
        public string Display = "空闲";
        int displaySig = int.MinValue;

        public void InvalidateDisplay()
        {
            displaySig = int.MinValue;
        }

        public string Label
        {
            get
            {
                if (Tx == null)
                {
                    return "?";
                }

                try
                {
                    string built = Tx.indicatorLabel;
                    if (!string.IsNullOrEmpty(built))
                    {
                        return built;
                    }
                }
                catch (System.Exception)
                {
                }

                return "J" + JammerIndex + ":" + Tx.index;
            }
        }

        public string Describe()
        {
            if (Tx == null)
            {
                return Label;
            }

            string owner = Manual ? "手动" : (AutoOwned ? "自动" : "空闲");
            string mode = Tx.isTransmitting ? Tx.mode.ToString() : "静默";
            string target = "无目标";
            if (Tx.tsdTarget != null && Tx.tsdTarget.actor)
            {
                target = EwoUtil.ActorLabel(Tx.tsdTarget.actor);
            }
            else if (Tx.aradTarget)
            {
                target = EwoUtil.ActorLabel(Tx.aradTarget);
            }

            return Label + "  " + owner + "  " + mode + "  " + EwoUtil.BandName(Tx.emBand) + "  " + target;
        }

        public void RefreshDisplay()
        {
            if (Tx == null)
            {
                Display = "?";
                displaySig = 0;
                return;
            }

            int targetId = 0;
            if (Tx.tsdTarget != null && Tx.tsdTarget.actor)
            {
                targetId = Tx.tsdTarget.actor.GetInstanceID();
            }
            else if (Tx.aradTarget)
            {
                targetId = Tx.aradTarget.GetInstanceID();
            }

            int sig = (Manual ? 1 : 0)
                | (AutoOwned ? 2 : 0)
                | (Tx.isTransmitting ? 4 : 0)
                | ((int)Tx.mode << 4)
                | ((int)Tx.emBand << 8)
                ^ targetId;
            if (sig == displaySig && Display != null)
            {
                return;
            }

            displaySig = sig;
            Display = Describe();
        }

        public bool IsJamming(Actor actor)
        {
            if (Tx == null || !Tx.isTransmitting || !actor)
            {
                return false;
            }

            if (Tx.targetMode == RadarJammer.JTransmitter.TargetModes.TSD && Tx.tsdTarget != null)
            {
                return EwoUtil.SameUnit(Tx.tsdTarget.actor, actor);
            }

            if (Tx.targetMode == RadarJammer.JTransmitter.TargetModes.ARAD)
            {
                return EwoUtil.SameUnit(Tx.aradTarget, actor);
            }

            return false;
        }
    }

    public class JammingPlanner
    {
        readonly List<ThreatContact> ranked = new List<ThreatContact>();
        readonly List<Cluster> clusters = new List<Cluster>();
        readonly List<PlanItem> plan = new List<PlanItem>();
        readonly List<PlanItem> planPool = new List<PlanItem>();
        readonly List<Cluster> clusterPool = new List<Cluster>();
        readonly HashSet<ChannelSlot> usedSlots = new HashSet<ChannelSlot>();
        readonly HashSet<string> lockedLabels = new HashSet<string>();
        int writing;
        bool rehooking;

        public readonly List<ChannelSlot> Slots = new List<ChannelSlot>();
        public bool BatteryGuard;
        public bool LocksDirty;
        public string Note = "";

        public void Reset()
        {
            ReleaseAuto();
            Slots.Clear();
            lockedLabels.Clear();
            BatteryGuard = false;
            LocksDirty = false;
            Note = "";
            writing = 0;
            rehooking = false;
        }

        public void LoadLocks(string labels)
        {
            lockedLabels.Clear();
            if (string.IsNullOrEmpty(labels))
            {
                return;
            }

            string[] parts = labels.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string label = parts[i].Trim();
                if (label.Length > 0)
                {
                    lockedLabels.Add(label);
                }
            }
        }

        public string ExportLocks()
        {
            LocksDirty = false;
            if (lockedLabels.Count == 0)
            {
                return "";
            }

            return string.Join(",", lockedLabels);
        }

        public void Rehook(EwoSession session)
        {
            if (rehooking)
            {
                return;
            }

            rehooking = true;
            try
            {
                RehookCore(session);
            }
            finally
            {
                rehooking = false;
            }
        }

        void RehookCore(EwoSession session)
        {
            List<ChannelSlot> previous = new List<ChannelSlot>(Slots);
            Slots.Clear();
            if (session == null || session.Settings == null || session.Ew == null || session.Ew.allJammers == null)
            {
                return;
            }

            LoadLocks(session.Settings.LockedChannelLabels);
            for (int j = 0; j < session.Ew.allJammers.Count; j++)
            {
                RadarJammer jammer = session.Ew.allJammers[j];
                if (!jammer || jammer.transmitters == null)
                {
                    continue;
                }

                for (int i = 0; i < jammer.transmitters.Length; i++)
                {
                    RadarJammer.JTransmitter tx = jammer.transmitters[i];
                    if (tx == null)
                    {
                        continue;
                    }

                    ChannelSlot slot = new ChannelSlot();
                    slot.Jammer = jammer;
                    slot.Tx = tx;
                    slot.JammerIndex = j;
                    ChannelSlot older = FindPrevious(previous, tx);
                    if (older != null)
                    {
                        slot.Manual = older.Manual;
                        slot.AutoOwned = older.AutoOwned;
                        slot.ExpectOn = older.ExpectOn;
                        slot.Score = older.Score;
                        slot.Threat = older.Threat;
                    }
                    else
                    {
                        slot.Manual = lockedLabels.Contains(slot.Label);
                        if (slot.Manual)
                        {
                            lockedLabels.Add(slot.Label);
                            LocksDirty = true;
                        }
                    }

                    Slots.Add(slot);
                }
            }

            session.Settings.LockedChannelLabels = ExportLocks();
            LocksDirty = false;
        }

        public int TransmitterCount(ElectronicWarfareController ew)
        {
            if (ew == null || ew.allJammers == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < ew.allJammers.Count; i++)
            {
                RadarJammer jammer = ew.allJammers[i];
                if (jammer && jammer.transmitters != null)
                {
                    count += jammer.transmitters.Length;
                }
            }

            return count;
        }

        public void SetManual(ChannelSlot slot, bool manual)
        {
            if (slot == null)
            {
                return;
            }

            slot.Manual = manual;
            if (manual)
            {
                slot.AutoOwned = false;
                slot.ExpectOn = false;
                if (lockedLabels.Add(slot.Label))
                {
                    LocksDirty = true;
                }
            }
            else if (lockedLabels.Remove(slot.Label))
            {
                LocksDirty = true;
            }

            slot.InvalidateDisplay();
        }

        public void ClearManual()
        {
            if (lockedLabels.Count > 0)
            {
                LocksDirty = true;
            }

            lockedLabels.Clear();
            for (int i = 0; i < Slots.Count; i++)
            {
                Slots[i].Manual = false;
                Slots[i].InvalidateDisplay();
            }
        }

        public void RefreshDisplays()
        {
            for (int i = 0; i < Slots.Count; i++)
            {
                ChannelSlot slot = Slots[i];
                if (slot != null)
                {
                    slot.RefreshDisplay();
                }
            }
        }

        public void TickFast(EwoSession session)
        {
            if (session == null || session.Settings == null || !session.IsReady || !session.Actor || Slots.Count == 0)
            {
                return;
            }

            if (session.Settings.Mode == EwoMode.Off)
            {
                return;
            }

            if (session.TerminalHoj)
            {
                CutNoiseForHoj(session);
            }

            if (session.Battery != null && !session.Battery.energized)
            {
                return;
            }

            ThreatContact emergency;
            if (!session.Threats.TryLiveEmergency(session, out emergency))
            {
                return;
            }

            if (!EwoUtil.BandAllowed(session.Settings, emergency.Band) || Covered(emergency.Actor))
            {
                return;
            }

            ChannelSlot slot = FindFree(session, emergency, null);
            if (slot == null)
            {
                slot = LowestAuto(emergency);
            }

            if (slot == null)
            {
                Note = AnyManualFree() ? "没有能覆盖该威胁的波束或频段" : "锁定/导弹逼近，但通道都在手动锁定中";
                return;
            }

            Task(session, slot, emergency, RadarJammer.TransmitModes.DRFM);
            Note = "RWR 抢占 " + slot.Label + " → " + EwoUtil.ActorLabel(emergency.Actor);
        }

        public void TickMedium(EwoSession session)
        {
            if (session == null || session.Settings == null || !session.IsReady || !session.Actor)
            {
                return;
            }

            BatteryGuard = ComputeBatteryGuard(session);
            if (session.Settings.Mode == EwoMode.Off || (session.Battery != null && !session.Battery.energized))
            {
                ReleaseAuto();
                return;
            }

            BuildPlan(session);
            ApplyPlan(session);
            ApplyPower(session);
        }

        public void ReleaseAuto()
        {
            for (int i = 0; i < Slots.Count; i++)
            {
                ChannelSlot slot = Slots[i];
                if (!slot.AutoOwned || slot.Manual)
                {
                    continue;
                }

                StopOwned(slot);
            }
        }

        void BuildPlan(EwoSession session)
        {
            ranked.Clear();
            clusters.Clear();
            plan.Clear();
            usedSlots.Clear();
            for (int i = 0; i < session.Threats.Items.Count; i++)
            {
                ThreatContact threat = session.Threats.Items[i];
                if (!Eligible(session, threat))
                {
                    continue;
                }

                ranked.Add(threat);
            }

            ranked.Sort(CompareScore);
            Vector3 selfPosition = session.Actor.position;
            float beam = session.Settings.BeamWidthDegrees;
            for (int i = 0; i < ranked.Count; i++)
            {
                ThreatContact threat = ranked[i];
                Vector3 direction = threat.Position - selfPosition;
                if (direction.sqrMagnitude < 4f)
                {
                    continue;
                }

                direction.Normalize();
                Cluster cluster = null;
                for (int c = 0; c < clusters.Count; c++)
                {
                    if (clusters[c].Band != threat.Band)
                    {
                        continue;
                    }

                    if (Vector3.Angle(clusters[c].SeedDirection, direction) < beam)
                    {
                        cluster = clusters[c];
                        break;
                    }
                }

                if (cluster == null)
                {
                    cluster = RentCluster();
                    cluster.Band = threat.Band;
                    cluster.SeedDirection = direction;
                }

                cluster.Members.Add(threat);
                if (threat.Score > cluster.Score)
                {
                    cluster.Score = threat.Score;
                }
            }

            for (int i = 0; i < clusters.Count; i++)
            {
                clusters[i].Finalize(selfPosition);
            }

            clusters.Sort(CompareCluster);
            for (int i = 0; i < clusters.Count; i++)
            {
                Cluster cluster = clusters[i];
                if (cluster.Core == null || !cluster.Core.Actor)
                {
                    continue;
                }

                ChannelSlot slot = FindFree(session, cluster.Core, usedSlots);
                if (slot == null)
                {
                    continue;
                }

                usedSlots.Add(slot);
                PlanItem item = RentPlan();
                item.Slot = slot;
                item.Threat = cluster.Core;
                item.Mode = PickMode(session, cluster.Core);
            }

            if (!session.TerminalHoj && clusters.Count > 0 && clusters[0].Core != null)
            {
                ThreatContact top = clusters[0].Core;
                if ((top.Locked || top.MissileSupport) && PickMode(session, top) == RadarJammer.TransmitModes.DRFM)
                {
                    ChannelSlot extra = FindFree(session, top, usedSlots);
                    if (extra != null)
                    {
                        usedSlots.Add(extra);
                        PlanItem noise = RentPlan();
                        noise.Slot = extra;
                        noise.Threat = top;
                        noise.Mode = RadarJammer.TransmitModes.NOISE;
                    }
                }
            }

            ThreatContact emergency;
            if (session.Threats.TryBestEmergency(out emergency) && Eligible(session, emergency) && !PlanCovers(emergency.Actor))
            {
                ChannelSlot victim = null;
                for (int i = 0; i < plan.Count; i++)
                {
                if (plan[i].Threat == null || !plan[i].Threat.Actor)
                {
                    continue;
                }

                if (victim == null || plan[i].Threat.Score < victim.Score)
                {
                    victim = plan[i].Slot;
                }
                }

                if (victim != null)
                {
                    for (int i = 0; i < plan.Count; i++)
                    {
                        if (plan[i].Slot == victim)
                        {
                            plan[i].Threat = emergency;
                            plan[i].Mode = RadarJammer.TransmitModes.DRFM;
                            break;
                        }
                    }
                }
            }
        }

        void ApplyPlan(EwoSession session)
        {
            for (int i = 0; i < Slots.Count; i++)
            {
                ChannelSlot slot = Slots[i];
                if (slot.Manual || !slot.AutoOwned)
                {
                    continue;
                }

                if (!PlanContains(slot))
                {
                    StopOwned(slot);
                }
            }

            for (int i = 0; i < plan.Count; i++)
            {
                PlanItem item = plan[i];
                if (item.Slot == null || item.Threat == null)
                {
                    continue;
                }

                Task(session, item.Slot, item.Threat, item.Mode);
            }

            if (plan.Count == 0)
            {
                Note = session.Settings.Mode == EwoMode.Defensive ? "静默，等待锁定或导弹" : "没有可分配的威胁";
            }
            else
            {
                Note = "干扰簇 " + clusters.Count + "，自动通道 " + plan.Count;
            }
        }

        void ApplyPower(EwoSession session)
        {
            float budget = session.Settings.MaxPowerBudgetPercent / 100f;
            for (int j = 0; j < Slots.Count; j++)
            {
                RadarJammer jammer = Slots[j].Jammer;
                if (!jammer)
                {
                    continue;
                }

                bool seen = false;
                for (int k = 0; k < j; k++)
                {
                    if (Slots[k].Jammer == jammer)
                    {
                        seen = true;
                        break;
                    }
                }

                if (seen)
                {
                    continue;
                }

                float manualSum = 0f;
                int autoCount = 0;
                for (int i = 0; i < Slots.Count; i++)
                {
                    ChannelSlot slot = Slots[i];
                    if (slot.Jammer != jammer || slot.Tx == null)
                    {
                        continue;
                    }

                    if (slot.Manual)
                    {
                        manualSum += slot.Tx.power;
                    }
                    else if (slot.AutoOwned && slot.Tx.isTransmitting)
                    {
                        autoCount++;
                    }
                }

                float remain = Mathf.Max(0f, budget - manualSum);
                if (autoCount == 0)
                {
                    continue;
                }

                if (remain < 0.02f)
                {
                    for (int i = 0; i < Slots.Count; i++)
                    {
                        if (Slots[i].Jammer == jammer && Slots[i].AutoOwned && !Slots[i].Manual)
                        {
                            StopOwned(Slots[i]);
                        }
                    }

                    continue;
                }

                float share = Mathf.Clamp01(remain / autoCount);
                Write(delegate
                {
                    if (!jammer)
                    {
                        return;
                    }

                    for (int i = 0; i < Slots.Count; i++)
                    {
                        ChannelSlot slot = Slots[i];
                        if (slot.Jammer != jammer || slot.Manual || !slot.AutoOwned || slot.Tx == null || !slot.Tx.isTransmitting)
                        {
                            continue;
                        }

                        if (Mathf.Abs(slot.Tx.power - share) > 0.03f)
                        {
                            slot.Tx.power = share;
                        }
                    }
                });
            }
        }

        void Task(EwoSession session, ChannelSlot slot, ThreatContact threat, RadarJammer.TransmitModes mode)
        {
            if (slot == null || slot.Tx == null || !slot.Jammer || threat == null || !threat.Actor)
            {
                return;
            }

            if (slot.Manual || !CanCommand(slot) || !HasAimReference(slot.Jammer) || !slot.Jammer.CanJamBand(threat.Band) || !EwoUtil.BandAllowed(session.Settings, threat.Band))
            {
                return;
            }

            if (!slot.Jammer.IsTargetInFOV(threat.Position))
            {
                return;
            }

            RadarJammer.JTransmitter tx = slot.Tx;
            bool sameActor = slot.IsJamming(threat.Actor) || SameSelected(tx, threat.Actor);
            bool steady = sameActor && tx.isTransmitting && tx.mode == mode && tx.emBand == threat.Band;
            if (steady)
            {
                slot.AutoOwned = true;
                slot.ExpectOn = true;
                slot.Score = threat.Score;
                slot.Threat = threat.Actor;
                slot.SilentSince = -1f;
                return;
            }

            bool started = false;
            Write(delegate
            {
                if (tx.isTransmitting && CanCommand(slot))
                {
                    slot.Jammer.StopJam(tx.index);
                }

                if (tx.mode != RadarJammer.TransmitModes.NOISE)
                {
                    tx.mode = RadarJammer.TransmitModes.NOISE;
                }

                bool usedTsd = false;
                if (threat.Info != null && slot.Jammer.tsc != null && (mode == RadarJammer.TransmitModes.NOISE || threat.Info.radar))
                {
                    tx.SetTSDTarget(threat.Info);
                    usedTsd = true;
                }
                else
                {
                    string symbol = string.IsNullOrEmpty(threat.Symbol) ? "R" : threat.Symbol;
                    tx.SetARADTarget(threat.Actor, symbol);
                }

                tx.UpdateTargetData(threat.Position, threat.Velocity);
                if (tx.emBand != threat.Band)
                {
                    tx.SetEMBand(threat.Band);
                }

                if (tx.mode != mode)
                {
                    tx.mode = mode;
                }

                if (usedTsd && mode == RadarJammer.TransmitModes.DRFM && (threat.Info == null || !threat.Info.radar))
                {
                    tx.SetARADTarget(threat.Actor, string.IsNullOrEmpty(threat.Symbol) ? "R" : threat.Symbol);
                    tx.UpdateTargetData(threat.Position, threat.Velocity);
                }

                started = CanCommand(slot) && slot.Jammer.BeginJam(tx.index);
            });

            slot.AutoOwned = started;
            slot.ExpectOn = started;
            slot.Score = threat.Score;
            slot.Threat = started ? threat.Actor : null;
            slot.SilentSince = started ? -1f : Time.time;
        }

        void CutNoiseForHoj(EwoSession session)
        {
            for (int i = 0; i < Slots.Count; i++)
            {
                ChannelSlot slot = Slots[i];
                if (slot.Manual || !slot.AutoOwned || slot.Tx == null || !slot.Tx.isTransmitting)
                {
                    continue;
                }

                if (slot.Tx.mode != RadarJammer.TransmitModes.NOISE)
                {
                    continue;
                }

                ThreatContact threat = FindThreat(session, slot.Threat);
                if (threat != null && (threat.Info == null || threat.Info.radar || threat.HasRwr))
                {
                    Task(session, slot, threat, RadarJammer.TransmitModes.DRFM);
                }
                else
                {
                    StopOwned(slot);
                }
            }
        }

        public void MarkPlayerTransmitter(RadarJammer.JTransmitter transmitter)
        {
            if (transmitter == null || writing > 0)
            {
                return;
            }

            for (int i = 0; i < Slots.Count; i++)
            {
                ChannelSlot slot = Slots[i];
                if (slot.Tx != transmitter || slot.Manual)
                {
                    continue;
                }

                SetManual(slot, true);
                Note = slot.Label + " 玩家操作，已锁定";
                return;
            }
        }

        public void MarkPlayerPod(RadarJammer jammer)
        {
            if (!jammer || jammer.transmitters == null || writing > 0)
            {
                return;
            }

            for (int i = 0; i < jammer.transmitters.Length; i++)
            {
                MarkPlayerTransmitter(jammer.transmitters[i]);
            }
        }

        public void MarkAllPlayerTransmitters(ElectronicWarfareController ew)
        {
            if (ew == null || ew.allJammers == null || writing > 0)
            {
                return;
            }

            for (int i = 0; i < ew.allJammers.Count; i++)
            {
                MarkPlayerPod(ew.allJammers[i]);
            }
        }

        bool ComputeBatteryGuard(EwoSession session)
        {
            Battery battery = session.Battery;
            if (battery == null)
            {
                return false;
            }

            if (!battery.connected || !battery.energized || battery.maxCharge <= 0.01f)
            {
                return true;
            }

            float fraction = battery.currentCharge / battery.maxCharge;
            if (fraction >= session.Settings.BatterySafeguardPercent / 100f)
            {
                return false;
            }

            return GenerationPerSecond(session) + 0.01f < DrainPerSecond();
        }

        static float GenerationPerSecond(EwoSession session)
        {
            float generation = 0f;
            if (session.Engines != null)
            {
                for (int i = 0; i < session.Engines.Length; i++)
                {
                    ModuleEngine engine = session.Engines[i];
                    if (engine)
                    {
                        generation += engine.alternatorChargeRate * Mathf.Max(0f, engine.finalThrottle);
                    }
                }
            }

            if (session.Apu && session.Apu.isPowerEnabled)
            {
                generation += session.Apu.chargeRate * Mathf.Max(0f, session.Apu.rpm);
            }

            return generation;
        }

        float DrainPerSecond()
        {
            float drain = 0f;
            for (int i = 0; i < Slots.Count; i++)
            {
                ChannelSlot slot = Slots[i];
                if (slot.Tx == null || !slot.Tx.isTransmitting || !slot.Jammer)
                {
                    continue;
                }

                drain += slot.Jammer.maxPowerOutput * slot.Tx.power * slot.Jammer.jammingBatteryDrainFactor;
            }

            return drain;
        }

        bool Eligible(EwoSession session, ThreatContact threat)
        {
            if (threat == null || !threat.Actor || threat.Score <= 0f)
            {
                return false;
            }

            if (!EwoUtil.BandAllowed(session.Settings, threat.Band))
            {
                return false;
            }

            if (session.Settings.Mode == EwoMode.Defensive && !threat.Locked && !threat.MissileSupport)
            {
                return false;
            }

            if (BatteryGuard && !threat.Locked && !threat.MissileSupport)
            {
                return false;
            }

            return true;
        }

        ChannelSlot FindFree(EwoSession session, ThreatContact threat, HashSet<ChannelSlot> used)
        {
            if (threat == null || !threat.Actor)
            {
                return null;
            }

            ChannelSlot idle = null;
            ChannelSlot same = null;
            for (int i = 0; i < Slots.Count; i++)
            {
                ChannelSlot slot = Slots[i];
                if (slot.Manual || slot.Tx == null || !slot.Jammer)
                {
                    continue;
                }

                if (used != null && used.Contains(slot))
                {
                    continue;
                }

                if (!HasAimReference(slot.Jammer) || !slot.Jammer.CanJamBand(threat.Band) || !slot.Jammer.IsTargetInFOV(threat.Position))
                {
                    continue;
                }

                if (SameSelected(slot.Tx, threat.Actor))
                {
                    same = slot;
                    break;
                }

                bool dropped = ranked.Count > 0 && !Ranked(slot.Threat);
                bool idleChannel = !slot.AutoOwned || !slot.Tx.isTransmitting || dropped;
                if (idle == null && idleChannel)
                {
                    idle = slot;
                }
            }

            return same != null ? same : idle;
        }

        ChannelSlot LowestAuto(ThreatContact emergency)
        {
            if (emergency == null || !emergency.Actor)
            {
                return null;
            }

            ChannelSlot worst = null;
            for (int i = 0; i < Slots.Count; i++)
            {
                ChannelSlot slot = Slots[i];
                if (slot.Manual || !slot.AutoOwned || slot.Tx == null || !slot.Jammer)
                {
                    continue;
                }

                if (!HasAimReference(slot.Jammer) || !slot.Jammer.CanJamBand(emergency.Band) || !slot.Jammer.IsTargetInFOV(emergency.Position))
                {
                    continue;
                }

                if (worst == null || slot.Score < worst.Score)
                {
                    worst = slot;
                }
            }

            return worst;
        }

        bool Covered(Actor actor)
        {
            for (int i = 0; i < Slots.Count; i++)
            {
                if (Slots[i].IsJamming(actor))
                {
                    return true;
                }
            }

            return false;
        }

        bool PlanCovers(Actor actor)
        {
            for (int i = 0; i < plan.Count; i++)
            {
                PlanItem item = plan[i];
                if (item.Threat != null && EwoUtil.SameUnit(item.Threat.Actor, actor))
                {
                    return true;
                }
            }

            return false;
        }

        bool PlanContains(ChannelSlot slot)
        {
            for (int i = 0; i < plan.Count; i++)
            {
                if (plan[i].Slot == slot)
                {
                    return true;
                }
            }

            return false;
        }

        static bool SameSelected(RadarJammer.JTransmitter tx, Actor actor)
        {
            if (tx == null || !actor)
            {
                return false;
            }

            if (tx.targetMode == RadarJammer.JTransmitter.TargetModes.TSD && tx.tsdTarget != null)
            {
                return EwoUtil.SameUnit(tx.tsdTarget.actor, actor);
            }

            if (tx.targetMode == RadarJammer.JTransmitter.TargetModes.ARAD)
            {
                return EwoUtil.SameUnit(tx.aradTarget, actor);
            }

            return false;
        }

        static RadarJammer.TransmitModes PickMode(EwoSession session, ThreatContact threat)
        {
            if (session.TerminalHoj && (threat.Locked || threat.MissileSupport))
            {
                return RadarJammer.TransmitModes.DRFM;
            }

            if (threat.Locked || threat.MissileSupport)
            {
                return RadarJammer.TransmitModes.DRFM;
            }

            return RadarJammer.TransmitModes.NOISE;
        }

        static ThreatContact FindThreat(EwoSession session, Actor actor)
        {
            if (!actor)
            {
                return null;
            }

            for (int i = 0; i < session.Threats.Items.Count; i++)
            {
                if (EwoUtil.SameUnit(session.Threats.Items[i].Actor, actor))
                {
                    return session.Threats.Items[i];
                }
            }

            return null;
        }

        void StopOwned(ChannelSlot slot)
        {
            slot.ExpectOn = false;
            slot.AutoOwned = false;
            slot.Threat = null;
            slot.Score = 0f;
            slot.SilentSince = -1f;
            if (CanCommand(slot) && slot.Tx.isTransmitting)
            {
                RadarJammer jammer = slot.Jammer;
                int index = slot.Tx.index;
                Write(delegate { jammer.StopJam(index); });
            }
        }

        static bool CanCommand(ChannelSlot slot)
        {
            if (slot == null || slot.Tx == null || !slot.Jammer || slot.Jammer.transmitters == null)
            {
                return false;
            }

            int index = slot.Tx.index;
            return index >= 0 && index < slot.Jammer.transmitters.Length && slot.Jammer.transmitters[index] == slot.Tx;
        }

        static bool HasAimReference(RadarJammer jammer)
        {
            return jammer && (jammer.fov <= 0f || jammer.fovRef);
        }

        Cluster RentCluster()
        {
            Cluster cluster;
            if (clusters.Count < clusterPool.Count)
            {
                cluster = clusterPool[clusters.Count];
            }
            else
            {
                cluster = new Cluster();
                cluster.Members = new List<ThreatContact>(4);
                clusterPool.Add(cluster);
            }

            cluster.Core = null;
            cluster.Score = 0f;
            cluster.Members.Clear();
            clusters.Add(cluster);
            return cluster;
        }

        PlanItem RentPlan()
        {
            PlanItem item;
            if (plan.Count < planPool.Count)
            {
                item = planPool[plan.Count];
            }
            else
            {
                item = new PlanItem();
                planPool.Add(item);
            }

            plan.Add(item);
            return item;
        }

        bool Ranked(Actor actor)
        {
            if (!actor)
            {
                return false;
            }

            for (int i = 0; i < ranked.Count; i++)
            {
                if (EwoUtil.SameUnit(ranked[i].Actor, actor))
                {
                    return true;
                }
            }

            return false;
        }

        bool AnyManualFree()
        {
            for (int i = 0; i < Slots.Count; i++)
            {
                if (!Slots[i].Manual)
                {
                    return true;
                }
            }

            return false;
        }

        void Write(Action action)
        {
            writing++;
            try
            {
                action();
            }
            finally
            {
                writing--;
            }
        }

        static ChannelSlot FindPrevious(List<ChannelSlot> previous, RadarJammer.JTransmitter tx)
        {
            for (int i = 0; i < previous.Count; i++)
            {
                if (previous[i].Tx == tx)
                {
                    return previous[i];
                }
            }

            return null;
        }

        static int CompareScore(ThreatContact a, ThreatContact b)
        {
            return b.Score.CompareTo(a.Score);
        }

        static int CompareCluster(Cluster a, Cluster b)
        {
            return b.Score.CompareTo(a.Score);
        }

        class Cluster
        {
            public EMBands Band;
            public Vector3 SeedDirection;
            public List<ThreatContact> Members;
            public ThreatContact Core;
            public float Score;

            public void Finalize(Vector3 selfPosition)
            {
                if (Members == null || Members.Count == 0)
                {
                    return;
                }

                Vector3 centroid = Vector3.zero;
                for (int i = 0; i < Members.Count; i++)
                {
                    centroid += Members[i].Position;
                }

                centroid /= Members.Count;
                ThreatContact nearest = Members[0];
                float nearestDistance = (nearest.Position - centroid).sqrMagnitude;
                for (int i = 1; i < Members.Count; i++)
                {
                    float distance = (Members[i].Position - centroid).sqrMagnitude;
                    if (distance < nearestDistance)
                    {
                        nearest = Members[i];
                        nearestDistance = distance;
                    }
                }

                Core = nearest;
                Vector3 direction = Core.Position - selfPosition;
                if (direction.sqrMagnitude > 1f)
                {
                    SeedDirection = direction.normalized;
                }
            }
        }

        class PlanItem
        {
            public ChannelSlot Slot;
            public ThreatContact Threat;
            public RadarJammer.TransmitModes Mode;
        }
    }
}
