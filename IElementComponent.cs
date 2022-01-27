using VodeoECS.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace VodeoECS
{
    /// <summary>
    /// Interface for List Components. All List Component Element Types must implement this.
    /// </summary>
    public interface IElementComponent : IComponent { }
}