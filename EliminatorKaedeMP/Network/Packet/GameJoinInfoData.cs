using System;
using System.Collections.Generic;

namespace EliminatorKaedeMP
{
    [Serializable]
    public struct GameJoinInfoData
    {
        // The ID of our player
        public uint PlayerID;

        // The current scene
        public byte SceneID;

        // The information about the other players
        public List<PlayerInfoData> PlayerInfos;
    }
}
