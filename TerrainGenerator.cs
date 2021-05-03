/*
 * This file serves as the terrain generator for Poly Dungeon Runner (project name subject to change).
 * It's adapted from the terrain generator I made for Trials of Tonalli, and has the following in common
 * with that script:
 * - Terrain is procedurally generated in chunks, similar to Minecraft
 * - Unlike Minecraft, terrain is never saved to or loaded from disk
 * - When a "default" terrain tile is generated, it has a chance to be populated with something (eg. an item pickup)
 * However, there are some major differences from the ToT TerrainGenerator:
 * - Tiles and chunks are hexagonal here, as opposed to square for ToT
 * - Chunks unload immediately when the player moves too far away, instead of lingering briefly as in ToT
 * - The terrain of a particular chunk is always the same and does not change when it is reloaded
 * - The only terrain types are basic tiles and walls, as opposed to grass, dirt, lava and walls in ToT
 * - Each chunk has a biome associated with it which affects the chunk's color and the way walls generate
 */

// Lots of stuff taken from https://www.redblobgames.com/grids/hexagons/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

enum HexType
{
    FlatTop = 0,
    PointyTop = 1
}

enum BiomeType
{
    Red = 0,
    Orange = 1,
    Green = 2
}

public class TerrainGenerator : MonoBehaviour
{
    [SerializeField] private GameObject basicTile;
    [SerializeField] private GameObject wallTile;

    [SerializeField] private GameObject chunkPrefab;

    [SerializeField] private GameObject ghostEnemy;
    [SerializeField] private GameObject shooterEnemy;
    [SerializeField] private GameObject treasureBox;
    private Dictionary<GameObject, float> entitySpawnChances;

    private static int chunkE = 6; // Chunk edge length
    private static int chunkR = chunkE - 1; // Chunk radius, excluding center hex
    private static int chunkD = chunkE * 2 - 1; // Chunk diameter at cross-section (including center hex)
    private float chunkSize;
    private static Dictionary<long, GameObject> loadedChunks;
    public static bool spawnChunkGenerated; // When false, the next time chunk (0, 0) is generated it will be all open and this variable will be set to true

    private float tileSize; // The diameter of the tiles, from one flat edge to the opposing edge

    private static float SQRT_3 = Mathf.Sqrt(3);

    private static float biomeNoiseOffset1; // A chunk's biome is determined by its coordinates and these 2 values
    private static float biomeNoiseOffset2;
    private static int worldSeed; // The seed used for generating tiles & such inside of chunks

    private static Dictionary<BiomeType, Color> biomeColors = new Dictionary<BiomeType, Color>(){
        {BiomeType.Red, new Color(0.53f, 0f, 0f)},
        {BiomeType.Orange, new Color(0.53f, 0.35f, 0f)},
        {BiomeType.Green, new Color(0.04f, 0.51f, 0f)}
    };

    // This stuff could be in Start, but I like initializing static fields ASAP
    void Awake(){
        biomeNoiseOffset1 = RandomScript.Float();
        biomeNoiseOffset2 = RandomScript.Float(3.2f);
        worldSeed = (int)(biomeNoiseOffset1 * Int32.MinValue);
        loadedChunks = new Dictionary<long, GameObject>();
        spawnChunkGenerated = false;

        entitySpawnChances = new Dictionary<GameObject, float>{
            {ghostEnemy, 0.01f},
            {shooterEnemy, 0.005f},
            {treasureBox, 0.002f}
        };
    }

    void Start(){
        tileSize = basicTile.GetComponentInChildren<Renderer>().bounds.size.x;
        chunkSize = tileSize * chunkE;
    }

    void FixedUpdate(){
        Dictionary<long, GameObject> newLoadedChunks = new Dictionary<long, GameObject>();

        // Preserve/load chunks
        /*
        For each player (foreach)
            For all the chunk coordinates (q, r) that the player can see (foreach)
                If the chunk was already loaded, preserve it
                If not, load it now

        Now newLoadedChunks has all the chunks which will be loaded this tick
        */
        foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player")){
            AxialCoords playerChunkCoords = ChunkCoords(player);
            foreach(AxialCoords chunkCoords in GetHexesInRange(playerChunkCoords, 2)){
                if(!newLoadedChunks.ContainsKey(Hash(chunkCoords))){
                    if(loadedChunks.ContainsKey(Hash(chunkCoords))){
                        newLoadedChunks[Hash(chunkCoords)] = loadedChunks[Hash(chunkCoords)];
                    }
                    else{
                        newLoadedChunks[Hash(chunkCoords)] = GenerateChunk(chunkCoords);
                    }
                }
            }
        }

