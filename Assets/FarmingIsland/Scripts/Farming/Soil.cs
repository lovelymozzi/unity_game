using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace CryingSnow.FarmingIsland
{
    [RequireComponent(typeof(MeshRenderer))]
    public class Soil : MonoBehaviour
    {
        public event System.Action OnStateChanged;
        public enum State { None, Seed, Sprout, Ripe, Harvest, Dead }
        private State state;

        private MeshRenderer soilRenderer;
        private Farm farm;
        private Color dryColor;
        private Color wetColor;

        private Quaternion originalPlantRotation;

        private Transform farmer;

        private void Awake()
        {
            soilRenderer = GetComponent<MeshRenderer>();
            gameObject.layer = LayerMask.NameToLayer("Soil");
        }

        public void Initialize(Farm farm)
        {
            this.farm = farm;

            // Get the integer X and Z coordinates of the soil's position by flooring the values.
            // This converts the soil's position to a grid-like system.
            int x = Mathf.FloorToInt(transform.position.x);
            int z = Mathf.FloorToInt(transform.position.z);

            // Calculate a grid index based on the sum of the X and Z coordinates.
            // Take the absolute value of the sum and then apply modulo 2 to create a checkerboard pattern.
            // The result will be either 0 or 1.
            int gridIndex = Mathf.Abs(x + z) % 2;

            // Retrieve the dry and wet soil colors based on the calculated grid index.
            // The index determines which color to use, creating a checkerboard effect.
            dryColor = farm.SoilDryColor(gridIndex);
            wetColor = farm.SoilWetColor(gridIndex);

            // Initially set the color of the soil renderer's material to the dry color.
            soilRenderer.material.color = dryColor;
        }

        public void DisableShadowCasting()
        {
            soilRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void OnTriggerEnter(Collider other)
        {
            // Check if the collider belongs to a player or farmer and if the current state is Harvest.
            // This ensures that the following code only runs when an appropriate entity enters the trigger and the soil is in harvest mode.
            if ((other.CompareTag("Player") || other.CompareTag("Farmer")) && state == State.Harvest)
            {
                // Get the first child of the current soil transform, which is assumed to be the plant.
                var plant = transform.GetChild(0);

                // Calculate the direction vector from the other object to the plant's position.
                Vector3 direction = plant.position - other.transform.position;

                // Normalize the direction vector to have a magnitude of 1, making it a direction vector rather than a position vector.
                direction.Normalize();

                // Create a Quaternion that represents the rotation needed to look in the direction of the calculated vector.
                Quaternion targetRotation = Quaternion.LookRotation(direction);

                // Adjust the target rotation to add a slight tilt to make the effect more visually appealing.
                targetRotation *= Quaternion.Euler(20f, 0f, 0f);

                // Apply a smooth rotation animation to the plant to the target rotation over 1 second using DOTween.
                plant?.DORotateQuaternion(targetRotation, 1f);
            }
        }

        void OnTriggerExit(Collider other)
        {
            // Check if the collider belongs to a player or farmer and if the current state is Harvest.
            // This ensures that the following code only runs when an appropriate entity exits the trigger and the soil is in harvest mode.
            if ((other.CompareTag("Player") || other.CompareTag("Farmer")) && state == State.Harvest)
            {
                // Get the first child of the current transform, which is assumed to be the plant.
                var plant = transform.GetChild(0);

                // Apply a smooth rotation animation to the plant to return to its original rotation over 1 second using DOTween.
                plant?.DORotateQuaternion(originalPlantRotation, 1f);
            }
        }

        public State GetSoilState() => state;

        public void SetSoilState(State state, Transform farmer)
        {
            this.state = state;
            OnStateChanged?.Invoke();

            this.farmer = farmer;

            if (state == State.Seed)
            {
                StartCoroutine(GrowSprout());
            }
            else if (state == State.Ripe)
            {
                StartCoroutine(GrowPlant());
            }
            else if (state == State.Dead)
            {
                StartCoroutine(HarvestPlant());
            }
        }

        IEnumerator GrowSprout()
        {
            // Get the time duration for the sprout to grow from the farm data.
            float growTime = farm.SproutGrowTime;

            // Instantiate a new sprout prefab at the position of this object (the soil).
            var sprout = Instantiate(farm.CropData.SproutPrefab, transform);

            // Set the initial scale of the sprout to zero (it starts invisible).
            sprout.transform.localScale = Vector3.zero;

            // Randomize the initial rotation of the sprout around the Y-axis.
            sprout.transform.rotation = Quaternion.Euler(Vector3.up * Random.Range(0, 360));

            // Wait for a short period before starting the scaling animation.
            yield return new WaitForSeconds(0.25f);

            // Animate the sprout's scale from zero to its final size (Vector3.one) over the specified grow time.
            yield return sprout.transform.DOScale(Vector3.one, growTime).WaitForCompletion();

            // Update the soil state to indicate that it now contains a sprout, and pass the farmer reference to use later.
            SetSoilState(State.Sprout, farmer);
        }

        IEnumerator GrowPlant()
        {
            // Animate the soil renderer's color to the wet color over the specified wet time.
            float soilWetTime = farm.SoilWetTime;
            yield return soilRenderer.material.DOColor(wetColor, soilWetTime).WaitForCompletion();

            // Destroy the existing sprout (assumed to be the first child of this transform).
            Destroy(transform.GetChild(0).gameObject);

            // Get the time duration for the plant to grow from the farm data.
            float growTime = farm.PlantGrowTime;

            // Instantiate a new plant prefab at the position of this object (the soil).
            var plant = Instantiate(farm.CropData.PlantPrefab, transform);

            // Set the initial scale of the plant to a smaller size.
            plant.transform.localScale = Vector3.one * 0.3f;

            // Randomize the initial rotation of the plant around the Y-axis and store the original rotation.
            plant.transform.rotation = Quaternion.Euler(Vector3.up * Random.Range(0, 360));
            originalPlantRotation = plant.transform.rotation;

            // Set the initial scale of all child transforms (crops) of the plant to zero.
            foreach (Transform crop in plant.transform)
            {
                crop.localScale = Vector3.zero;
            }

            // Animate the plant's scale from the initial size to full size over the specified grow time.
            yield return plant.transform.DOScale(Vector3.one, growTime).WaitForCompletion();

            // Animate each crop's scale from zero to full size over the crop grow time.
            foreach (Transform crop in plant.transform)
            {
                float time = farm.CropGrowTime;
                yield return crop.DOScale(Vector3.one, time).WaitForCompletion();
            }

            // Update the soil state to indicate that it is now ready for harvesting, and pass the farmer reference to use later.
            SetSoilState(State.Harvest, farmer);
        }

        IEnumerator HarvestPlant()
        {
            // Get the plant (assumed to be the first child of this object) and stop any existing animations on it.
            Transform plant = transform.GetChild(0);
            DOTween.Kill(plant);
            Destroy(plant.gameObject);

            // Change the soil color to indicate it's dry, over the specified dry time.
            soilRenderer.material.DOColor(dryColor, farm.SoilDryTime);

            // Determine the number of crops to yield randomly between 2 and 4.
            int yield = Random.Range(2, 5);
            List<GameObject> crops = new List<GameObject>();

            while (yield > 0)
            {
                // Spawn a crop object from the pool.
                var crop = PoolManager.Instance.SpawnObject(farm.CropData.Drop.name);

                // Set the crop's initial position to the soil's position.
                crop.transform.position = transform.position;

                // Generate a random position within a sphere around the soil for the crop to jump to.
                var randomPos = Random.insideUnitSphere * 1.5f;
                randomPos.y = 0f;

                // Animate the crop's jump to the random position and back to simulate a drop effect.
                crop.transform.DOJump(transform.position + randomPos, 1.5f, 1, 0.5f)
                    .SetEase(Ease.Linear)
                    .OnComplete(() =>
                        crop.transform.DOJump(crop.transform.position, 0.5f, 1, 0.5f)
                            .SetEase(Ease.OutBounce)
                );

                // Play a sound effect if the farmer is player.
                if (farmer.CompareTag("Player"))
                    AudioManager.Instance.PlaySFX(AudioID.Pop);

                crops.Add(crop);
                yield--;

                // Wait for a short random duration before spawning the next crop.
                yield return new WaitForSeconds(Random.Range(0.1f, 0.25f));
            }

            // Wait for a moment before starting the crop movement.
            yield return new WaitForSeconds(1f);

            // Move each crop to the farmer who harvested them (can be farmer NPC or player).
            crops.ForEach(crop => StartCoroutine(MoveCropToTarget(crop.transform, farmer)));

            // Update the soil state to indicate no plant is present.
            SetSoilState(State.None, farmer);
        }

        IEnumerator MoveCropToTarget(Transform crop, Transform target)
        {
            float duration = 1f;
            float elapsedTime = 0f;

            while (elapsedTime < duration && Vector3.Distance(crop.position, target.position + Vector3.up) > 0.1f)
            {
                // Calculate interpolation factor with ease-in effect.
                float t = elapsedTime / duration;
                t = 1f - Mathf.Pow(1f - t, 2);

                // Move the crop towards the target position, adjusting the height slightly.
                crop.position = Vector3.Lerp(crop.position, target.position + Vector3.up, t);

                elapsedTime += Time.deltaTime;

                yield return null;
            }

            // Return the crop to the object pool and add it to the target's inventory (farmer NPC or player).
            PoolManager.Instance.ReturnObject(crop.gameObject);
            target.GetComponent<Inventory>().AddItem(farm.CropData.ItemId);
        }
    }
}
