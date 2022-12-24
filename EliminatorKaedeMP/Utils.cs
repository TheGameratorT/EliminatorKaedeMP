using K_PlayerControl;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine.SceneManagement;

namespace EliminatorKaedeMP
{
	public class Utils
	{
		public static int ReadInt(byte[] bytes, int offset)
		{
			return
				bytes[offset] |
				(bytes[offset + 1] << 8) |
				(bytes[offset + 2] << 16) |
				(bytes[offset + 3] << 24);
		}

		public static void WriteInt(byte[] bytes, int offset, int value)
		{
			bytes[offset] = (byte)value;
			bytes[1 + offset] = (byte)(value >> 8);
			bytes[2 + offset] = (byte)(value >> 16);
			bytes[3 + offset] = (byte)(value >> 24);
		}

		public static void Serialize(BinaryWriter writer, object obj)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				new BinaryFormatter().Serialize(stream, obj);
				writer.Write(stream.ToArray());
			}
		}

		public static object Deserialize(MemoryStream stream)
		{
			return new BinaryFormatter().Deserialize(stream);
		}

		public static int GetCurrentScene()
		{
			return SceneManager.GetActiveScene().buildIndex;
		}

		// Returns true if the player is in Dam or Mission scene
		public static bool IsInGame()
		{
			int sceneID = GetCurrentScene();
			return sceneID == SceneID.Dam || sceneID == SceneID.DebugStage;
		}

		// Returns the player name in the config
		public static string GetPlayerName()
		{
			return PlayerPref.instance.c_PlayerName;
		}
	}
}
