using System;

namespace TrRebootTools.Shared.Serialization
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ArrayAttribute : Attribute
    {
        public ArrayAttribute(int length)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class ListAttribute : Attribute
    {
        public ListAttribute(string countField)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class PaddingAttribute : Attribute
    {
        public PaddingAttribute(int length)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class UnionAttribute : Attribute
    {
        public UnionAttribute(string selectorField)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class UnionMemberAttribute : Attribute
    {
        public UnionMemberAttribute(object selectorValue)
        {
        }
    }
}
