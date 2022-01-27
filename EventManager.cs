using System;
using System.Collections.Generic;
using VodeoECS.Internal;

namespace VodeoECS
{
    /// <summary>
    /// Event Manager. For registering Event pools and requesting listening and emitting access to them.
    /// </summary>
    public class EventManager : IDisposable
    {
        private World world;
        private Registry<Type> eventRegistry;
        private List<IEventPool> eventPools;

        /// <summary>
        /// Construct an Event Manager. Normally called by World class.
        /// </summary>
        public EventManager ( World world )
        {
            this.world = world;
            this.eventRegistry = new Registry<Type>( );
            this.eventPools = new List<IEventPool>( );
        }

        /// <summary>
        /// Initialize all Event Pools and create System dependency graph.
        /// </summary>
        public void Initialize ( )
        {
            foreach ( IEventPool pool in this.eventPools)
            {
                foreach ( SystemECS emitter in pool.Emitters)
                {
                    if ( emitter is ScheduledSystemECS )
                    {
                        foreach ( SystemECS listener in pool.Listeners )
                        {
                            if ( listener is ScheduledSystemECS )
                            {
                                (( ScheduledSystemECS) listener ).Dependencies.Add( (ScheduledSystemECS ) emitter );
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Swap Event buffers for all Event pools, clearing the previous buffered Events and replacing them by the currently queued up Events.
        /// </summary>
        public void SwapBuffers( )
        {
            foreach ( IEventPool pool in this.eventPools )
            {
                pool.SwapBuffers( );
            }
        }

        /// <summary>
        /// Request an Event Emitter to create new Events of the given type.
        /// </summary>
        /// <typeparam name="T">The type of Events to create.</typeparam>
        /// <param name="system">The System using this Emitter, or null if not used by an ECS System.</param>
        /// <returns>The requested Emitter.</returns>
        public EventEmitter<T> GetEmitter<T> ( SystemECS system ) where T : unmanaged, IEventECS
        {
            EventPool<T> pool = this.GetEventPool<T>( );
            if (system != null) pool.Emitters.Add( system );
            return pool.GetEmitQueue();
        }

        /// <summary>
        /// Request an Event Emitter to listen to Events of the given type.
        /// </summary>
        /// <typeparam name="T">The type of Events to listen to.</typeparam>
        /// <param name="system">The System using this Listener, or null if not used by an ECS System.</param>
        /// <returns>The requested Emitter.</returns>
        public EventListener<T> GetListener<T> ( SystemECS system ) where T : unmanaged, IEventECS
        {
            EventPool<T> pool = this.GetEventPool<T>( );
            if ( system != null ) pool.Listeners.Add( system );

            Type type = typeof( T );
            if (type.IsGenericType)
            {
                if ( type.GetGenericTypeDefinition( ) == typeof( ComponentCreationEvent<> ) )
                {
                    world.GetComponentPoolDynamic( world.GetComponentTypeRegistry( ).GetIndex( type.GetGenericArguments()[0] ) ).EnableCreationEvents( );
                }
                else if ( type.GetGenericTypeDefinition( ) == typeof( ComponentDestructionEvent<> ) )
                {
                    world.GetComponentPoolDynamic( world.GetComponentTypeRegistry( ).GetIndex( type.GetGenericArguments( )[0] ) ).EnableDestructionEvents( );
                }
            }

            return pool.GetListenBuffer();
        }

        /// <summary>
        /// Dispose this Event Manager.
        /// </summary>
        public void Dispose ( )
        {
            foreach ( IEventPool pool in eventPools )
            {
                pool.Dispose( );
            }
        }

        private EventPool<T> GetEventPool<T> ( ) where T : unmanaged, IEventECS
        {
            RegistryIndex<Type> poolType;
            if ( this.eventRegistry.TryGetIndex( typeof( T ), out poolType ) )
            {
                return ( EventPool<T> )eventPools[poolType.ID];
            }
            else
            {
                EventPool<T> pool = new EventPool<T>( true );
                this.eventRegistry.Register( typeof( T ) );
                this.eventPools.Add( pool );
                return pool;
            }
        }

    }
}