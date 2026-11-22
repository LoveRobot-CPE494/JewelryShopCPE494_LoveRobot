using System;

namespace JewelryShop.Core
{
    /// <summary>The Hub's answer to a submitted Order (ADR-0012).</summary>
    public enum SubmitReply { Accepted, Declined }

    /// <summary>What Checkout hands the Order to and waits on for a reply (ADR-0012). The reply
    /// may come at once or later, from any call after Submit returns.</summary>
    public interface IOrderSubmitter
    {
        /// <summary>Send the Order; <paramref name="onReply"/> gets the answer.</summary>
        void Submit(Order order, Action<SubmitReply> onReply);
    }

    /// <summary>Local stand-in until the Hub link exists (#75): accepts every Order at once.</summary>
    public class LocalOrderSubmitter : IOrderSubmitter
    {
        public void Submit(Order order, Action<SubmitReply> onReply) => onReply(SubmitReply.Accepted);
    }
}
