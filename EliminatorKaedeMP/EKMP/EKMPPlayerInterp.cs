using UnityEngine;

namespace EliminatorKaedeMP
{
    // Snapshot interpolation for remote players.
    //
    // Strategy: buffer the last N state snapshots, each stamped with the local
    // realtime when it was received on the main thread.  Each frame we sample
    // the buffer at (now - InterpDelay), finding the two snapshots that bracket
    // that target time and linearly interpolating between them.
    //
    // At 30 Hz sends and 100 ms delay we always have ~3 buffered snapshots, so
    // interpolation is smooth.  Under temporary packet loss the fallback is to
    // hold the last known state rather than teleporting.
    public partial class EKMPPlayer
    {
        // How far behind real-time we render remote players.
        // 100 ms gives a comfortable interpolation window even at 20 Hz.
        private const float InterpDelay = 0.10f;

        private struct StateSnapshot
        {
            public float      Time;         // Time.realtimeSinceStartup when added
            public Vector3    Position;
            public Quaternion Rotation;
            public Vector3    Velocity;
            public Vector3    CameraForward;
            public float      FloatH;
            public float      FloatV;
            public int        PlayerStateID;
            public int        FlyStateID;
            public byte       AnimFlags;
            public float      Sick;

            public bool IsGrounded  => (AnimFlags & 0x01) != 0;
            public bool IsAiming    => (AnimFlags & 0x02) != 0;
            public bool IsCrouching => (AnimFlags & 0x04) != 0;
        }

        private const int SnapshotBufferSize = 12;
        private StateSnapshot[] snapshots = new StateSnapshot[SnapshotBufferSize];
        private int snapshotCount = 0;
        private int snapshotHead  = 0;   // points at the NEXT slot to write

        // Called on the main thread whenever a PlayerStateData arrives.
        private void AddSnapshot(PlayerStateData data)
        {
            StateSnapshot s;
            s.Time           = Time.realtimeSinceStartup;
            s.Position       = data.Position;
            s.Rotation       = data.Rotation;
            s.Velocity       = data.Velocity;
            s.CameraForward  = data.CameraForward;
            s.FloatH         = data.FloatH;
            s.FloatV         = data.FloatV;
            s.PlayerStateID  = data.PlayerStateID;
            s.FlyStateID     = data.FlyStateID;
            s.AnimFlags      = data.AnimFlags;
            s.Sick           = data.Sick;

            snapshots[snapshotHead] = s;
            snapshotHead = (snapshotHead + 1) % SnapshotBufferSize;
            if (snapshotCount < SnapshotBufferSize)
                snapshotCount++;
        }

        // Get the snapshot at logical index i (0 = oldest, count-1 = newest).
        private StateSnapshot GetSnapshot(int i)
        {
            // head points at the next-write slot, so the oldest is at head when full,
            // or at (head - count + SnapshotBufferSize) % SnapshotBufferSize.
            int start = (snapshotHead - snapshotCount + SnapshotBufferSize) % SnapshotBufferSize;
            return snapshots[(start + i) % SnapshotBufferSize];
        }

        // Returns true and fills 'out' with the interpolated state for the remote
        // player this frame.  Returns false if there are no snapshots yet.
        private bool TryGetInterpolatedState(out StateSnapshot result)
        {
            result = default(StateSnapshot);

            if (snapshotCount == 0)
                return false;

            float renderTime = Time.realtimeSinceStartup - InterpDelay;

            StateSnapshot newest = GetSnapshot(snapshotCount - 1);
            StateSnapshot oldest = GetSnapshot(0);

            // Not enough history yet: just use the oldest we have.
            if (renderTime <= oldest.Time)
            {
                result = oldest;
                return true;
            }

            // Render time is ahead of all snapshots (high latency / packet loss):
            // hold at the newest known position.
            if (renderTime >= newest.Time)
            {
                result = newest;
                return true;
            }

            // Find the two snapshots that bracket renderTime.
            StateSnapshot a = oldest;
            StateSnapshot b = newest;
            for (int i = 0; i < snapshotCount - 1; i++)
            {
                StateSnapshot sa = GetSnapshot(i);
                StateSnapshot sb = GetSnapshot(i + 1);
                if (renderTime >= sa.Time && renderTime <= sb.Time)
                {
                    a = sa;
                    b = sb;
                    break;
                }
            }

            float span = b.Time - a.Time;
            float t    = (span > 0.0001f) ? (renderTime - a.Time) / span : 1f;

            result.Time          = renderTime;
            result.Position      = Vector3.Lerp(a.Position, b.Position, t);
            result.Rotation      = Quaternion.Slerp(a.Rotation, b.Rotation, t);
            result.Velocity      = Vector3.Lerp(a.Velocity, b.Velocity, t);
            result.CameraForward = Vector3.Slerp(a.CameraForward, b.CameraForward, t);
            result.FloatH        = Mathf.Lerp(a.FloatH, b.FloatH, t);
            result.FloatV        = Mathf.Lerp(a.FloatV, b.FloatV, t);
            result.Sick          = Mathf.Lerp(a.Sick, b.Sick, t);
            // Discrete states: snap at midpoint to avoid applying partial transitions.
            result.PlayerStateID  = t < 0.5f ? a.PlayerStateID  : b.PlayerStateID;
            result.FlyStateID     = t < 0.5f ? a.FlyStateID     : b.FlyStateID;
            result.AnimFlags      = t < 0.5f ? a.AnimFlags       : b.AnimFlags;
            return true;
        }
    }
}
