using System.IO;
using UnityEngine;

namespace EliminatorKaedeMP
{
    // 30Hz snapshot of a player's full movement + game-state context.
    // Replaces the old PlayerMoveData which was missing velocity, camera direction,
    // PlayerState, FlyState, and the sick parameter.
    public class PlayerStateData
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;        // rigidbody velocity – used for extrapolation
        public Vector3 CameraForward;   // world-space aim direction for IK targeting
        public float FloatH;
        public float FloatV;
        public int PlayerStateID;       // PlayerControl.State cast to int
        public int FlyStateID;          // PlayerControl.Fly cast to int
        public byte AnimFlags;          // bitmask: bit0=grounded, bit1=aiming, bit2=crouching
        public float Sick;              // drives Sick/Sick_Speed animator params
        public byte  ToiletTypeID;      // ToiletEventManager.Type; 0 if not in toilet event
        public int   ToiletAnimHash;    // EventMotions layer fullPathHash; 0 if not in toilet
        public float ToiletAnimTime;    // normalized time (0-1) in that state

        public bool IsGrounded  => (AnimFlags & 0x01) != 0;
        public bool IsAiming    => (AnimFlags & 0x02) != 0;
        public bool IsCrouching => (AnimFlags & 0x04) != 0;

        public static byte BuildAnimFlags(bool grounded, bool aiming, bool crouching)
        {
            byte f = 0;
            if (grounded)  f |= 0x01;
            if (aiming)    f |= 0x02;
            if (crouching) f |= 0x04;
            return f;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Position);
            writer.Write(Rotation);
            writer.Write(Velocity);
            writer.Write(CameraForward);
            writer.Write(FloatH);
            writer.Write(FloatV);
            writer.Write(PlayerStateID);
            writer.Write(FlyStateID);
            writer.Write(AnimFlags);
            writer.Write(Sick);
            writer.Write(ToiletTypeID);
            writer.Write(ToiletAnimHash);
            writer.Write(ToiletAnimTime);
        }

        public static PlayerStateData Read(BinaryReader reader)
        {
            PlayerStateData d = new PlayerStateData();
            d.Position       = reader.ReadVector3();
            d.Rotation       = reader.ReadQuaternion();
            d.Velocity       = reader.ReadVector3();
            d.CameraForward  = reader.ReadVector3();
            d.FloatH         = reader.ReadSingle();
            d.FloatV         = reader.ReadSingle();
            d.PlayerStateID  = reader.ReadInt32();
            d.FlyStateID     = reader.ReadInt32();
            d.AnimFlags      = reader.ReadByte();
            d.Sick           = reader.ReadSingle();
            d.ToiletTypeID   = reader.ReadByte();
            d.ToiletAnimHash = reader.ReadInt32();
            d.ToiletAnimTime = reader.ReadSingle();
            return d;
        }
    }
}
