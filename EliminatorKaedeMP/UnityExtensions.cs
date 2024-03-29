﻿using System.IO;
using UnityEngine;

namespace EliminatorKaedeMP
{
	public static class UnityExtensions
	{
		public static void Write(this BinaryWriter writer, Vector3 vector)
		{
			writer.Write(vector.x);
			writer.Write(vector.y);
			writer.Write(vector.z);
		}

		public static void Write(this BinaryWriter writer, Quaternion quaternion)
		{
			writer.Write(quaternion.x);
			writer.Write(quaternion.y);
			writer.Write(quaternion.z);
			writer.Write(quaternion.w);
		}

		public static void Write(this BinaryWriter writer, Color color)
		{
			Plugin.Log(color);
			writer.Write((byte)(255.0f * color.r));
			writer.Write((byte)(255.0f * color.g));
			writer.Write((byte)(255.0f * color.b));
			writer.Write((byte)(255.0f * color.a));
		}

		public static Vector3 ReadVector3(this BinaryReader reader)
		{
			Vector3 vector;
			vector.x = reader.ReadSingle();
			vector.y = reader.ReadSingle();
			vector.z = reader.ReadSingle();
			return vector;
		}

		public static Quaternion ReadQuaternion(this BinaryReader reader)
		{
			Quaternion quaternion;
			quaternion.x = reader.ReadSingle();
			quaternion.y = reader.ReadSingle();
			quaternion.z = reader.ReadSingle();
			quaternion.w = reader.ReadSingle();
			return quaternion;
		}

		public static Color ReadColor(this BinaryReader reader)
		{
			Color color;
			color.r = reader.ReadByte() / 255.0f;
			color.g = reader.ReadByte() / 255.0f;
			color.b = reader.ReadByte() / 255.0f;
			color.a = reader.ReadByte() / 255.0f;
			return color;
		}
	}
}
