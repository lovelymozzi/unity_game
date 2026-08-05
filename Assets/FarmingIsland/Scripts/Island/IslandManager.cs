using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using DG.Tweening;

namespace CryingSnow.FarmingIsland
{
    public class IslandManager : MonoBehaviour
    {
        public static IslandManager Instance { get; private set; }

        public event System.Action<int> OnCoinChanged;
        public event System.Action<GameData> OnSaveGame;

        #region Serialized Fields
        [Header("Settings")]
        [SerializeField, Tooltip("The size of each island, used to determine spacing and placement.")]
        private int islandSize = 8;

        [SerializeField, Tooltip("The base price for unlocking an island, which increases with each subsequent purchase.")]
        private int baseUnlockPrice = 100;

        [SerializeField, Tooltip("The factor by which the unlock price increases for each island.")]
        private float unlockGrowthFactor = 1.1f;

        [SerializeField, Tooltip("The starting amount of money the player begins with.")]
        private int startingMoney = 500;

        [Header("References")]
        [SerializeField, Tooltip("List of meshes used to represent different island configurations.")]
        private List<Mesh> islandMeshes;

        [SerializeField, Tooltip("List of meshes used to represent different road configurations connecting islands.")]
        private List<Mesh> roadMeshes;

        [SerializeField, Tooltip("Prefab used to display purchasing option when an island is available for unlocking.")]
        private Purchaser purchaserPrefab;

        [SerializeField, Tooltip("Theme song that will be played for current level.")]
        private AudioClip themeSong;

#if UNITY_EDITOR
        [Header("Editor Tool")]
        [SerializeField, Tooltip("Whether to draw road connections between islands in the editor for debugging.")]
        private bool drawRoads = true;
#endif
        #endregion

        #region Private Fields
        private string saveFileName;
        private GameData data;

        private NavMeshSurface navMeshSurface;

        private List<Island> islands = new List<Island>();

        private Dictionary<Vector3Int, Island> islandGrid = new Dictionary<Vector3Int, Island>();
        private Dictionary<Vector3Int, Purchaser> activePurchasers = new Dictionary<Vector3Int, Purchaser>();

        private Coroutine navMeshUpdateCoroutine;

        private readonly Dictionary<int, int> tileIndexMap = new Dictionary<int, int>
        {
            { 2, 1 }, { 8, 2 }, { 10, 3 }, { 11, 4 }, { 16, 5 },
            { 18, 6 }, { 22, 7 }, { 24, 8 }, { 26, 9 }, { 27, 10 },
            { 30, 11 }, { 31, 12 }, { 64, 13 }, { 66, 14 }, { 72, 15 },
            { 74, 16 }, { 75, 17 }, { 80, 18 }, { 82, 19 }, { 86, 20 },
            { 88, 21 }, { 90, 22 }, { 91, 23 }, { 94, 24 }, { 95, 25 },
            { 104, 26 }, { 106, 27 }, { 107, 28 }, { 120, 29 }, { 122, 30 },
            { 123, 31 }, { 126, 32 }, { 127, 33 }, { 208, 34 }, { 210, 35 },
            { 214, 36 }, { 216, 37 }, { 218, 38 }, { 219, 39 }, { 222, 40 },
            { 223, 41 }, { 248, 42 }, { 250, 43 }, { 251, 44 }, { 254, 45 },
            { 255, 46 }, { 0, 47 }
        };
        #endregion

        #region Public Properties
        public int IslandSize => islandSize;

        public List<Island> UnlockedIslands { get; private set; } = new List<Island>();
        public List<PurchaserData> Purchasers { get; private set; } = new List<PurchaserData>();
        public List<(Vector3Int, Location)> PropLocations { get; private set; } = new List<(Vector3Int, Location)>();

        public bool HasNavMesh { get; private set; } = false;

        public int[] AnimalLevels => data.AnimalLevels;

        public int Coin
        {
            get => data.Coin;
            set
            {
                data.Coin = value;
                OnCoinChanged?.Invoke(value);
            }
        }
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            Instance = this;
            navMeshSurface = GetComponent<NavMeshSurface>();
            islands = GetComponentsInChildren<Island>().ToList();

            foreach (var island in islands)
            {
                Vector3Int pos = island.GetPosition();
                if (!islandGrid.ContainsKey(pos))
                {
                    islandGrid.Add(pos, island);
                }
            }

