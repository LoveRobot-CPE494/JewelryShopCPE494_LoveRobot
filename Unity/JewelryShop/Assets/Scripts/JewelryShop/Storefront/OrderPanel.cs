using JewelryShop.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace JewelryShop.Storefront
{
    /// <summary>The floating panel above the Order Tray: one row per Gem with its name and
    /// Price, the Order total, or a hint while the Order is empty (#80). Redraws from the
    /// Order's Changed event, never by polling.</summary>
    public class OrderPanel : MonoBehaviour
    {
        [SerializeField] private OrderTray _tray;
        [FormerlySerializedAs("_names")]
        [SerializeField] private TMP_Text _nameColumn;
        [FormerlySerializedAs("_prices")]
        [SerializeField] private TMP_Text _priceColumn;
        [SerializeField] private TMP_Text _total;
        [SerializeField] private TMP_Text _hint;

        private void OnEnable()
        {
            if (_tray == null || _nameColumn == null || _priceColumn == null || _total == null || _hint == null)
            {
                Debug.LogWarning("OrderPanel: tray or a text field is unwired; panel stays blank.", this);
                return;
            }
            _hint.text = OrderPanelText.EmptyHint;
            _tray.Order.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_tray != null) _tray.Order.Changed -= Refresh;
        }

        private void Refresh()
        {
            var order = _tray.Order;
            var rows = OrderPanelText.Rows(order, _tray.NameOf);
            _nameColumn.text = OrderPanelText.NameColumn(rows);
            _priceColumn.text = OrderPanelText.PriceColumn(rows);
            _total.text = OrderPanelText.TotalText(order);
            bool empty = order.Lines.Count == 0;
            _hint.gameObject.SetActive(empty);
            _total.gameObject.SetActive(!empty);
        }
    }
}
