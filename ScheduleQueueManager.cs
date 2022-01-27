using System;
using System.Collections.Generic;

namespace VodeoECS
{
    using QueueIndex = RegistryIndex<NativePriorityQueue<Entity>>;
    /// <summary>
    /// The ECS Schedule Queue Manager is used by ECS Systems to register Schedule Queues and schedule Entity updates.
    /// </summary>
    public class ScheduleQueueManager : IDisposable
    {
        private NamedRegistry<NativePriorityQueue<Entity>> queues;
        private Dictionary<QueueIndex, ScheduledSystemECS> systems;
        private int capacity;

        /// <summary>
        /// Constructs a Schedule Queue Manager.
        /// </summary>
        /// <param name="queueCapacity">Starting capacity for the queue.</param>
        public ScheduleQueueManager ( int queueCapacity )
        {
            this.capacity = queueCapacity;
            this.queues = new NamedRegistry<NativePriorityQueue<Entity>>( );
            this.systems = new Dictionary<QueueIndex, ScheduledSystemECS>( );
        }


        /// <summary>
        /// Request a registered Schedule Queue by name and register the given Scheduled System as dependency.
        /// </summary>
        /// <param name="name">The name of the requested Queue.</param>
        /// <param name="system">The Scheduled System to register as dependency.</param>
        /// <returns></returns>
        public ScheduleQueue GetQueue ( string name, ScheduledSystemECS system )
        {
            var index = this.queues.GetIndexByName( name );
            this.systems[index].Dependencies.Add( system );

            return new ScheduleQueue( this.queues[index] );
        }

        /// <summary>
        /// Request a registered Schedule Queue by name for use by a Passive System.
        /// </summary>
        /// <param name="name">The name of the requested Queue.</param>
        /// <param name="system">The Scheduled System to register as dependency.</param>
        /// <returns></returns>
        public ScheduleQueue GetQueueWithoutDependency ( string name )
        {
            var index = this.queues.GetIndexByName( name );

            return new ScheduleQueue( this.queues[index] );
        }

        /// <summary>
        /// Register a new Schedule Queue by name. The queue is "owned" by a single System but Entities can be Scheduled onto it by any System.
        /// </summary>
        /// <param name="name">The name of the new Schedule Queue.</param>
        /// <param name="system">The ECS System which will "own" this Schedule Queue.</param>
        /// <returns>The created Queue.</returns>
        public NativePriorityQueue<Entity> CreateQueue ( string name, ScheduledSystemECS system )
        {
            NativePriorityQueue<Entity> queue = new NativePriorityQueue<Entity>( capacity, Unity.Collections.Allocator.Persistent );
            this.systems.Add( this.queues.Register( queue, name ), system );
            return queue;
        }

        /// <summary>
        /// For internal use by the Serializer. Get the Queue Registry.
        /// </summary>
        /// <returns>The Schedule Queue Registry.</returns>
        public NamedRegistry<NativePriorityQueue<Entity>> GetQueueRegistry ( )
        {
            return this.queues;
        }

        /// <summary>
        /// Dispose the Native Memory reserved by this Schedule Queue Manager.
        /// </summary>
        public void Dispose ( )
        {
            foreach ( NativePriorityQueue<Entity> queue in queues )
            {
                queue.Dispose( );
            }
        }
    }
}