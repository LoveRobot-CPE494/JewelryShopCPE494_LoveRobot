using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace JewelryShop.Storefront
{
    /// <summary>Marks the Payment Card on the Checkout Counter: grabbed with a Controller, it stays
    /// where it is dropped (#87). The Card Terminal's zone counts a tap only for this card.</summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class PaymentCard : MonoBehaviour { }
}
