using System.IO;
using UnityEngine;

namespace EliminatorKaedeMP
{
	public class PlayerMoveData
	{
		public Vector3 PlayerPos;
		public Quaternion PlayerRot;

		public bool InputH;
		public bool InputV;
		public float FloatH;
		public float FloatV;
		//public Vector3 cameraPos;
		//public Quaternion cameraRot;

		public void Write(BinaryWriter writer)
		{
			writer.Write(PlayerPos);
			writer.Write(PlayerRot);
			writer.Write(InputH);
			writer.Write(InputV);
			writer.Write(FloatH);
			writer.Write(FloatV);
		}

		public static PlayerMoveData Read(BinaryReader reader)
		{
			PlayerMoveData moveData = new PlayerMoveData();
			moveData.PlayerPos = reader.ReadVector3();
			moveData.PlayerRot = reader.ReadQuaternion();
			moveData.InputH = reader.ReadBoolean();
			moveData.InputV = reader.ReadBoolean();
			moveData.FloatH = reader.ReadSingle();
			moveData.FloatV = reader.ReadSingle();
			return moveData;
		}
	}
}
