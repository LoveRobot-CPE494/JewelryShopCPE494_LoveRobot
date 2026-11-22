using System.Collections.Generic;
using JewelryShop.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace JewelryShop.Storefront
{
    /// <summary>The Order Tray on the Checkout Counter: a held Gem Model dropped into its
    /// trigger collider adds that Gem to the Order and shows one token; the Gem Model
    /// flies home at once, added or refused (#78). A token pulled out and released outside
    /// the tray removes its Gem; released inside, it returns to its slot (#79).</summary>
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

        private readonly List<GameObject> _tokens = new List<GameObject>();
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

        private void OnEnable() => Order.Changed += RebuildTokens;
        private void OnDisable() => Order.Changed -= RebuildTokens;

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<OrderToken>() != null) return; // a token is not a Gem drop
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

        // shortcut: rebuilds every token on each change; fine for <= 30 Gems, upgrade if Orders grow.
        private void RebuildTokens()
        {
            foreach (var old in _tokens) Destroy(old);
            _tokens.Clear();
            for (int i = 0; i < Order.Lines.Count; i++)
            {
                var token = _tokenPrefab != null
                    ? Instantiate(_tokenPrefab, _tokenRoot)
                    : MakeFallbackToken(Order.Lines[i].Key);
                token.transform.localPosition = SlotPosition(i);
                MakeGrabbable(token, i);
                _tokens.Add(token);
            }
        }

        private Vector3 SlotPosition(int index) => new Vector3(
            (index % _tokensPerRow) * _tokenSpacingM,
            _tokenSizeM,
            (index / _tokensPerRow) * _tokenSpacingM);

        private void MakeGrabbable(GameObject token, int index)
        {
            token.AddComponent<OrderToken>().Index = index;
            if (token.GetComponentInChildren<Collider>() == null) token.AddComponent<BoxCollider>();
            var body = token.GetComponent<Rigidbody>();
            if (body == null) body = token.AddComponent<Rigidbody>();
            body.isKinematic = true;
            var grab = token.GetComponent<XRGrabInteractable>();
            if (grab == null) grab = token.AddComponent<XRGrabInteractable>();
            grab.selectExited.AddListener(_ => OnTokenReleased(token));
        }

        /// <summary>Let go outside the tray removes that Gem; inside, the token goes back to its slot.</summary>
        private void OnTokenReleased(GameObject token)
        {
            int index = token.GetComponent<OrderToken>().Index;
            if (GetComponent<Collider>().bounds.Contains(token.transform.position))
            {
                token.transform.SetParent(_tokenRoot, true);
                token.transform.localPosition = SlotPosition(index);
                token.transform.localRotation = Quaternion.identity;
            }
            else
            {
                Order.RemoveAt(index); // Changed rebuilds the tokens, destroying this one
            }
        }

        private GameObject MakeFallbackToken(GemKey key)
        {
            var token = GameObject.CreatePrimitive(
                key.Cut == GemCut.Round ? PrimitiveType.Sphere : PrimitiveType.Cube);
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
