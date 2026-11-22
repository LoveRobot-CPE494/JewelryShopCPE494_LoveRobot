using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
namespace JewelryShop.Storefront
{
    /// <summary>The Payment Card on the Checkout Counter (#87): grabbed with a Controller, it stays
    /// where it is dropped. It returns to its home spot on Approved, or when it leaves the
    /// playable area (#92). The Card Terminal's zone counts a tap only for this card.</summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class PaymentCard : MonoBehaviour
    {
        [Tooltip("Home spot on the Checkout Counter. Unset: the card never returns, with a warning.")]
        [SerializeField] private Transform _home;
        [Tooltip("World-space playable area. Zero size = unset: the card is never out of area.")]
        [SerializeField] private Bounds _playableArea;

        private bool _warned;

        /// <summary>True when the area is set (non-zero size) and the point lies outside it.</summary>
        public static bool IsOutside(Bounds area, Vector3 point) =>
            area.size != Vector3.zero && !area.Contains(point);

        private void Update()
        {
            if (IsOutside(_playableArea, transform.position)) ReturnHome();
        }

        /// <summary>Lets go of the card if held, stops it, and puts it on its home spot.</summary>
        public void ReturnHome()
        {
            if (_home == null)
            {
                if (!_warned) Debug.LogWarning("PaymentCard: home spot is unwired; the card will not return.", this);
                _warned = true;
                return;
            }
            var grab = GetComponent<XRGrabInteractable>();
            if (grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            var body = GetComponent<Rigidbody>(); // kinematic while held: velocity is not settable
            if (body != null && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            transform.SetPositionAndRotation(_home.position, _home.rotation);
            Physics.SyncTransforms(); // so the Rigidbody does not carry the old pose into the next step
        }
    }
}
