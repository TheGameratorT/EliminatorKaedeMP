using System.IO;

namespace EliminatorKaedeMP
{
	public class EKMPPlayerInfo
	{
		public uint ID;
		public string Name;
		public byte CharacterID;

		public void Write(BinaryWriter writer, bool isS2C)
		{
			if (isS2C)
				writer.Write(ID);
			writer.Write(Name);
			writer.Write(CharacterID);
		}

		public static EKMPPlayerInfo Read(BinaryReader reader, bool isS2C)
		{
			EKMPPlayerInfo data = new EKMPPlayerInfo();
			if (isS2C)
				data.ID = reader.ReadUInt32();
			data.Name = reader.ReadString();
			data.CharacterID = reader.ReadByte();
			return data;
		}
	}
}
