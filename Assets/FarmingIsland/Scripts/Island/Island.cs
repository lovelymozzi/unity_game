using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;

namespace CryingSnow.FarmingIsland
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class Island : MonoBehaviour
    {
        [Tooltip("Indicates whether the island is unlocked and available for the player to access or purchase.")]
        public bool IsUnlocked;

        [Tooltip("Specifies if this island is a bridge, determining its mesh and visual appearance.")]
        public bool IsBridge;

        [Tooltip("Indicates whether this island has a procedurally generated road connecting it to other islands.")]
        public bool HasRoad;

        [Tooltip("Another island that needs to be unlocked first before this island can be purchased.")]
        [SerializeField] private Island constraint;

        [Tooltip("The visual mesh representing the road on the island.")]
        [SerializeField] private MeshFilter roadMesh;

        [Tooltip("Colliders placed on the north, west, east, and south sides of the island, toggled based on neighboring islands.")]
        [SerializeField] private BoxCollider[] cardinalColliders;

        public event System.Action OnActivated;
        public event System.Action OnPropsInitialized;

        public Island Constraint => constraint;

        private int islandSize => IslandManager.Instance.IslandSize;

        private MeshFilter meshFilter;

        private List<IProp> props = new List<IProp>();

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            props = GetComponentsInChildren<IProp>(true).ToList();

            if (!HasRoad && !IsBridge)
            {
                this.roadMesh.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            if (IsUnlocked) StartCoroutine(InitializeProps(true));
        }

        private IEnumerator InitializeProps(bool instant)
        {
            int animated = 0;

            props.ForEach(prop =>
            {
                prop.Animate(instant, () => animated++);

                if (prop.Location != Location.None)
                {
                    var position = GetPosition();

                    if (prop.Location == Location.Fish)
                    {
                        var rightPosition = transform.TransformPoint(Vector3.right * islandSize);
                        position = Vector3Int.RoundToInt(rightPosition);
                    }
                    else if (prop.Location == Location.Dock)
                    {
                        var backPosition = transform.TransformPoint(Vector3.back * islandSize);
                        position = Vector3Int.RoundToInt(backPosition);
                    }

                    IslandManager.Instance.RegisterLocation(position, prop.Location);
                }
            });

            yield return new WaitWhile(() => animated < props.Count);

            OnPropsInitialized?.Invoke();
        }

        public Vector3Int GetPosition()
        {
            return Vector3Int.RoundToInt(transform.position);
        }

        public void ConfigureIsland(Mesh islandMesh, Mesh roadMesh, bool[] adjacentIslands)
        {
            meshFilter.mesh = islandMesh;

            if (HasRoad && !IsBridge)
            {
                this.roadMesh.mesh = roadMesh;
            }

            int[] rotatedIndices = GetRotatedIndices();

            for (int i = 0; i < cardinalColliders.Length; i++)
            {
                cardinalColliders[rotatedIndices[i]].enabled = !adjacentIslands[i];
            }
        }

        private int[] GetRotatedIndices()
        {
            // Original order: North (0), West (1), East (2), South (3)
            int[] indices = { 0, 1, 2, 3 };

            // Calculate the number of 90-degree steps
            int rotationSteps = Mathf.RoundToInt(transform.eulerAngles.y / 90f) % 4;

            // Rotate indices based on the rotation steps
            for (int i = 0; i < rotationSteps; i++)
            {
                int temp = indices[0];
                indices[0] = indices[1];
                indices[1] = indices[3];
                indices[3] = indices[2];
                indices[2] = temp;
            }

            return indices;
        }

        public void Activate()
        {
            transform.localScale = Vector3.zero;
            gameObject.SetActive(true);

            transform.DOJump(transform.position, 2f, 1, 1f)
                .SetEase(Ease.OutBounce);

            transform.DOScale(Vector3.one, 1f)
                .SetEase(Ease.OutBounce)
                .OnComplete(() =>
                {
                    IsUnlocked = true;
                    OnActivated?.Invoke();
                    StartCoroutine(InitializeProps(false));
                });
        }
    }
}