            ItemDatabase.Initialize();
            saveFileName = SceneManager.GetActiveScene().name;
            LoadGameData();
        }

        private void Start()
        {
            // Events Subscription
            islands.ForEach(island =>
            {
                island.OnActivated += () => OnIslandUnlocked(island);
                island.OnPropsInitialized += RequestNavMeshUpdate;
            });

            // Islands Initialization
            islands.ForEach(island => RefreshIsland(island));
            RequestNavMeshUpdate();

            AudioManager.Instance.PlayBGM(themeSong);
        }

        private void OnDisable()
        {
            SaveGameData();
            DOTween.KillAll();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) SaveGameData();
        }

        private void OnApplicationQuit()
        {
            SaveGameData();
        }
        #endregion

        #region Core Logic
        private void OnIslandUnlocked(Island newlyUnlockedIsland)
        {
            // 1. Refresh the island we just bought (to update its visual mesh)
            RefreshIsland(newlyUnlockedIsland);

            // 2. Refresh neighbors to update the Coastline/Road connections of adjacent UNLOCKED islands.
            Vector3Int center = newlyUnlockedIsland.GetPosition();
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    if (x == 0 && z == 0) continue;

                    Vector3Int neighborPos = center + new Vector3Int(x, 0, z) * islandSize;
                    if (islandGrid.TryGetValue(neighborPos, out Island neighbor))
                    {
                        RefreshIsland(neighbor);
                    }
                }
            }

            // 3. Check ALL Locked Islands.
            // Why? An island far away might have a "Constraint" that requires the island 
            // we just bought. Since it's not a neighbor, the loop above missed it.
            foreach (var island in islands)
            {
                // We only need to check Locked islands here. 
                // Unlocked islands were handled by the neighbor loop if they needed mesh updates.
                if (!island.IsUnlocked)
                {
                    RefreshIsland(island);
                }
            }

            RequestNavMeshUpdate();
        }

        private void RefreshIsland(Island island)
        {
            Vector3Int islandPos = island.GetPosition();

            // 1. Get all 8 neighbors for Tiling (Corners require diagonal data)
            var allNeighbors = CheckIslandsFast(islandPos);

            // 2. Extract ONLY Cardinal Neighbors for Colliders [North, West, East, South]
            // Indices from CheckIslandsFast: 0=NW, 1=N, 2=NE, 3=W, 4=E, 5=SW, 6=S, 7=SE
            bool[] cardinalNeighbors = new bool[]
            {
                allNeighbors[1].exists, // North
                allNeighbors[3].exists, // West
                allNeighbors[4].exists, // East
                allNeighbors[6].exists  // South
            };

            if (island.IsUnlocked)
            {
                if (!UnlockedIslands.Contains(island)) UnlockedIslands.Add(island);

                RemovePurchaserAt(islandPos);

                // Update Meshes (Uses all 8 neighbors for correct corner tiling)
                int islandMeshIndex = GetIslandTileIndex(islandPos, allNeighbors);
                if (islandMeshIndex >= islandMeshes.Count) islandMeshIndex = 0;

                Mesh islandMesh = island.IsBridge ? islandMeshes[0] : islandMeshes[islandMeshIndex];

                int roadTileIndex = GetRoadTileIndex(islandPos);
                Mesh roadMesh = roadMeshes[roadTileIndex];

                // Pass the "cardinalNeighbors" array (4 items) instead of "allNeighbors" (8 items)
                island.ConfigureIsland(islandMesh, roadMesh, cardinalNeighbors);
            }
            else
            {
                island.gameObject.SetActive(false);

                // Use the cardinal check for purchasing logic (we can only buy from cardinal adjacency)
                if (cardinalNeighbors.Any(x => x))
                {
                    if (island.Constraint != null && !island.Constraint.IsUnlocked) return;

                    int price = CalculateIslandPrice(island.transform.GetSiblingIndex());
                    SpawnPurchaserFor(island, price);
                }
                else
                {
                    RemovePurchaserAt(islandPos);
                }
            }
        }

        private void SpawnPurchaserFor(Island island, int price)
        {
            Vector3Int pos = island.GetPosition();

            if (activePurchasers.ContainsKey(pos)) return;

            var purchaser = Instantiate(purchaserPrefab, pos, Quaternion.identity);
            var newPurchaserData = new PurchaserData(pos, price);

            purchaser.Init(island, price, newPurchaserData);

            activePurchasers.Add(pos, purchaser);
            Purchasers.Add(newPurchaserData);
        }

        private void RemovePurchaserAt(Vector3Int position)
        {
            if (activePurchasers.TryGetValue(position, out Purchaser purchaser))
            {
                if (purchaser != null) Destroy(purchaser.gameObject);
                activePurchasers.Remove(position);

                // Also sync the data list
                Purchasers.RemoveAll(p => p.Position == position);
            }
        }

        private int GetIslandTileIndex(Vector3Int position, (bool exists, bool isBridge)[] adj = null)
        {
            int index = 0;

            // If we didn't pass cached neighbors, calculate them now
            if (adj == null) adj = CheckIslandsFast(position);

            // Mapping results to readable booleans
            // adj index: 0=NW, 1=N, 2=NE, 3=W, 4=E, 5=SW, 6=S, 7=SE
            bool nw = adj[0].exists && !adj[0].isBridge;
            bool n = adj[1].exists && !adj[1].isBridge;
            bool ne = adj[2].exists && !adj[2].isBridge;
            bool w = adj[3].exists && !adj[3].isBridge;
            bool e = adj[4].exists && !adj[4].isBridge;
            bool sw = adj[5].exists && !adj[5].isBridge;
            bool s = adj[6].exists && !adj[6].isBridge;
            bool se = adj[7].exists && !adj[7].isBridge;

            if (nw && n && w) index += 1;
            if (n) index += 2;
            if (ne && n && e) index += 4;
            if (w) index += 8;
            if (e) index += 16;
            if (sw && s && w) index += 32;
            if (s) index += 64;
            if (se && s && e) index += 128;

            return tileIndexMap.TryGetValue(index, out int mappedIndex) ? mappedIndex : -1;
        }

        private (bool exists, bool isBridge)[] CheckIslandsFast(Vector3Int position)
        {
            // Standardizes the order for the whole system:
            // 0:NW, 1:N, 2:NE, 3:W, 4:E, 5:SW, 6:S, 7:SE
            Vector3Int[] offsets = {
                new Vector3Int(-1, 0, 1), new Vector3Int(0, 0, 1), new Vector3Int(1, 0, 1),
                new Vector3Int(-1, 0, 0),                          new Vector3Int(1, 0, 0),
                new Vector3Int(-1, 0, -1), new Vector3Int(0, 0, -1), new Vector3Int(1, 0, -1)
            };

            var results = new (bool, bool)[8];

            for (int i = 0; i < 8; i++)
            {
                Vector3Int checkPos = position + (offsets[i] * islandSize);
                // Important: We only consider the neighbor "Existing" if it is UNLOCKED
                if (islandGrid.TryGetValue(checkPos, out Island island) && island.IsUnlocked)
                {
                    results[i] = (true, island.IsBridge);
                }
                else
                {
                    results[i] = (false, false);
                }
            }
            return results;
        }

        private int GetRoadTileIndex(Vector3Int position)
        {
            int index = 0;

            // Check N, W, E, S
            if (HasRoadAt(position + new Vector3Int(0, 0, 1) * islandSize)) index += 1;
            if (HasRoadAt(position + new Vector3Int(-1, 0, 0) * islandSize)) index += 2;
            if (HasRoadAt(position + new Vector3Int(1, 0, 0) * islandSize)) index += 4;
            if (HasRoadAt(position + new Vector3Int(0, 0, -1) * islandSize)) index += 8;

            return index;
        }

        private bool HasRoadAt(Vector3Int pos)
        {
            if (islandGrid.TryGetValue(pos, out Island island))
            {
                // A road connects if the island is there, has a road, AND is unlocked
                return island.IsUnlocked && island.HasRoad;
            }
            return false;
        }
        #endregion

        #region NavMesh Management
        private void RequestNavMeshUpdate()
        {
            if (!HasNavMesh)
            {
                // Immediate build for first run
                navMeshSurface.BuildNavMesh();
                HasNavMesh = true;
                return;
            }

            // If an update is already pending, don't start another one
            if (navMeshUpdateCoroutine != null) return;

            navMeshUpdateCoroutine = StartCoroutine(UpdateNavMeshDelayed());
        }

        private IEnumerator UpdateNavMeshDelayed()
        {
            // Wait for end of frame to allow all batch updates (like loading 20 props) to finish
            yield return new WaitForEndOfFrame();

            var op = navMeshSurface.UpdateNavMesh(navMeshSurface.navMeshData);
            yield return op;

            navMeshUpdateCoroutine = null;
        }
        #endregion

        #region Data Handling
        public InventoryData LoadInventoryData(string inventoryId) => data.InventoryDatabase.FirstOrDefault(data => data.InventoryId == inventoryId);
        public SiloData LoadSiloData(string siloId) => data.SiloDatabase.FirstOrDefault(data => data.SiloId == siloId);

        private void LoadGameData()
        {
            data = SaveSystem.LoadData<GameData>(saveFileName);

            if (data == null)
            {
                var unlockedBools = islands.Select(island => island.IsUnlocked).ToList();
                data = new GameData(startingMoney, unlockedBools);
            }
            else
            {
                // Apply unlocked states
                if (data.UnlockedIslands.Count == islands.Count)
                {
                    for (int i = 0; i < islands.Count; i++)
                    {
                        islands[i].IsUnlocked = data.UnlockedIslands[i];
                    }
                }

                // Spawn Saved Purchasers
                foreach (var pData in data.Purchasers)
                {
                    if (islandGrid.TryGetValue(pData.Position, out Island island))
                    {
                        // Ensure we don't spawn purchasers on already unlocked islands (save file mismatch protection)
                        if (!island.IsUnlocked)
                        {
                            SpawnPurchaserFor(island, pData.Price);
                        }
                    }
                }
            }
        }

        private void SaveGameData()
        {
            data.UnlockedIslands = islands.Select(island => island.IsUnlocked).ToList();
            data.Purchasers = Purchasers; // This list is kept in sync in Spawn/Remove methods
            OnSaveGame?.Invoke(data);

            SaveSystem.SaveData<GameData>(data, saveFileName);
        }
        #endregion

        #region Helpers & Debug
        public Island GetCurrentIsland(Vector3 currentPosition)
        {
            // Simple spatial check - Grid check would be harder here due to float-to-int conversion
            // sticking to bounds check is fine for player position
            foreach (var island in islands)
            {
                Bounds islandBounds = new Bounds((Vector3)island.GetPosition(), Vector3.one * islandSize);
                if (islandBounds.Contains(currentPosition)) return island;
            }
            return null;
        }

        public Island GetRandomIsland()
        {
            return UnlockedIslands.Where(i => !i.IsBridge).ToList().SelectRandom();
        }

        public void RegisterLocation(Vector3Int position, Location location)
        {
            PropLocations.Add((position, location));
        }

        private int CalculateIslandPrice(int islandIndex)
        {
            return Mathf.RoundToInt(Mathf.Round(baseUnlockPrice * Mathf.Pow(unlockGrowthFactor, islandIndex)) / 5f) * 5;
        }

        private void Update()
        {
            if (SimpleInput.GetButtonDown("One Million Coins"))
            {
                Coin += 1000000;
                AudioManager.Instance.PlaySFX(AudioID.Cash);
                Debug.Log("TESTING: Added 1M Coins. REMOVE ON BUILD!");
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawRoads) return;

            // In Editor, islandGrid might not be populated if game isn't running
            var debugIslands = GetComponentsInChildren<Island>().ToList();

            foreach (var island in debugIslands)
            {
                if (!island.HasRoad) continue;

                Vector3Int islandPos = island.GetPosition();
                float radius = 0.3f;
                Vector3 offset = Vector3.up * radius;

                // Simple Check for Gizmos
                var dirs = new[] { Vector3.forward, Vector3.left, Vector3.right, Vector3.back };
                var neighbors = new[] {
                    islandPos + new Vector3Int(0,0,1)*islandSize,
                    islandPos + new Vector3Int(-1,0,0)*islandSize,
                    islandPos + new Vector3Int(1,0,0)*islandSize,
                    islandPos + new Vector3Int(0,0,-1)*islandSize
                };

                for (int i = 0; i < 4; i++)
                {
                    var neighbor = debugIslands.FirstOrDefault(x => x.GetPosition() == neighbors[i]);
                    if (neighbor != null && neighbor.HasRoad)
                    {
                        Debug.DrawLine(islandPos + offset, islandPos + offset + dirs[i] * 4, Color.blue);
                    }
                }

                Gizmos.color = island.IsBridge ? Color.red : Color.yellow;
                Gizmos.DrawWireSphere(islandPos + (island.IsBridge ? offset * 2 : offset), radius);
            }
        }
#endif
        #endregion
    }
}
