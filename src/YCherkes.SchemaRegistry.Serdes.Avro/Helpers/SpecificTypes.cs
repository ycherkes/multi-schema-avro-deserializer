using System;
using System.Collections.Generic;
using System.Reflection;

namespace YCherkes.SchemaRegistry.Serdes.Avro.Helpers
{
    public class SpecificTypes : ISpecificTypeProvider
    {
        private readonly Func<IEnumerable<Type>> _getTypesFunc;

        public SpecificTypes(IEnumerable<Assembly> assemblies)
        {
            _getTypesFunc = () => ReflectionHelper.GetSpecificTypes(assemblies);
        }

        public SpecificTypes(IEnumerable<Type> types)
        {
            _getTypesFunc = () => types;
        }

        public IEnumerable<Type> GetSpecificTypes()
        {
            return _getTypesFunc();
        }

        public static SpecificTypes FromAssembly(Assembly assembly) => new SpecificTypes(new[] { assembly ?? throw new ArgumentNullException(nameof(assembly))});
        public static SpecificTypes FromAssemblies(params Assembly[] assemblies) => new SpecificTypes(assemblies ?? throw new ArgumentNullException(nameof(assemblies)));
        public static SpecificTypes FromAssemblyContainingType(Type type) => FromAssembly(type?.Assembly ?? throw new ArgumentNullException(nameof(type)));
        public static SpecificTypes FromTypes(params Type[] types) => new SpecificTypes(types ?? throw new ArgumentNullException(nameof(types)));
    }
}
