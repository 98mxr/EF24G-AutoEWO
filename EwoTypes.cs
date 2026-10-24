using System.Text;
using UnityEngine;

namespace EF24GAutoEWO
{
    public enum EwoMode
    {
        Off = 0,
        Defensive = 1,
        Offensive = 2
    }

    public enum EwoPriority
    {
        Balanced = 0,
        Sam = 1,
        Air = 2
    }

    public enum ArmLaunchMode
    {
        SemiAuto = 0,
        FullAuto = 1
    }

    public static class EwoTune
    {
        public const float FastSeconds = 0.1f;
        public const float MediumSeconds = 0.5f;
        public const float SlowSeconds = 1f;

        // Radar, LockingRadar, RadarJammer and OpticalTargeter.CheckOcclusion all use this mask.
        public const int TerrainMask = 1;
        public const float OcclusionSlopSqr = 25f;
        public const float GpsMatchMeters = 150f;
        public const float TgpAlignDegrees = 3f;
        public const float PlayerSlewGrace = 4f;
        public const float OpenEllipseConfidence = 0.55f;
        public const int MaxTerrainTests = 24;
        public const float PlayerStopGrace = 0.8f;
        public const float MaldNeighborMeters = 8000f;
    }

    public static class EwoLog
    {
        public static void Info(string message)
        {
            Debug.Log("[EF24G-AutoEWO] " + message);
        }

        public static void Warn(string message)
        {
            Debug.LogWarning("[EF24G-AutoEWO] " + message);
        }
    }

    public static class EwoUtil
    {
        public static Actor RootUnit(Actor actor)
        {
            if (!actor)
            {
                return null;
            }

            int guard = 8;
            while (actor.parentActor && (actor.role & Actor.Roles.Missile) == 0 && guard-- > 0)
            {
                actor = actor.parentActor;
            }

            return actor;
        }

        public static bool SameUnit(Actor a, Actor b)
        {
            if (!a || !b)
            {
                return false;
            }

            if (a == b)
            {
                return true;
            }

            return RootUnit(a) == RootUnit(b);
        }

        public static bool IsHostile(Actor self, Actor other)
        {
            if (!self || !other)
            {
                return false;
            }

            Actor unit = other;
            if ((unit.role & Actor.Roles.Missile) != 0 && unit.parentActor)
            {
                unit = unit.parentActor;
            }

            return unit.team != self.team;
        }

        public static bool IsSam(Actor actor)
        {
            Actor unit = RootUnit(actor);
            if (!unit)
            {
                return false;
            }

            return (unit.role & Actor.Roles.Ground) != 0 || (unit.role & Actor.Roles.GroundArmor) != 0;
        }

        public static bool IsAir(Actor actor)
        {
            Actor unit = RootUnit(actor);
            return unit && (unit.role & Actor.Roles.Air) != 0;
        }

        public static bool BandAllowed(EwoSettings settings, EMBands band)
        {
            if (settings == null)
            {
                return false;
            }

            if (band == EMBands.Low)
            {
                return settings.AllowLowBand;
            }

            if (band == EMBands.Mid)
            {
                return settings.AllowMidBand;
            }

            if (band == EMBands.High)
            {
                return settings.AllowHighBand;
            }

            return false;
        }

        public static string BandName(EMBands band)
        {
            if (band == EMBands.Low)
            {
                return "Low";
            }

            if (band == EMBands.Mid)
            {
                return "Mid";
            }

            if (band == EMBands.High)
            {
                return "High";
            }

            return band.ToString();
        }

        public static string ModeName(EwoMode mode)
        {
            switch (mode)
            {
                case EwoMode.Defensive:
                    return "DEFENSIVE";
                case EwoMode.Offensive:
                    return "OFFENSIVE";
                default:
                    return "OFF";
            }
        }

        public static bool TerrainBlocks(Vector3 from, Vector3 to, Actor ignore, Actor self)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 40f)
            {
                return false;
            }

            Vector3 start = from + delta / distance * 20f;
            RaycastHit hit;
            if (!Physics.Linecast(start, to, out hit, EwoTune.TerrainMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if ((hit.point - to).sqrMagnitude <= EwoTune.OcclusionSlopSqr)
            {
                return false;
            }

            if (hit.collider)
            {
                Actor hitActor = hit.collider.GetComponentInParent<Actor>();
                if (hitActor && (SameUnit(hitActor, ignore) || SameUnit(hitActor, self)))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool TryOrthogonalHeading(Vector3 selfPosition, Vector3 selfForward, Vector3 targetPosition, out float bearing, out float recommended)
        {
            bearing = 0f;
            recommended = 0f;
            Vector3 flatTarget = Vector3.ProjectOnPlane(targetPosition - selfPosition, Vector3.up);
            Vector3 flatNose = Vector3.ProjectOnPlane(selfForward, Vector3.up);
            if (flatTarget.sqrMagnitude < 100f || flatNose.sqrMagnitude < 0.01f)
            {
                return false;
            }

            bearing = Compass(flatTarget);
            float nose = Compass(flatNose);
            float left = Repeat360(bearing - 90f);
            float right = Repeat360(bearing + 90f);
            recommended = Mathf.Abs(Mathf.DeltaAngle(nose, left)) <= Mathf.Abs(Mathf.DeltaAngle(nose, right)) ? left : right;
            return true;
        }

        public static float Compass(Vector3 flatDirection)
        {
            float degrees = Mathf.Atan2(flatDirection.x, flatDirection.z) * Mathf.Rad2Deg;
            return Repeat360(degrees);
        }

        public static float Repeat360(float degrees)
        {
            degrees %= 360f;
            if (degrees < 0f)
            {
                degrees += 360f;
            }

            return degrees;
        }

        public static string ActorLabel(Actor actor)
        {
            if (!actor)
            {
                return "无";
            }

            if (!string.IsNullOrEmpty(actor.actorName))
            {
                return actor.actorName;
            }

            return actor.name;
        }

        public static void AppendKm(StringBuilder builder, float meters)
        {
            builder.Append((meters / 1000f).ToString("0.0"));
            builder.Append("km");
        }
    }
}
