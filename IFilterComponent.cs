using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace VodeoECS
{
    /// <summary>
    /// Interface for Filter Components. All IFilterComponent<T> interfaces implement this. 
    /// </summary>
    public interface IFilterComponent : IComponent { }

    /// <summary>
    /// Interface for Filter Components of a specific type. All Filter Component Types must implement this.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IFilterComponent<T> : IFilterComponent, IEquatable<T> where T : IFilterComponent<T> { }
}