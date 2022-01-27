using System;
using System.Collections.Generic;
using UnityEngine;

namespace VodeoECS.Internal
{
    /// <summary>
    /// Manages Systems. For internal use by the World class.
    /// </summary>
    public class SystemManager
    {
        private World world;
        private EventListener<DestroyEntityEvent> destroyEvents;

        private Registry<Type> scheduledRegistry;
        private Registry<Type> frameRegistry;
        private Registry<Type> passiveRegistry;

        private List<ScheduledSystemECS> scheduledSystems;
        private List<FrameSystemECS> frameSystems;
        private List<PassiveSystemECS> passiveSystems;

        public SystemManager ( World world )
        {
            this.world = world;

            this.scheduledRegistry = new NamedRegistry<Type>( );
            this.frameRegistry = new NamedRegistry<Type>( );
            this.passiveRegistry = new NamedRegistry<Type>( );

            this.scheduledSystems = new List<ScheduledSystemECS>( );
            this.frameSystems = new List<FrameSystemECS>( );
            this.passiveSystems = new List<PassiveSystemECS>( );

            this.destroyEvents = world.Events.GetListener<DestroyEntityEvent>( null );
        }

        /// <summary>
        /// Register a ScheduledSystem with the Manager. This should be called by all ScheduledSystem on construction.
        /// </summary>
        /// <param name="system">The ScheduledSystem to register. Systems should pass themselves as parameter.</param>
        public void RegisterSystem ( ScheduledSystemECS system )
        {
            this.scheduledSystems.Add( system );
            this.scheduledRegistry.Register( system.GetType( ) );
        }
        /// <summary>
        /// Register a FrameSystem with the Manager. This should be called by all FrameSystem on construction.
        /// </summary>
        /// <param name="system">The FrameSystem to register. Systems should pass themselves as parameter.</param>
        public void RegisterSystem ( FrameSystemECS system )
        {
            this.frameSystems.Add( system );
            this.frameRegistry.Register( system.GetType( ) );
        }
        /// <summary>
        /// Register a PassiveSystem with the Manager. This should be called by all PassiveSystems on construction.
        /// </summary>
        /// <param name="system">The PassiveSystem to register. Systems should pass themselves as parameter.</param>
        public void RegisterSystem ( PassiveSystemECS system )
        {
            this.passiveSystems.Add( system );
            this.passiveRegistry.Register( system.GetType( ) );
        }

        /// <summary>
        /// Initialize all Systems.
        /// </summary>
        public void Initialize ( )
        {
            foreach ( PassiveSystemECS system in this.passiveSystems )
            {
                system.Initialize( );
            }

            foreach ( ScheduledSystemECS system in this.scheduledSystems )
            {
                system.Initialize( );
            }

            foreach ( FrameSystemECS system in this.frameSystems )
            {
                system.Initialize( );
            }
        }

        /// <summary>
        /// Update Systems up to current time, in preparation for a frame render.
        /// </summary>
        public void FrameUpdate ( )
        {
            //next frame time
            float t = Time.time + world.timeOffset;

            bool events = true;
            while ( events )
            {
                this.ProcessEvents( );
                events = false;

                bool updates = true;
                while ( updates )
                {
                    ScheduledSystemECS minSystem = null;
                    float min = float.MaxValue;
                    //find system to update
                    foreach ( ScheduledSystemECS system in this.scheduledSystems )
                    {
                        float next;
                        if ( ( next = system.ScheduleQueue.NextDeadline( ) ) < min )
                        {
                            min = next;
                            minSystem = system;
                        }
                    }

                    if ( min < t )
                    {
                        // find maximum time without violating dependencies
                        float max = t;
                        foreach ( ScheduledSystemECS system in minSystem.Dependencies)
                        {
                            float next = system.ScheduleQueue.NextDeadline( );
                            if ( next < max ) max = next; 
                        }

                        //update system until up to date
                        minSystem.UpdateTo( max );
                        events = true;
                        break;
                    }
                    else
                    {
                        updates = false;
                    }
                }
            }

            foreach ( FrameSystemECS system in this.frameSystems )
            {
                system.UpdateFrame( t );
            }
        }

        /// <summary>
        /// Complete a frame update.
        /// </summary>
        public void CompleteUpdate ( )
        {
            foreach ( FrameSystemECS system in this.frameSystems )
            {
                system.CompleteUpdate( );
            }

            this.ProcessEvents( );
        }


        /// <summary>
        /// Dispose the System Manager.
        /// </summary>
        public void Dispose ( )
        {
            foreach ( FrameSystemECS system in frameSystems )
                system.Dispose( );
            foreach ( PassiveSystemECS system in passiveSystems )
                system.Dispose( );
            foreach ( ScheduledSystemECS system in scheduledSystems )
                system.Dispose( );
        }

        private void ProcessEvents ( )
        {
            this.world.Events.SwapBuffers( );

            foreach ( PassiveSystemECS system in this.passiveSystems )
            {
                system.ProcessEvents( );
            }
            foreach ( ScheduledSystemECS system in this.scheduledSystems )
            {
                system.ProcessEvents( );
            }
            foreach ( FrameSystemECS system in this.frameSystems )
            {
                system.ProcessEvents( );
            }

            foreach ( DestroyEntityEvent e in destroyEvents )
            {
                world.DestroyEntity( e.entity );
            }
        }
    }
}