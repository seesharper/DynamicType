using System.Collections.Generic;
using DynamicType;

namespace DynamicType.Tests
{

    /// <summary>
    /// Code to be emitted to create a new instance of a dynamic type.
    /// </summary>
    public class DynamicTypeActivator
    {
        public static void ExtractValues(ObjectWithPropertiesAndFields simpleDynamicType, List<DynamicMemberInfo> members)
        {
            members.Add(new DynamicMemberInfo<int>("Id", simpleDynamicType.Id));
            members.Add(new DynamicMemberInfo<string>("Name", simpleDynamicType.Name));
        }

        public static IDynamicType CreateInstance(DynamicMemberInfo[] dynamicMembers)
        {
            return new SimpleDynamicType(((DynamicMemberInfo<int>)dynamicMembers[0]).Value, ((DynamicMemberInfo<string>)dynamicMembers[1]).Value);
        }
    }

    /// <summary>
    /// Code be emitted to create a new dynamic type.
    /// </summary>
    public class SimpleDynamicType : IDynamicType
    {
        public SimpleDynamicType(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public int Id { get; set; }

        public string Name { get; set; }
    }


}