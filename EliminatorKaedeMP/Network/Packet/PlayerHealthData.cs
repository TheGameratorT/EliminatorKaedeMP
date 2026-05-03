using System.IO;

namespace EliminatorKaedeMP
{
    // 5Hz broadcast of a player's health/status values.
    // Sent by each player about themselves; server relays to all others.
    public class PlayerHealthData
    {
        public float Health;
        public float Urine;
        public float Feces;
        public float Sick;

        public void Write(BinaryWriter writer)
        {
            writer.Write(Health);
            writer.Write(Urine);
            writer.Write(Feces);
            writer.Write(Sick);
        }

        public static PlayerHealthData Read(BinaryReader reader)
        {
            PlayerHealthData d = new PlayerHealthData();
            d.Health = reader.ReadSingle();
            d.Urine  = reader.ReadSingle();
            d.Feces  = reader.ReadSingle();
            d.Sick   = reader.ReadSingle();
            return d;
        }
    }
}
