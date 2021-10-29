using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avro.Specific;

namespace YCherkes.SchemaRegistry.Serdes.Avro.Helpers
{
    internal static class ReflectionHelper
    {
        internal static IEnumerable<Type> GetSpecificTypes(IEnumerable<Assembly> assemblies)
        {
            return assemblies
                  .SelectMany(a => a.GetTypes())
                  .Where(IsSpecificType);
        }

        internal static bool IsSpecificType(Type type)
        {
            return type.IsClass && 
                !type.IsAbstract && 
                typeof(ISpecificRecord).IsAssignableFrom(type) && 
                GetSchema(type) != null;
        }

        internal static global::Avro.Schema GetSchema(IReflect type)
        {
            return (global::Avro.Schema)type.GetField("_SCHEMA", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        }
    }
}
