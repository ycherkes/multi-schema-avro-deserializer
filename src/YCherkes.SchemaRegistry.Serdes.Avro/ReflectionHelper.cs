using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace YCherkes.SchemaRegistry.Serdes.Avro
{
    internal static class ReflectionHelper
    {
        internal static IEnumerable<Assembly> GetAssemblies()
        {
            var loadedAssemblies = AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(x => !string.IsNullOrEmpty(x.FullName))
                .GroupBy(x => x.FullName)
                .ToDictionary(x => x.Key, x => x.First());

            var missingAssemblies = new HashSet<string>();

            var notLoadedAssemblyReferences = Assembly.GetEntryAssembly()
                ?.GetReferencedAssemblies()
                .Where(a => !string.IsNullOrEmpty(a.FullName) && !loadedAssemblies.ContainsKey(a.FullName))
                .ToArray()
                ?? Array.Empty<AssemblyName>();

            foreach (var reference in notLoadedAssemblyReferences)
            {
                if (loadedAssemblies.ContainsKey(reference.FullName) ||
                    missingAssemblies.Contains(reference.FullName))
                {
                    continue;
                }

                try
                {
                    var assembly = Assembly.Load(reference);
                    loadedAssemblies.Add(reference.FullName, assembly);
                }
                catch
                {
                    missingAssemblies.Add(reference.FullName);
                }
            }

            return loadedAssemblies.Values;
        }
    }
}
