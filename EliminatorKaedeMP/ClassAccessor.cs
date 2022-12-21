using HarmonyLib;
using System;
using System.Reflection;

namespace EliminatorKaedeMP
{
	public static class ClassAccessor
	{
		public static T AFGet<T>(this object obj, string field) =>
			(T)AccessTools.Field(obj.GetType(), field).GetValue(obj);

		public static void AFSet(this object obj, string field, object value) =>
			AccessTools.Field(obj.GetType(), field).SetValue(obj, value);

		public static MethodInfo AMCall(this object obj, string name, Type[] parameters = null, Type[] generics = null) =>
			AccessTools.Method(obj.GetType(), name, parameters, generics);
	}
}
