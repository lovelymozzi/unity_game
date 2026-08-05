using System.Collections;
using System.Linq;
using UnityEngine;

namespace CryingSnow.FarmingIsland
{
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class Wool : MonoBehaviour
    {
        [SerializeField, Range(0.1f, 0.5f)]
        [Tooltip("Radius around the shear point where vertices will be affected.")]
        private float shearRadius = 0.1f;

        [SerializeField, Range(0.1f, 1f)]
        [Tooltip("Minimum percentage of wool that must be sheared before the process is complete.")]
        private float minShearAmount = 0.9f;

        [SerializeField]
        [Tooltip("Sound clip played when the wool is being sheared.")]
        private AudioClip razorSound;

        private SkinnedMeshRenderer skinnedMeshRenderer;
        private MeshCollider woolCollider;
        private ParticleSystem woolParticle;
        private Camera mainCamera;
        private int woolLayer;

        void Awake()
        {
            // Get references to required components
            skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
            woolCollider = GetComponent<MeshCollider>();

            // Attempt to get the particle system component from the children
            woolParticle = GetComponentInChildren<ParticleSystem>();
            if (!woolParticle)
                Debug.LogWarning("Wool particle not found!");

            // Disable the collider initially
            woolCollider.enabled = false;
        }

        void Start()
        {
            // Cache the main camera and set the layer for the wool
            mainCamera = Camera.main;
            gameObject.layer = LayerMask.NameToLayer("Wool");
            woolLayer = 1 << gameObject.layer;
        }

        public IEnumerator Shear()
        {
            // Create a shearable version of the wool
            GameObject shearableWool = CreateShearableWool(out Mesh woolMesh);
            Vector3[] movedVertices = woolMesh.vertices;

            // Set the blend shape weight to maximum for shearing effect
            skinnedMeshRenderer.SetBlendShapeWeight(0, 100f);

            // Prepare a mesh to capture the state of wool after shearing
            Mesh sheared = new Mesh();
            skinnedMeshRenderer.BakeMesh(sheared);
            Vector3[] targetVertices = sheared.vertices;
            bool[] vertexMoved = new bool[movedVertices.Length];

            // Temporarily disable the skinned mesh renderer and enable the collider
            skinnedMeshRenderer.enabled = false;
            woolCollider.enabled = true;

            // Create an audio source to play the shearing sound
            var audioSourceObj = new GameObject("Razor Audio Source");
            var audioSource = audioSourceObj.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.clip = razorSound;

            // Hide the HUD and show the progress bar
            UIManager.Instance.ToggleHUD(false);
            UIManager.Instance.ProgressBar.Toggle("Shear the wool!");

            float shearProgress = 0f;

            while (shearProgress < minShearAmount)
            {
                // Calculate the shear progress based on moved vertices
                shearProgress = vertexMoved.Count(moved => moved) / (float)vertexMoved.Length;

                // Get the mouse position and cast a ray to check for intersections with the wool
                Vector3 mousePos = Input.mousePosition;
                Ray ray = mainCamera.ScreenPointToRay(mousePos);

                if (Input.GetMouseButton(0) && Physics.Raycast(ray, out RaycastHit hit, 100f, woolLayer))
                {
                    Vector3 hitPoint = hit.point;

                    // Play and position the particle system
                    if (!woolParticle.isPlaying) woolParticle.Play();
                    woolParticle.transform.position = hitPoint;
                    woolParticle.transform.forward = hit.normal;

                    // Play the shearing sound
                    if (!audioSource.isPlaying)
                    {
                        audioSource.loop = true;
                        audioSource.Play();
                    }

                    // Define a bounding box for shearing
                    Bounds shearBounds = new Bounds(hitPoint, Vector3.one * shearRadius * 2);
                    for (int i = 0; i < movedVertices.Length; i++)
                    {
                        if (!vertexMoved[i])
                        {
                            Vector3 worldVertex = transform.TransformPoint(movedVertices[i]);

                            if (shearBounds.Contains(worldVertex))
                            {
                                movedVertices[i] = targetVertices[i];
                                vertexMoved[i] = true;
                            }
                        }
                    }

                    // Update the mesh and recalculate normals
                    woolMesh.vertices = movedVertices;
                    woolMesh.RecalculateNormals();
                }
                else
                {
                    // Stop particle system and audio if not shearing
                    if (woolParticle.isPlaying) woolParticle.Stop();
                    audioSource.loop = false;
                }

                // Update progress bar
                float progress = shearProgress;
                if (progress > 0f) progress += 1 - minShearAmount;
                UIManager.Instance.ProgressBar.UpdateDisplay(progress);

                yield return null;
            }

            // Short delay after finishing the shearing (wait for Pose animation transition)
            yield return new WaitForSeconds(0.3f);

            // Restore HUD and clean up
            UIManager.Instance.ProgressBar.Toggle("");
            UIManager.Instance.ToggleHUD(true);

            woolCollider.enabled = false;

            audioSource.loop = false;
            Destroy(audioSourceObj, 1f);
            woolParticle.Stop();

            Destroy(shearableWool);
        }

        private GameObject CreateShearableWool(out Mesh woolMesh)
        {
            // Create a new GameObject for the shearable wool
            var woolObj = new GameObject("Shearable Wool");
            woolObj.transform.SetParent(transform.parent);
            woolObj.transform.localPosition = Vector3.zero;
            woolObj.transform.localRotation = transform.localRotation;

            // Create a mesh for the shearable wool and assign it to the new GameObject
            Mesh mesh = new Mesh();
            skinnedMeshRenderer.BakeMesh(mesh);
            var filter = woolObj.AddComponent<MeshFilter>();
            filter.mesh = mesh;
            var meshRenderer = woolObj.AddComponent<MeshRenderer>();
            meshRenderer.material = skinnedMeshRenderer.material;

            woolMesh = mesh;

            return woolObj;
        }

        public void GrowWool()
        {
            // Reset the blend shape weight and enable the skinned mesh renderer
            skinnedMeshRenderer.SetBlendShapeWeight(0, 0f);
            skinnedMeshRenderer.enabled = true;
        }
    }
}
