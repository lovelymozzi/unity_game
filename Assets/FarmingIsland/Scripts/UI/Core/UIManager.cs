using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SimpleInputNamespace;
using DG.Tweening;

namespace CryingSnow.FarmingIsland
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [SerializeField, Tooltip("The virtual joystick used for movement input.")]
        private Joystick joystick;

        [SerializeField, Tooltip("Button used for triggering activity-related actions.")]
        private Button activityButton;

        [SerializeField, Tooltip("Displays the progress of an action.")]
        private ProgressBar progressBar;

        [SerializeField, Tooltip("Handles the line tension for fishing mini game.")]
        private LineTension lineTension;

        [SerializeField, Tooltip("Displays the minimap on the screen.")]
        private Minimap minimap;

        [SerializeField, Tooltip("Button used to toggle the minimap display.")]
        private Button minimapButton;

        [SerializeField, Tooltip("Image used to fade screen in and out.")]
        private Image screenFader;

        [SerializeField, Tooltip("List of UI elements representing the HUD (hidden when doing activities).")]
        private List<GameObject> huds;

        [Header("Inventories")]
        [SerializeField, Tooltip("Prefab for displaying item information.")]
        private ItemInfo itemInfoPrefab;

        [SerializeField, Tooltip("Container for holding item information UI elements.")]
        private Transform itemInfoContainer;

        [Header("Farming")]
        [SerializeField, Tooltip("Button for triggering farming-related actions.")]
        private Button farmingButton;

        [SerializeField, Tooltip("Icons representing different farming states.")]
        private Sprite[] farmingIcons;

        [SerializeField, Tooltip("Displays information about the silo.")]
        private SiloInfo siloInfo;

        [SerializeField, Tooltip("Prefab for displaying a 'full' message (when silo's inventory is full).")]
        private FullMessage fullMessagePrefab;

        [SerializeField, Tooltip("Handles upgrading the silo.")]
        private SiloUpgrade siloUpgrade;

        [SerializeField, Tooltip("Handles upgrading animals.")]
        private AnimalUpgrade animalUpgrade;

        [SerializeField, Tooltip("Prefab for displaying product information.")]
        private ProductInfo productInfoPrefab;

        [SerializeField, Tooltip("Prefab for displaying merchant information.")]
        private MerchantInfo merchantInfoPrefab;

        // Public Getters
        public SiloInfo SiloInfo => siloInfo;
        public SiloUpgrade SiloUpgrade => siloUpgrade;
        public AnimalUpgrade AnimalUpgrade => animalUpgrade;
        public ProgressBar ProgressBar => progressBar;
        public LineTension LineTension => lineTension;
        public Minimap Minimap => minimap;

        // List of item info UI elements currently being used. This list is dynamically updated based on the number of items.
        private List<ItemInfo> itemInfos = new List<ItemInfo>();

        // Stores the last recorded position of the joystick to calculate shake intensity in tree mini game.
        private Vector2 lastJoystickPosition;

        // Lazy-loaded property to get the Canvas component from the children, initializing it only when accessed.
        private Canvas _canvas;
        private Canvas canvas
        {
            get
            {
                if (_canvas == null) _canvas = GetComponentInChildren<Canvas>(true);
                return _canvas;
            }
        }

        void Awake()
        {
            Instance = this;

            // Fade out the screen with a 1-second delay to ensure the scene has fully loaded.
            screenFader.gameObject.SetActive(true);
            screenFader.DOFade(0f, 1f)
                .SetDelay(1f)
                .OnComplete(() =>
                {
                    screenFader.gameObject.SetActive(false);
                });
        }

        void OnEnable()
        {
            minimapButton.onClick.AddListener(() => minimap.Show());
        }

        void OnDisable()
        {
            minimapButton.onClick.RemoveAllListeners();
        }

        public IEnumerator FadeInScreen()
        {
            screenFader.gameObject.SetActive(true);
            yield return screenFader.DOFade(1f, 1f).WaitForCompletion();
        }

        /// <summary>
        /// Toggles the farming button's visibility and sets its click action based on the farm's state.
        /// Updates the button's icon to match the given farm state and assigns a click action that hides the button after invocation.
        /// </summary>
        /// <param name="farmState">The current state of the farm used to determine the button's icon.</param>
        /// <param name="farmingAction">The action to be invoked when the button is clicked.</param>
        public void ToggleFarmingButton(Farm.State farmState, System.Action farmingAction)
        {
            // Get the Image component from the first child of the farmingButton (assuming it contains the icon)
            var iconImage = farmingButton.transform.GetChild(0).GetComponent<Image>();

            // Set the icon of the farming button based on the current state of the farm
            iconImage.sprite = farmingIcons[(int)farmState];

            // Remove any existing listeners from the farming button's onClick event
            farmingButton.onClick.RemoveAllListeners();

            // Add a new listener to the farming button's onClick event that invokes the provided farmingAction
            // and then hides the button
            farmingButton.onClick.AddListener(() =>
            {
                farmingAction.Invoke();
                farmingButton.gameObject.SetActive(false);
            });

            // Make the farming button visible
            farmingButton.gameObject.SetActive(true);
        }

        /// <summary>
        /// Disables the farming button by hiding it from view.
        /// This method is used to hide the farming button when it's not needed or when the farm's state does not require it.
        /// </summary>
        public void DisableFarmingButton()
        {
            // Hide the farming button, making it inactive and not visible
            farmingButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// Updates the display of item information in the UI based on the provided list of items.
        /// This method ensures that the number of item info displays matches the number of items.
        /// It handles the creation of new item info displays, the removal of excess ones, and the
        /// updating of existing item info displays with the latest data from the items list.
        /// </summary>
        /// <param name="items">The list of items whose information should be updated.</param>
        public void UpdateItemInfos(List<Item> items)
        {
            // Remove excess item infos if there are too many
            while (itemInfos.Count > items.Count)
            {
                var lastInfo = itemInfos[itemInfos.Count - 1];
                itemInfos.Remove(lastInfo);
                Destroy(lastInfo.gameObject);
            }

            // Ensure itemInfos matches items count
            while (itemInfos.Count < items.Count)
            {
                var info = Instantiate(itemInfoPrefab, itemInfoContainer);
                itemInfos.Add(info);
            }

            // Update each item info
            for (int i = 0; i < items.Count; i++)
            {
                itemInfos[i].Init(items[i].Data);
                itemInfos[i].UpdateInfo(items[i]);
            }
        }

        /// <summary>
        /// Toggles the visibility of all HUD (Heads-Up Display) elements.
        /// This method iterates through the list of HUD elements and sets their active state based on the
        /// provided boolean value. If <paramref name="active"/> is true, all HUD elements are enabled;
        /// otherwise, they are disabled.
        /// </summary>
        /// <param name="active">True to show the HUD elements; false to hide them.</param>
        public void ToggleHUD(bool active)
        {
            // Iterate through each HUD element and set its active state based on the 'active' parameter.
            huds.ForEach(hud => hud.SetActive(active));
        }

        /// <summary>
        /// Resets the joystick input and sets its last position to zero.
        /// This method triggers the joystick's pointer up event to release any ongoing input
        /// and resets the last known joystick position to zero. This is useful for stopping
        /// joystick-based actions or clearing the joystick state when necessary.
        /// </summary>
        public void ReleaseJoystick()
        {
            // Trigger the joystick's pointer up event to release any ongoing input.
            joystick.OnPointerUp(null);

            // Reset the last known joystick position to zero.
            lastJoystickPosition = Vector3.zero;
        }

        /// <summary>
        /// Calculates the intensity of joystick shake based on the current and previous joystick positions.
        /// This method computes the distance between the current joystick position and the last recorded
        /// position to determine the shake intensity. The last joystick position is then updated to the
        /// current position for the next calculation.
        /// </summary>
        /// <returns>The intensity of the joystick shake as a float value.</returns>
        public float GetJoystickShakeIntensity()
        {
            // Get the current joystick position.
            Vector2 currentJoystickPosition = joystick.Value;

            // Calculate the distance between the current position and the last recorded position.
            float shakeIntensity = Vector2.Distance(currentJoystickPosition, lastJoystickPosition);

            // Update the last joystick position to the current position.
            lastJoystickPosition = currentJoystickPosition;

            return shakeIntensity;
        }

        /// <summary>
        /// Configures and toggles the activity button based on the provided icon and action.
        /// This method sets the activity button's visibility based on whether the provided
        /// <paramref name="activity"/> action is null. If an action is provided, the button is made visible,
        /// its icon is set, and a click listener is added to invoke the action when the button is pressed.
        /// If no action is provided, the button is hidden.
        /// </summary>
        /// <param name="icon">The sprite to display on the activity button.</param>
        /// <param name="activity">The action to invoke when the activity button is clicked. If null, the button is hidden.</param>
        public void ToggleActivityButton(Sprite icon, System.Action activity)
        {
            // Set the activity button's visibility based on whether the 'activity' action is null.
            activityButton.gameObject.SetActive(activity != null);

            // If no action is provided, exit the method.
            if (activity == null) return;

            // Set the button's icon to the provided sprite.
            activityButton.transform.GetChild(0).GetComponent<Image>().sprite = icon;

            // Remove any existing click listeners and add a new listener to invoke the provided action.
            activityButton.onClick.RemoveAllListeners();
            activityButton.onClick.AddListener(() =>
            {
                activity.Invoke();
            });
        }

        /// <summary>
        /// Sets the joystick's active state.
        /// This method enables or disables the joystick based on the provided <paramref name="active"/> parameter.
        /// When <paramref name="active"/> is true, the joystick is shown; otherwise, it is hidden.
        /// </summary>
        /// <param name="active">True to show the joystick; false to hide it.</param>
        public void SetActiveJoystick(bool active)
        {
            // Set the joystick's active state based on the 'active' parameter.
            joystick.gameObject.SetActive(active);
        }

        /// <summary>
        /// Instantiates and displays a full message UI element at the specified position.
        /// This method creates a <see cref="FullMessage"/> UI element from the prefab and sets its position
        /// in the canvas based on the provided <paramref name="displayPosition"/>. The new message is then
        /// initialized and set to be the first sibling in the canvas hierarchy to ensure it appears on top.
        /// </summary>
        /// <param name="displayPosition">The world position where the full message should be displayed.</param>
        /// <returns>The instantiated <see cref="FullMessage"/> component.</returns>
        public FullMessage DisplayFullMessage(Vector3 displayPosition)
        {
            // Instantiate the FullMessage prefab and set its parent to the canvas.
            var fullMessage = Instantiate(fullMessagePrefab, canvas.transform);

            // Set the new FullMessage as the first sibling in the canvas hierarchy.
            fullMessage.transform.SetAsFirstSibling();

            // Initialize the FullMessage with the specified display position.
            fullMessage.Initialize(displayPosition);

            return fullMessage;
        }

        /// <summary>
        /// Instantiates and displays a product info UI element at a specific position relative to the animal.
        /// This method creates a <see cref="ProductInfo"/> UI element from the prefab and sets its position
        /// in the canvas relative to the <paramref name="animal"/> transform. The new product info is then
        /// initialized with the provided <paramref name="offset"/> and <paramref name="icon"/>. The product info
        /// element is set to be the first sibling in the canvas hierarchy to ensure it appears on top.
        /// </summary>
        /// <param name="animal">The transform of the animal to which the product info is related.</param>
        /// <param name="offset">The position offset from the animal's transform for the product info display.</param>
        /// <param name="icon">The sprite icon to display on the product info UI element.</param>
        /// <returns>The instantiated <see cref="ProductInfo"/> component.</returns>
        public ProductInfo DisplayProductInfo(Transform animal, Vector3 offset, Sprite icon)
        {
            // Instantiate the ProductInfo prefab and set its parent to the canvas.
            var productInfo = Instantiate(productInfoPrefab, canvas.transform);

            // Set the new ProductInfo as the first sibling in the canvas hierarchy.
            productInfo.transform.SetAsFirstSibling();

            // Initialize the ProductInfo with the specified animal transform, offset, and icon.
            productInfo.Initialize(animal, offset, icon);

            return productInfo;
        }

        /// <summary>
        /// Instantiates and displays a merchant info UI element.
        /// This method creates a <see cref="MerchantInfo"/> UI element from the prefab and sets its position
        /// in the canvas based on the provided <paramref name="merchant"/>. The new merchant info is then
        /// initialized with the provided merchant data and set to be the first sibling in the canvas hierarchy
        /// to ensure it appears on top.
        /// </summary>
        /// <param name="merchant">The merchant data to initialize the merchant info UI element.</param>
        /// <returns>The instantiated <see cref="MerchantInfo"/> component.</returns>
        public MerchantInfo DisplayMerchantInfo(Merchant merchant)
        {
            // Instantiate the MerchantInfo prefab and set its parent to the canvas.
            var merchantInfo = Instantiate(merchantInfoPrefab, canvas.transform);

            // Set the new MerchantInfo as the first sibling in the canvas hierarchy.
            merchantInfo.transform.SetAsFirstSibling();

            // Initialize the MerchantInfo with the specified merchant data.
            merchantInfo.Initialize(merchant);

            return merchantInfo;
        }
    }
}
