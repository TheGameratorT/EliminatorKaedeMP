using System;

namespace EliminatorKaedeMP
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class PatchAttr : Attribute
    {
        public enum EPatchType
        {
            Prefix,
            Postfix
        }

        public Type TargetClass { get { return targetClass; } }
        public string TargetMethod { get { return targetMethod; } }
        public EPatchType PatchType { get { return patchType; } }

        private Type targetClass;
        private string targetMethod;
        private EPatchType patchType;
  
        public PatchAttr(Type targetClass, string targetMethod, EPatchType patchType)
        {
            this.targetClass = targetClass;
            this.targetMethod = targetMethod;
            this.patchType = patchType;
        }
    }
}
