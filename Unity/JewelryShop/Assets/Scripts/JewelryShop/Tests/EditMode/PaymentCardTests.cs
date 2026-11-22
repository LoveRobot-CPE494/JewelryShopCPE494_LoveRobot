using JewelryShop.Storefront;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class PaymentCardTests
    {
        static readonly Bounds Area = new Bounds(Vector3.zero, new Vector3(10, 4, 10));

        readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();

        GameObject Make(string name)
        {
            var go = new GameObject(name);
            _made.Add(go);
            return go;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        [Test]
        public void IsOutside_PointInsideArea_IsFalse() =>
            Assert.That(PaymentCard.IsOutside(Area, new Vector3(1, 0.5f, 1)), Is.False);

        [Test]
        public void IsOutside_FellThroughFloor_IsTrue() =>
            Assert.That(PaymentCard.IsOutside(Area, new Vector3(0, -50, 0)), Is.True);

        [Test]
        public void IsOutside_PastWall_IsTrue() =>
            Assert.That(PaymentCard.IsOutside(Area, new Vector3(20, 0, 0)), Is.True);

        [Test]
        public void IsOutside_AreaUnset_IsNeverTrue() =>
            Assert.That(PaymentCard.IsOutside(default, new Vector3(999, -999, 999)), Is.False);

        [Test]
        public void ReturnHome_HomeSet_MovesCardAndStopsIt()
        {
            var home = Make("home").transform;
            home.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(0, 90, 0));
            var card = Make("card");
            var pc = card.AddComponent<PaymentCard>();
            new SerializedObjectHelper(pc).Set("_home", home);
            var body = card.GetComponent<Rigidbody>();
            body.linearVelocity = Vector3.one;
            card.transform.position = new Vector3(50, -9, 0);

            pc.ReturnHome();

            Assert.That(card.transform.position, Is.EqualTo(home.position));
            Assert.That(card.transform.rotation, Is.EqualTo(home.rotation));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ReturnHome_HomeUnset_WarnsAndStaysPut()
        {
            var card = Make("card");
            var pc = card.AddComponent<PaymentCard>();
            card.transform.position = new Vector3(5, 5, 5);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("home spot is unwired"));

            pc.ReturnHome();

            Assert.That(card.transform.position, Is.EqualTo(new Vector3(5, 5, 5)));
        }

        // Private [SerializeField] set the way the Inspector does.
        readonly struct SerializedObjectHelper
        {
            readonly UnityEditor.SerializedObject _so;
            public SerializedObjectHelper(Object o) => _so = new UnityEditor.SerializedObject(o);
            public void Set(string field, Object value)
            {
                _so.FindProperty(field).objectReferenceValue = value;
                _so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
