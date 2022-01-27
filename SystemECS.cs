using System;
using System.Collections.Generic;
using UnityEngine;

namespace VodeoECS
{
    /// <summary>
    /// Abstract class for ECS Systems. All ECS Systems must inherit from this.
    /// </summary>
    public abstract class SystemECS : IDisposable
    {
        /// <summary>
        /// Initialize the System.
        /// </summary>
        public abstract void Initialize ( );
        /// <summary>
        /// Process any outstanding the System is listening to here.
        /// </summary>
        public abstract void ProcessEvents ( );
        /// <summary>
        /// Dispose the System.
        /// </summary>
        public abstract void Dispose ( );
    }
}