using UnityEngine;

namespace CryingSnow.FarmingIsland
{
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public class Teat : MonoBehaviour
    {
        public ParticleSystem MilkParticle { get; private set; } // Particle system for the milking effect.

        private SkinnedMeshRenderer skinnedMeshRenderer;
        private int blendShapeCount;
        private int startFrameIndex = 0; // Start index for blend shapes.
        private int endFrameIndex = 0; // End index for blend shapes.

        private void Awake()
        {
            MilkParticle = GetComponentInChildren<ParticleSystem>(); // Locate the milking particle system.
            skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>(); // Get the SkinnedMeshRenderer for controlling blend shapes.
            blendShapeCount = skinnedMeshRenderer.sharedMesh.blendShapeCount; // Total number of blend shapes available.
            endFrameIndex = blendShapeCount - 1; // Set the end frame to the last blend shape index.
        }

        // Sets blend shapes based on a normalized value (0 to 1).
        public void SetBlendShapes(float value)
        {
            // Calculate the exact frame based on the input value.
            float frameValue = value * (endFrameIndex - startFrameIndex);
            int currentFrameIndex = Mathf.FloorToInt(frameValue); // Determine the current blend shape index.
            int nextFrameIndex = Mathf.CeilToInt(frameValue); // Determine the next blend shape index.

            float blend = frameValue - currentFrameIndex; // Calculate the blend ratio between frames.

            // Reset all blend shapes before applying new values.
            for (int i = 0; i < blendShapeCount; i++)
            {
                skinnedMeshRenderer.SetBlendShapeWeight(i, 0);
            }

            // Set the weight of the current blend shape based on the calculated ratio.
            if (currentFrameIndex < blendShapeCount)
            {
                skinnedMeshRenderer.SetBlendShapeWeight(currentFrameIndex, (1 - blend) * 100);
            }

            // Set the weight of the next blend shape if applicable.
            if (nextFrameIndex < blendShapeCount)
            {
                skinnedMeshRenderer.SetBlendShapeWeight(nextFrameIndex, blend * 100);
            }
        }
    }
}
