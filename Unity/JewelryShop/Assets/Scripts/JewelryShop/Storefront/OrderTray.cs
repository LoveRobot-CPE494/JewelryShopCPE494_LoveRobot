using JewelryShop.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace JewelryShop.Storefront
{
    /// <summary>The Order Tray on the Checkout Counter: a held Gem Model dropped into its
    /// trigger collider adds that Gem to the Order and shows one token; the Gem Model
    /// flies home at once, added or refused (#78).</summary>
    [RequireComponent(typeof(Collider))]
    public class OrderTray : MonoBehaviour
    {
        [Tooltip("Most Gems one Order may hold.")]
        [Min(1)]
        [SerializeField] private int _maxGems = Order.DefaultMaxGems;
        [Tooltip("One token per Gem; a tinted primitive is used when unwired.")]
        [SerializeField] private GameObject _tokenPrefab;
        [Tooltip("Where tokens appear; the tray itself when unwired.")]
        [SerializeField] private Transform _tokenRoot;
        [Tooltip("Token edge length, metres.")]
        [SerializeField] private float _tokenSizeM = 0.03f;
        [Tooltip("Gap between token centres, metres.")]
        [SerializeField] private float _tokenSpacingM = 0.04f;
        [SerializeField] private int _tokensPerRow = 10;

        private Order _order;
        private GemModel _lastDropped;
        private int _lastDropFrame = -1;

        /// <summary>The Order being built; created on first use.</summary>
        public Order Order => _order ??= new Order(_maxGems);

        private void Awake()
        {
            if (!GetComponent<Collider>().isTrigger)
                Debug.LogWarning("OrderTray: collider is not a trigger; drops unseen.", this);
            if (_tokenRoot == null) _tokenRoot = transform;
        }

        private void OnEnable() => Order.Changed += ShowNewestToken;
        private void OnDisable() => Order.Changed -= ShowNewestToken;

        private void OnTriggerEnter(Collider other)
        {
            var grab = other.GetComponentInParent<XRGrabInteractable>();
            var gem = other.GetComponentInParent<GemModel>();
            if (gem == null || grab == null || !grab.isSelected) return; // only a held Gem Model
            // A Gem Model with several colliders enters once per collider in one step.
            if (gem == _lastDropped && Time.frameCount == _lastDropFrame) return;
            _lastDropped = gem;
            _lastDropFrame = Time.frameCount;
            // Let go first: its release must not undo the immediate return below.
            if (grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection(
                    (IXRSelectInteractable)grab);
            if (gem.TryGetKey(out var key))
                Order.Add(key, gem.Entry.PriceBaht, gem.Entry.StartingStockCount);
            // shortcut: refused drops go home too; the AddResult is dropped until the
            // refusal-feedback ticket (001-06) needs it.
            gem.ReturnHomeNow();
        }

        private void ShowNewestToken()
        {
            int index = Order.Lines.Count - 1;
            var key = Order.Lines[index].Key;
            var token = _tokenPrefab != null
                ? Instantiate(_tokenPrefab, _tokenRoot)
                : MakeFallbackToken(key);
            token.transform.localPosition = new Vector3(
                (index % _tokensPerRow) * _tokenSpacingM,
                _tokenSizeM,
                (index / _tokensPerRow) * _tokenSpacingM);
        }

        private GameObject MakeFallbackToken(GemKey key)
        {
            var token = GameObject.CreatePrimitive(
                key.Cut == GemCut.Round ? PrimitiveType.Sphere : PrimitiveType.Cube);
            Destroy(token.GetComponent<Collider>()); // must not re-trigger the tray
            token.transform.SetParent(_tokenRoot, false);
            token.transform.localScale = Vector3.one * _tokenSizeM;
            token.GetComponent<Renderer>().material.color = ColorOf(key.Color);
            return token;
        }

        private static Color ColorOf(GemColor color) => color switch
        {
            GemColor.Red => Color.red,
            GemColor.Blue => Color.blue,
            GemColor.Clear => Color.white,
            GemColor.Pink => new Color(1f, 0.5f, 0.7f),
            GemColor.Purple => new Color(0.6f, 0.2f, 0.8f),
            GemColor.Green => Color.green,
            GemColor.Yellow => Color.yellow,
            _ => new Color(1f, 0.5f, 0f),
        };
    }
}
