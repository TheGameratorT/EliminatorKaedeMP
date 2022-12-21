using System;
using UnityEngine;

namespace EliminatorKaedeMP
{
    [Serializable]
    public struct PlayerMoveData
    {
        public float PlayerPosX;
        public float PlayerPosY;
        public float PlayerPosZ;

        public float PlayerRotX;
        public float PlayerRotY;
        public float PlayerRotZ;
        public float PlayerRotW;
        //public Vector3 cameraPos;
        //public Quaternion cameraRot;

        public void SetPlayerPos(Vector3 pos)
        {
            PlayerPosX = pos.x;
            PlayerPosY = pos.y;
            PlayerPosZ = pos.z;
        }

        public Vector3 GetPlayerPos()
        {
            return new Vector3(PlayerPosX, PlayerPosY, PlayerPosZ);
        }

        public void SetPlayerRot(Quaternion rot)
        {
            PlayerRotX = rot.x;
            PlayerRotY = rot.y;
            PlayerRotZ = rot.z;
            PlayerRotW = rot.w;
        }

        public Quaternion GetPlayerRot()
        {
            return new Quaternion(PlayerRotX, PlayerRotY, PlayerRotZ, PlayerRotW);
        }
    }
}
