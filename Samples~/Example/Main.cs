using System.IO;
using UnityEngine;
using UnityEditor;
using VodeoECS;
using VodeoECS.Standard;

public class Main : MonoBehaviour
{
    public World world; //Our ECS World
    private ECS_Serializer serializer;

    private ObjectRenderSystem objectRenderer;

    private string saveDirectory;
    private string saveName = "demoSave";

    //Initialization
    public void Awake ( )
    {
#if UNITY_EDITOR
        //Copy StreamingAssets folder from sample if not present
        DirectoryInfo directoryInfo = new DirectoryInfo( Application.streamingAssetsPath );
        if ( !directoryInfo.Exists )
        {
            FileUtil.CopyFileOrDirectory( "Assets/Samples/Vodeo ECS/0.1.0/Example/StreamingAssets/", "Assets/StreamingAssets/" );
        }
#endif

        //Create World
        world = new World( );

        //Create our Systems
        new LevelGeneratorSystem( world );
        new MeshRenderSystem( world );
        new AgentSystem( world );
        new PathFindingSystem( world );
        new PhysicsSystem( world );
        new SpawnerSystem( world );
        new FlyingSystem( world );
        new PlayerSystem( world );
        new AISystem( world );
        objectRenderer = new ObjectRenderSystem( world );

        //Our serializer to save/load the world
        serializer = new ECS_Serializer( world );
    }

    // Start is called before the first frame update
    void Start ()
    {
        //Initialize all Systems
        world.InitializeSystems( );
        //Path where save files are stored
        saveDirectory = Application.persistentDataPath + "/Saves/";
    }

    // Update is called once per frame
    void Update()
    {
        this.GetComponent<PlayerInputs>( ).AttachCamera( objectRenderer );
        world.Systems.FrameUpdate( );
    }

    private void LateUpdate ( )
    {
        world.Systems.CompleteUpdate( );

        if ( Input.GetKeyDown( KeyCode.Alpha5 ) )
        {
            Directory.CreateDirectory( saveDirectory );
            string path = saveDirectory + saveName + ".json";
            StreamWriter writer = new StreamWriter( path, false );
            writer.WriteLine( serializer.SerializeWorld( ) );
            Debug.Log( "State saved to " + path );
            writer.Dispose( );
        }
        else if ( Input.GetKeyDown( KeyCode.Alpha9 ) )
        {
            string path = saveDirectory + saveName + ".json";
            StreamReader reader = new StreamReader( path, false );
            serializer.DeserializeWorld( reader.ReadToEnd( ) );
            Debug.Log( "State loaded from " + path );
            reader.Dispose( );
        }
    }

    public void OnDestroy ( )
    {
        world.Dispose( );
    }
}
