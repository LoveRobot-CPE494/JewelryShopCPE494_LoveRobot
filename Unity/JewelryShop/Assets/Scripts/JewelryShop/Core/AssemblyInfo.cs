// JewelryShop.Core holds pure rules (order building, pricing, coordinate mapping) with no
// UnityEngine reference, so they are tested in EditMode without a scene. See Docs/unity/TESTING.md.
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("JewelryShop.Tests")]
[assembly: InternalsVisibleTo("JewelryShop.PlayModeTests")]
