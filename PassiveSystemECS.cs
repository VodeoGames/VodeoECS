using System;
using System.Collections.Generic;
using UnityEngine;

namespace VodeoECS
{
    /// <summary>
    /// Passive Systems only respond to Events and are not updated otherwise.
    /// </summary>
    public abstract class PassiveSystemECS : SystemECS
    {
        /// <summary>
        /// Construct a Passive System.
        /// </summary>
        /// <param name="world">The World object.</param>
        public PassiveSystemECS ( World world )
        {
            world.Systems.RegisterSystem( this );
        }
    }
}