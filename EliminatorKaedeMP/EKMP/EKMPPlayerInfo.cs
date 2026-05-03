using System.IO;

namespace EliminatorKaedeMP
{
	public class EKMPPlayerInfo
	{
		public uint ID;
		public string Name;
		public byte CharacterID;
		public EKMPPlayerClothInfo Cloth;

		public void Write(BinaryWriter writer, bool isS2C)
		{
			if (isS2C)
				writer.Write(ID);
			writer.Write(Name);
			writer.Write(CharacterID);
			Cloth.Write(writer);
		}

		public static EKMPPlayerInfo Read(BinaryReader reader, bool isS2C)
		{
			EKMPPlayerInfo data = new EKMPPlayerInfo();

			if (isS2C)
				data.ID = reader.ReadUInt32();
			data.Name = reader.ReadString();
			data.CharacterID = reader.ReadByte();
			data.Cloth = EKMPPlayerClothInfo.Read(reader);

			return data;
		}
	}
}
