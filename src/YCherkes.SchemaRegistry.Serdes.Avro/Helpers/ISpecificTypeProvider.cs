using System;
using System.Collections.Generic;

namespace YCherkes.SchemaRegistry.Serdes.Avro.Helpers
{
    public interface ISpecificTypeProvider
    {
        IEnumerable<Type> GetSpecificTypes();
    }
}