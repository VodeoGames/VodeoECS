using System;
using System.Collections.Generic;
using UnityEngine;

namespace VodeoECS
{
    /// <summary>
    /// FrameSystems are updated everytime a graphical frame needs to be rendered.
    /// </summary>
    public abstract class FrameSystemECS : SystemECS
    {
        /// <summary>
        /// Construct a Frame System.
        /// </summary>
        /// <param name="world">The World object.</param>
        public FrameSystemECS ( World world )
        {
            world.Systems.RegisterSystem( this );
        }
        /// <summary>
        /// Prepare frame for rendering.
        /// </summary>
        /// <param name="t">The time at which the frame is to be rendered.</param>
        public abstract void UpdateFrame ( float t );
        /// <summary>
        /// Complete frame rendering.
        /// </summary>
        public abstract void CompleteUpdate ( );
    }
}