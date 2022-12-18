using HarmonyLib;

namespace EliminatorKaedeMP
{
    public static class FieldAccessor
    {
        public static T AFGet<T>(this object obj, string field) =>
            (T)AccessTools.Field(obj.GetType(), field).GetValue(obj);

        public static void AFSet(this object obj, string field, object value) =>
            AccessTools.Field(obj.GetType(), field).SetValue(obj, value);
    }
}
