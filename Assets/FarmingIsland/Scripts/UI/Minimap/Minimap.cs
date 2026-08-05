using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CryingSnow.FarmingIsland
{
    public class Minimap : MonoBehaviour
    {
        [Tooltip("Size of each tile on the minimap.")]
        [SerializeField] private Vector2 minimapTileSize = new Vector2(100, 100);

        [Tooltip("Color used for island tiles.")]
        [SerializeField] private Color islandColor = Color.white;

        [Tooltip("Color used for bridge tiles.")]
        [SerializeField] private Color bridgeColor = Color.white;

        [Tooltip("Content area where minimap elements are placed.")]
        [SerializeField] private Transform minimapContent;

        [Tooltip("RectTransform representing the player's icon on the minimap.")]
        [SerializeField] private RectTransform playerIcon;

        [Tooltip("Text component for displaying travel message using boat.")]
        [SerializeField] private TMP_Text travelMessage;

        [Tooltip("Sprites used for different prop icons (IProp.Location).")]
        [SerializeField] private Sprite[] propSprites;

        private int islandSize => IslandManager.Instance.IslandSize;
        private List<Image> islandTiles = new List<Image>();
        private List<TMP_Text> purchaserIcons = new List<TMP_Text>();
        private List<Image> propIcons = new List<Image>();
        private List<DockButton> dockButtons = new List<DockButton>();

        private PlayerController _player;
        private PlayerController player
        {
            get
            {
                if (_player == null)
                    _player = FindFirstObjectByType<PlayerController>();

                return _player;
            }
        }

        public void Show(bool allowTravel = false)
        {
            gameObject.SetActive(true);
            player.IsControllable = false;

            CreateIslandTiles();
            CreatePropLocations();
            CreatePurchaserIcons();

            dockButtons.ForEach(db => db.enabled = allowTravel);
            travelMessage.gameObject.SetActive(allowTravel);

            playerIcon.anchoredPosition = GetMinimapPosition(player.transform.position);
            playerIcon.SetAsLastSibling();

            AudioManager.Instance.PlaySFX(AudioID.UI_Select);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            player.IsControllable = true;

            AudioManager.Instance.PlaySFX(AudioID.UI_Decline);
        }

        private void CreateIslandTiles()
        {
            var islands = IslandManager.Instance.UnlockedIslands;

            while (islandTiles.Count < islands.Count)
            {
                var island = islands[islandTiles.Count];
                var islandTileObj = new GameObject(island.name + " Tile");
                islandTileObj.transform.SetParent(minimapContent, false);

                var islandTile = islandTileObj.AddComponent<Image>();
                islandTile.rectTransform.anchoredPosition = GetMinimapPosition(island.GetPosition());
                islandTile.rectTransform.sizeDelta = minimapTileSize;
                islandTile.color = island.IsBridge ? bridgeColor : islandColor;

                islandTiles.Add(islandTile);
            }
        }

        private void CreatePropLocations()
        {
            var propLocations = IslandManager.Instance.PropLocations;

            while (propIcons.Count < propLocations.Count)
            {
                var propLocation = propLocations[propIcons.Count];
                var propIconObj = new GameObject($"{propLocation.Item2} {propLocation.Item1}");
                propIconObj.transform.SetParent(minimapContent, false);

                var propIcon = propIconObj.AddComponent<Image>();
                propIcon.rectTransform.anchoredPosition = GetMinimapPosition(propLocation.Item1);
                propIcon.rectTransform.sizeDelta = minimapTileSize * 0.8f;
                propIcon.sprite = propSprites[(int)propLocation.Item2];

                propIcons.Add(propIcon);

                if (propLocation.Item2 == Location.Dock)
                {
                    var dockButton = propIconObj.AddComponent<DockButton>();

                    dockButton.Initialize(() =>
                    {
                        var destination = propLocation.Item1 + (Vector3.forward * islandSize);
                        player.Teleport(destination);
                        Hide();
                    });

                    dockButtons.Add(dockButton);
                }
            }
        }

        private void CreatePurchaserIcons()
        {
            var purchasers = IslandManager.Instance.Purchasers;
            purchaserIcons.ForEach(icon => Destroy(icon.gameObject));
            purchaserIcons.Clear();
            foreach (var purchaser in purchasers)
            {
                var purchaserObj = new GameObject($"Purchaser Icon{purchaser}");
                purchaserObj.transform.SetParent(minimapContent, false);
                var purchaserIcon = purchaserObj.AddComponent<TextMeshProUGUI>();
                purchaserIcon.text = "$";
                purchaserIcon.alignment = TextAlignmentOptions.CenterGeoAligned;
                purchaserIcon.fontStyle = FontStyles.Bold;
                purchaserIcon.fontSize = 100;
                purchaserIcon.rectTransform.anchoredPosition = GetMinimapPosition(purchaser.Position);
                purchaserIcon.rectTransform.sizeDelta = minimapTileSize;
                purchaserIcons.Add(purchaserIcon);
            }
        }

        private Vector2 GetMinimapPosition(Vector3 position)
        {
            Vector2 minimapPosition = new Vector2(position.x, position.z);
            return minimapPosition * (minimapTileSize / islandSize);
        }
    }
}
