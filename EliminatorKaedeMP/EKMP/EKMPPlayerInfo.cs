using System.IO;
using UnityEngine;

namespace EliminatorKaedeMP
{
	public class EKMPPlayerInfo
	{
		public uint ID;
		public string Name;
		public byte CharacterID;
		public byte ClothID;

		public int S_underHair;
		public float S_underHair_alpha;
		public float S_underHair_density;
		public int S_HairStyle;
		public float S_HIYAKE_kosa;
		public int S_HIYAKE_patan;
		public Color[] S_MatColor;

		public void Write(BinaryWriter writer, bool isS2C)
		{
			if (isS2C)
				writer.Write(ID);
			writer.Write(Name);
			writer.Write(CharacterID);
			writer.Write(ClothID);

			writer.Write(S_underHair);
			writer.Write(S_underHair_alpha);
			writer.Write(S_underHair_density);
			writer.Write(S_HairStyle);
			writer.Write(S_HIYAKE_kosa);
			writer.Write(S_HIYAKE_patan);

			for (int i = 0; i < 10; i++)
				writer.Write(S_MatColor[i]);
		}

		public static EKMPPlayerInfo Read(BinaryReader reader, bool isS2C)
		{
			EKMPPlayerInfo data = new EKMPPlayerInfo();

			if (isS2C)
				data.ID = reader.ReadUInt32();
			data.Name = reader.ReadString();
			data.CharacterID = reader.ReadByte();
			data.ClothID = reader.ReadByte();

			data.S_underHair = reader.ReadInt32();
			data.S_underHair_alpha = reader.ReadSingle();
			data.S_underHair_density = reader.ReadSingle();
			data.S_HairStyle = reader.ReadInt32();
			data.S_HIYAKE_kosa = reader.ReadSingle();
			data.S_HIYAKE_patan = reader.ReadInt32();

			data.S_MatColor = new Color[10];
			for (int i = 0; i < 10; i++)
				data.S_MatColor[i] = reader.ReadColor();

			return data;
		}
	}
}
