using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace JewelryShop.Storefront
{
    /// <summary>The Card Terminal's tap zone: a trigger that counts a tap when the Payment
    /// Card enters it while a Controller holds it.</summary>
    [RequireComponent(typeof(Collider))]
    public class CardTerminalZone : MonoBehaviour
    {
        /// <summary>Raised on each tap.</summary>
        public event Action Tapped;

        private int _lastTapFrame = -1;

        private void Awake()
        {
            if (!GetComponent<Collider>().isTrigger)
                Debug.LogWarning("CardTerminalZone: collider is not a trigger; taps unseen.", this);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PaymentCard>() == null) return;
            var grab = other.GetComponentInParent<XRGrabInteractable>();
            if (grab == null || !grab.isSelected) return; // only a held card
            if (Time.frameCount == _lastTapFrame) return; // one tap per card, though it has several colliders
            _lastTapFrame = Time.frameCount;
            Tapped?.Invoke();
        }
    }
}
