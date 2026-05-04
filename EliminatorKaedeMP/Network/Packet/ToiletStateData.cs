using System.IO;

namespace EliminatorKaedeMP
{
    // Toilet event state synchronization packet.
    // Sends the current toilet state along with dynamic object references (paths).
    public class ToiletStateData
    {
        public int ToiletType;           // ToiletEventManager.Type cast to int
        public int ToiletState;          // ToiletEventManager.State cast to int
        public string ToiletObjectPath;  // Relative path to toilet object (can be empty)
        public string StartPointPath;    // Relative path to start point (can be empty)

        public void Write(BinaryWriter writer)
        {
            writer.Write(ToiletType);
            writer.Write(ToiletState);
            writer.Write(ToiletObjectPath ?? "");
            writer.Write(StartPointPath ?? "");
        }

        public static ToiletStateData Read(BinaryReader reader)
        {
            ToiletStateData d = new ToiletStateData();
            d.ToiletType = reader.ReadInt32();
            d.ToiletState = reader.ReadInt32();
            d.ToiletObjectPath = reader.ReadString();
            d.StartPointPath = reader.ReadString();
            return d;
        }
    }
}
