﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

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
            bytes[offset] = (byte) value;
            bytes[1 + offset] = (byte) (value >> 8);
            bytes[2 + offset] = (byte) (value >> 16);
            bytes[3 + offset] = (byte) (value >> 24);
        }

        public static byte[] SerializePacket(S2CPacket packetID, object obj)
        {
            byte[] result;

            using MemoryStream stream = new MemoryStream();
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(stream, obj);
            byte[] serialized = stream.ToArray();

            result = new byte[4 + serialized.Length];

            WriteInt(result, 0, (int) packetID);
            Array.Copy(serialized, 0, result, 4, serialized.Length);

            return result;
        }

        public static object DeserializePacket(MemoryStream stream)
        {
            stream.Position = 4;
            BinaryFormatter formatter = new BinaryFormatter();
            return formatter.Deserialize(stream);
        }

		public static void IsInGame()
        {

        }
    }
}
