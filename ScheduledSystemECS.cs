using System;
using System.Collections.Generic;
using UnityEngine;

namespace VodeoECS
{
    public abstract class ScheduledSystemECS : SystemECS
    {
        /// <summary>
        /// Get the Schedule Queue corresponding to this Scheduled System.
        /// </summary>
        public ScheduleQueue ScheduleQueue { get; }
        /// <summary>
        /// For internal use by the System Scheduler.
        /// </summary>
        public List<ScheduledSystemECS> Dependencies { get; }
     
        private NativePriorityQueue<Entity> queue;
        /// <summary>
        /// Construct a Scheduled System.
        /// </summary>
        /// <param name="world">The World object.</param>
        /// <param name="queueName">The name of this System's Schedule Queue.</param>
        public ScheduledSystemECS ( World world, string queueName )
        {
            world.Systems.RegisterSystem( this );
            queue = world.ScheduleQueues.CreateQueue( queueName, this );
            this.ScheduleQueue = new ScheduleQueue( queue );
            this.Dependencies = new List<ScheduledSystemECS>( );
        }

        /// <summary>
        /// Update this system up to the given time.
        /// </summary>
        /// <param name="time">The time that the system needs to be updated up to.</param>
        public void UpdateTo ( float time )
        {
            float t;
            int n = 0;
            while ( ( t = ScheduleQueue.NextDeadline( ) ) <= time )
            {
                n++;
                Entity entity = queue.Pop( );
                this.UpdateEntity( entity, t );
                if (n > 10000) throw new Exception( "Possible infinite update loop detected in "+this.GetType() );
            }
        }
        /// <summary>
        /// Update the given Entity at scheduled time.
        /// </summary>
        /// <param name="entity">The Entity to update.</param>
        /// <param name="time">The time of the update.</param>
        public abstract void UpdateEntity ( Entity entity, float time );
    }
}