        // Unload chunks which are no longer visible
        foreach(long chunkHash in loadedChunks.Keys){
            if(!newLoadedChunks.ContainsKey(chunkHash)){
                Destroy(loadedChunks[chunkHash]);
            }
        }

        loadedChunks = newLoadedChunks;
    }

    private GameObject GenerateChunk(AxialCoords thisChunkCoords){ // The axial coordinates of the chunk in world space
        // Make a new chunk object
        AxialCoords chunkTileCoords = ChunkToTileCoords(thisChunkCoords);
        Vector3 chunkPos = AxialToSquare(chunkTileCoords, HexType.FlatTop); // New chunk world coords
        GameObject chunkObj = Instantiate(chunkPrefab, chunkPos, Quaternion.identity, transform);
        chunkObj.GetComponent<Chunk>().Init(thisChunkCoords.q, thisChunkCoords.r);
        chunkObj.name = "Chunk (" + thisChunkCoords.q + ", " + thisChunkCoords.r + ")";

        // Set the random seed
        UnityEngine.Random.InitState(GetChunkRandomSeed(thisChunkCoords));

        // Get the chunk biome
        BiomeType biome = GetBiome(chunkPos);
        Color biomeColor = biomeColors[biome];

        // Populate the chunk with tiles
        AxialCoords tileCoords;
        GameObject tile;
        Vector3 tilePos;
        GameObject tileType;
        foreach(AxialCoords offsetCoords in GetHexesInRange(AxialCoords.zero, chunkE-1)){
            // Generate this tile
            tileCoords = HexAdd(offsetCoords, chunkTileCoords); // The global tile coords
            tilePos = AxialToSquare(tileCoords, HexType.FlatTop);

            tileType = GenerateTile(thisChunkCoords, offsetCoords, biome);

            tile = Instantiate(
                    tileType, // the type of terrain
                    tilePos, // the world coordinates
                    Quaternion.Euler(0f, 30f, 0f), // the rotation
                    chunkObj.transform // the parent
            );
            tile.name = "Tile (" + tileCoords.q + ", " + tileCoords.r + ")";

            if(tileType == basicTile){
                Renderer tileRenderer = tile.GetComponentInChildren<Renderer>();
                tileRenderer.material.SetColor("_Color", biomeColor); // Maybe play around with sorting more in the future?
                // Maybe spawn an enemy or treasure on the tile
                if(!ThisChunkIsTheSpawnChunk(thisChunkCoords)){
                    PopulateTile(tilePos);
                }
            }
        }

        if(ThisChunkIsTheSpawnChunk(thisChunkCoords)) spawnChunkGenerated = true;

        return chunkObj;
    }

    private GameObject GenerateTile(AxialCoords chunkCoords, AxialCoords offsetCoords, BiomeType biome){
        if(ThisChunkIsTheSpawnChunk(chunkCoords)) return basicTile;

        switch(biome){
            case BiomeType.Red: // A biome with lines crossing through chunk centers
                if(offsetCoords.q == 0 && offsetCoords.r == 0){
                    return ChanceOfWall(0.5f);
                }
                else if(offsetCoords.q == 0 || offsetCoords.r == 0 || offsetCoords.q + offsetCoords.r == 0){
                    return ChanceOfWall(0.68f);
                }
                else{
                    return ChanceOfWall(0.05f);
                }
            case BiomeType.Orange: // A biome with randomly distributed walls
                return ChanceOfWall(0.125f);
            case BiomeType.Green: // A biome where some chunks have lots of walls and some have very few
                if(Mathf.Abs(chunkCoords.q + chunkCoords.r) % 2 == 0 && RandomScript.Float() > 0.5f){
                    float value = RandomScript.Float()+ (Mathf.Abs(chunkR - offsetCoords.q) / 3f) + (Mathf.Abs(chunkR - offsetCoords.r) / 3f);
                    return value > 3f ? wallTile : basicTile;
                }
                else{
                    return ChanceOfWall(0.1f);
                }
            default:
                return basicTile;
        }
    }

    private GameObject ChanceOfWall(float chance){
        return RandomScript.Float() > chance ? basicTile : wallTile;
    }

    private static bool ThisChunkIsTheSpawnChunk(AxialCoords chunkCoords){
        return chunkCoords.q == 0 && chunkCoords.r == 0 && !spawnChunkGenerated;
    }

    private static BiomeType GetBiome(Vector3 chunkPos){
        float value1 = Mathf.PerlinNoise((chunkPos.x / 110f) - biomeNoiseOffset2, (chunkPos.z / 110f) - biomeNoiseOffset2);
        float value2 = Mathf.PerlinNoise(-(chunkPos.x / 60f) + biomeNoiseOffset1, -(chunkPos.z / 60f) + biomeNoiseOffset1);
        if(value1 > 0.35f && value1 < 0.6f && value2 < 0.5f) return BiomeType.Red;
        else if(value1 > value2) return BiomeType.Orange;
        else return BiomeType.Green;
    }

    public static bool InLoadedTerrain(GameObject obj){
        // return loadedChunks.ContainsKey(Hash(ChunkCoords(obj))); <--- I'd do this except ChunkCoords relies on Find() too
        AxialCoords hex = SquareToAxial(new Vector3(obj.transform.position.x, 0f, obj.transform.position.z), HexType.FlatTop);
        return GameObject.Find("Tile (" + hex.q + ", " + hex.r + ")") != null;
    }

    // Get a random seed for a given chunk so it can be loaded multiple times and retain its generated features
    private static int GetChunkRandomSeed(AxialCoords chunkCoords){
        return Convert.ToInt32(Hash(chunkCoords)) + worldSeed;
    }

    private void PopulateTile(Vector3 tilePos){
        GameObject entity = RandomScript.Choice<GameObject>(entitySpawnChances);
        if(entity != null) SpawnEntity(entity, tilePos);
    }

    private static void SpawnEntity(GameObject entity, Vector3 tilePos){
        Instantiate(entity, new Vector3(tilePos.x, 0.5f, tilePos.z), Quaternion.identity);
    }

    // ------------------------------------- AUXILIARY FUNCTIONS -------------------------------------

    // The functions below deal with flat-top coordinates, except where there's a parameter for specifying the coordinate type

    // The below deals with a situation where the axes are:
    // We're going fron chunk coords to tile coords here
    /*
     *        Tile:                     Chunk:
     *             +r                     +r
     *        -----                        -
     *      ---------                    -----
     * +q -------------                  -----
     *      ---------                    -----
     *        -----                     /  -  \
     *             +s                 +q       +s
     *
     *   q offset: q * chunkD + r * chunkR
     *   r offset: -q * chunkR + r * chunkE
     *
     */

    private static AxialCoords ChunkCoords(GameObject obj){ // Works on players, chunks and other objects!
        return TileToChunkCoords(SquareToAxial(new Vector3(obj.transform.position.x, 0f, obj.transform.position.z), HexType.FlatTop));
    }

    private static AxialCoords TileToChunkCoords(AxialCoords hex){
        // Prevents a crash when this function is called on the player just after the scene is loaded
        if(!spawnChunkGenerated) return AxialCoords.zero;

        Chunk chunkScript = GameObject.Find("Tile (" + hex.q + ", " + hex.r + ")").transform.parent.gameObject.GetComponent<Chunk>();
        return new AxialCoords(chunkScript.q, chunkScript.r);
    }

    // Get the tile coords of the center hex of the chunk
    private static AxialCoords ChunkToTileCoords(AxialCoords chunkCoords){
        //return new AxialCoords(hex.q*chunkR + hex.r*chunkE, hex.r*chunkR + (hex.r - hex.q)*chunkE);
        return new AxialCoords(chunkCoords.q * chunkD + chunkCoords.r * chunkR, -chunkCoords.q * chunkR + chunkCoords.r * chunkE);
    }

    // Turns chunk coords into a key that can be used in a dictionary
    private static long Hash(AxialCoords chunkCoords){
        return chunkCoords.q * 1000L + chunkCoords.r;
    }

    // Given some floating-point axial coords, which hex are they in?
    private static AxialCoords HexRound(AxialCoordsFloat hex){
        return CubeToAxial(CubeRound(AxialToCubeFloat(hex)));
    }

    // Note that for the following 2 functions, the PointyTop versions are untested and may be incorrect
    private static AxialCoords SquareToAxial(Vector3 p, HexType type){
        float q, r;
        if(type == HexType.FlatTop){
            q = -2/3f * p.x;
            r = 1/3f * p.x + SQRT_3/3f * p.z;
        }
        else{
            q = -SQRT_3/3f * p.x - 1/3f * p.z;
            r = 2/3f * p.z;
        }
        return HexRound(new AxialCoordsFloat(q, r));
    }

    private static Vector3 AxialToSquare(AxialCoords hex, HexType type){
        float x, z;
        if(type == HexType.FlatTop){
            x = -3/2f * hex.q;
            z = SQRT_3 * (hex.q/2f + hex.r);
        }
        else{
            x = -SQRT_3 * (hex.q + hex.r/2f);
            z = 3/2f * hex.r;
        }
        return new Vector3(x, 0f, z);
    }

    private static AxialCoords CubeToAxial(CubeCoords cube){
        int q = cube.x;
        int r = cube.z;
        return new AxialCoords(q, r);
    }

    private static CubeCoords AxialToCube(AxialCoords hex){
        int x = hex.q;
        int z = hex.r;
        int y = -x-z;
        return new CubeCoords(x, y, z);
    }

    private static CubeCoordsFloat AxialToCubeFloat(AxialCoordsFloat hex){
        float x = hex.q;
        float z = hex.r;
        float y = -x-z;
        return new CubeCoordsFloat(x, y, z);
    }

    // Round some cube coordinates to the nearest hex
    private static CubeCoords CubeRound(CubeCoordsFloat cube){
        int rx = (int)Mathf.Round(cube.x);
        int ry = (int)Mathf.Round(cube.y);
        int rz = (int)Mathf.Round(cube.z);

        var xDiff = Mathf.Abs(rx - cube.x);
        var yDiff = Mathf.Abs(ry - cube.y);
        var zDiff = Mathf.Abs(rz - cube.z);

        if(xDiff > yDiff && xDiff > zDiff){
            rx = -ry-rz;
        }
        else if(yDiff > zDiff){
            ry = -rx-rz;
        }
        else{
            rz = -rx-ry;
        }

        return new CubeCoords(rx, ry, rz);
    }

    // Unused
    private static int HexDistance(AxialCoords a, AxialCoords b){
        // Inlined version of converting a and b to cube coords and taking CubeDistance()
        return (Mathf.Abs(a.q - b.q) + Mathf.Abs(a.q + a.r - b.q - b.r) + Mathf.Abs(a.r - b.r)) / 2;
    }

    private static AxialCoords HexAdd(AxialCoords a, AxialCoords b){
        return new AxialCoords(a.q + b.q, a.r + b.r);
    }

    // Unused
    private static int CubeDistance(CubeCoords a, CubeCoords b){
        return Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y) + Math.Abs(a.z - b.z) / 2;
    }

    private static CubeCoords CubeAdd(CubeCoords a, CubeCoords b){
        return new CubeCoords(a.x+b.x, a.y+b.y, a.z+b.z);
    }

    private static List<AxialCoords> GetHexesInRange(AxialCoords center, int distance){
        List<AxialCoords> results = new List<AxialCoords>();
        int z;
        for(int x = -distance; x <= distance; x++){
            for(int y = Math.Max(-distance, -x-distance); y <= Math.Min(distance, -x+distance); y++){
                z = -x - y;
                results.Add(CubeToAxial(CubeAdd(AxialToCube(center), new CubeCoords(x, y, z))));
            }
        }
        return results;
    }

}

class AxialCoords
{
    public int q, r;
    public static AxialCoords zero = new AxialCoords(0, 0);

    public AxialCoords(int q, int r){
        this.q = q;
        this.r = r;
    }

    public override string ToString(){
        return "AxialCoords(" + this.q + "," + this.r + ")";
    }
}

class AxialCoordsFloat
{
    public float q, r;

    public AxialCoordsFloat(float q, float r){
        this.q = q;
        this.r = r;
    }

    public override string ToString(){
        return "AxialCoordsFloat(" + this.q + "," + this.r + ")";
    }
}

class CubeCoords
{
    public int x, y, z;
    public CubeCoords(int x, int y, int z){
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public override string ToString(){
        return "CubeCoords(" + this.x + "," + this.y + "," + this.z + ")";
    }
}

class CubeCoordsFloat
{
    public float x, y, z;
    public CubeCoordsFloat(float x, float y, float z){
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public override string ToString(){
        return "CubeCoordsFloat(" + this.x + "," + this.y + "," + this.z + ")";
    }
}
