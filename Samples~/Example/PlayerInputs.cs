using UnityEngine;
using VodeoECS;
using VodeoECS.Standard;

public class PlayerInputs : MonoBehaviour
{
    private EventEmitter<InputEvent> inputEmitter;
    private EventEmitter<ChangeHomeEvent> homeEmitter;
    private Archetype playerArchetype;

    public void AttachCamera( ObjectRenderSystem system )
    {
        Entity player = this.GetComponent<Main>( ).world.GetRandomEntityOfArchetype( this.GetComponent<Main>( ).world.GetDataComponentPool<FlyingComponent>( ), this.GetComponent<Main>( ).world.MakeQuery( playerArchetype, new PlayerFilter( )) );

        //set camera
        Camera.main.transform.SetParent( system.GetTransform( player ) );
        Camera.main.transform.localPosition = new Vector3( 0, 1.0f, -3.0f );
        Camera.main.transform.localRotation = Quaternion.identity;
    }

    private void Awake ( )
    {
        this.playerArchetype = this.GetComponent<Main>( ).world.DefineArchetype( this.GetComponent<Main>( ).world.GetDataComponentPool<FlyingComponent>( ), this.GetComponent<Main>( ).world.GetFilterComponentPool<PlayerFilter>( ) );
    }

    // Start is called before the first frame update
    void Start()
    {
        this.inputEmitter = this.GetComponent<Main>( ).world.Events.GetEmitter<InputEvent>( null );
        this.homeEmitter = this.GetComponent<Main>( ).world.Events.GetEmitter<ChangeHomeEvent>( null );

        Cursor.visible = false;
    }

    // Update is called once per frame
    void Update ( ) 
    {
        InputEvent e = new InputEvent( );
        e.yawAxis = Mathf.Clamp( Input.GetAxis( "Mouse X" ), -0.5f, 0.5f ) * 2.0f;
        e.pitchAxis = Mathf.Clamp( Input.GetAxis( "Mouse Y" ), -0.5f, 0.5f ) * 2.0f;
        e.rollAxis = ( Input.GetKey( KeyCode.A ) ? 1 : 0 ) - ( Input.GetKey( KeyCode.D ) ? 1 : 0 );
        e.throttle = ( Input.GetKey( KeyCode.W ) ? 1 : 0 ) - ( Input.GetKey( KeyCode.S ) ? 1 : 0 );

        inputEmitter.CreateEvent( e );

        if (Input.GetKeyDown(KeyCode.Space))
        {
            this.homeEmitter.CreateEvent( new ChangeHomeEvent( ) );
        }
    }
}
