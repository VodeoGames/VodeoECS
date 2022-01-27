using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;

namespace VodeoECS
{
    /// <summary>
    /// An interface for loader classes that can be used as a fallback loader in a Registry class.
    /// </summary>
    /// <typeparam name="T">The type loaded by the loader.</typeparam>
    public interface IRegistryLoader<T>
    {
        public T Load ( string name );
    }
}