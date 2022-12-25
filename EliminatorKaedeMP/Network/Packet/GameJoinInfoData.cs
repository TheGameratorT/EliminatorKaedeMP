using System.IO;

namespace EliminatorKaedeMP
{
    public class GameJoinInfoData
    {
        // The ID of our player
        public uint PlayerID;

        // The current scene
        public byte SceneID;

        // The information about the other players
        public EKMPPlayerInfo[] PlayerInfos;

		public void Write(BinaryWriter writer)
		{
			writer.Write(PlayerID);
			writer.Write(SceneID);
			writer.Write(PlayerInfos.Length);
			foreach (EKMPPlayerInfo playerInfo in PlayerInfos)
				playerInfo.Write(writer, true);
		}

		public static GameJoinInfoData Read(BinaryReader reader)
        {
			GameJoinInfoData info = new GameJoinInfoData();
			info.PlayerID = reader.ReadUInt32();
			info.SceneID = reader.ReadByte();
            int playerInfoCount = reader.ReadInt32();
			info.PlayerInfos = new EKMPPlayerInfo[playerInfoCount];
			for (int i = 0; i < playerInfoCount; i++)
                info.PlayerInfos[i] = EKMPPlayerInfo.Read(reader, true);
			return info;
		}
    }
}
