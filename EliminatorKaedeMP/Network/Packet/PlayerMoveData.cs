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
    }
}
