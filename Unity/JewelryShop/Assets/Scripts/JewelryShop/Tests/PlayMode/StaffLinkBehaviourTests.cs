using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using JewelryShop.Networking;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JewelryShop.Tests
{
    // Real Netcode in Play Mode: the EditMode tests only see a fake link, so they cannot catch
    // Netcode rejecting how the NetworkManager is built (a nested manager did exactly that).
    [TestFixture]
    public class StaffLinkBehaviourTests
    {
        private const string ManagerName = "StaffLinkNetworkManager";
        private GameObject _linkObject;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_linkObject != null) Object.Destroy(_linkObject); // OnDestroy stops the host
            yield return null; // let the destroy and the host shutdown finish before the next test
            Assert.That(GameObject.Find(ManagerName), Is.Null, "manager outlived its behaviour");
        }

        [UnityTest]
        public IEnumerator Host_StartInPlayMode_ManagerIsAtSceneRootAndNoError()
        {
            _linkObject = new GameObject("Staff Link");
            _linkObject.SetActive(false);
            var behaviour = _linkObject.AddComponent<StaffLinkBehaviour>();
            SetField(behaviour, "_role", StaffLinkBehaviour.LinkRole.Host);
            // Off the default port, so a real Staff Console can stay up while this runs.
            SetField(behaviour, "_port", (ushort)7799);
            LogAssert.Expect(LogType.Warning, new Regex("status text"));
            LogAssert.Expect(LogType.Log, new Regex("Staff link"));

            _linkObject.SetActive(true);
            yield return null;
            yield return null;

            var manager = GameObject.Find(ManagerName);
            Assert.That(manager, Is.Not.Null);
            Assert.That(manager.transform.parent, Is.Null, "Netcode forbids a nested manager");
            // Any error log (nested-manager, NullReference...) fails the test by default.
        }

        // With scene management on, a client that connects is told to load the host's scene, so the
        // Quest's Storefront got swapped for the Staff Console. The link must not sync scenes.
        [UnityTest]
        public IEnumerator Link_Created_DoesNotSynchronizeScenes()
        {
            _linkObject = new GameObject("Staff Link");
            _linkObject.SetActive(false);
            var behaviour = _linkObject.AddComponent<StaffLinkBehaviour>();
            SetField(behaviour, "_role", StaffLinkBehaviour.LinkRole.Client);
            SetField(behaviour, "_serverAddress", "127.0.0.1");
            SetField(behaviour, "_port", (ushort)7797);
            LogAssert.Expect(LogType.Warning, new Regex("status text"));
            LogAssert.Expect(LogType.Log, new Regex("Staff link"));

            _linkObject.SetActive(true);
            yield return null;

            // Read through reflection so the test assembly stays free of Netcode (ADR-0008).
            var manager = GameObject.Find(ManagerName).GetComponent("NetworkManager");
            var configField = manager.GetType().GetField("NetworkConfig");
            Assert.That(configField, Is.Not.Null, "NetworkManager.NetworkConfig was renamed");
            var config = configField.GetValue(manager);
            var flag = config.GetType().GetField("EnableSceneManagement");
            Assert.That(flag, Is.Not.Null, "NetworkConfig.EnableSceneManagement was renamed");
            Assert.That((bool)flag.GetValue(config), Is.False);
        }

        [UnityTest]
        public IEnumerator Client_DestroyedWhileConnecting_ShutsDownWithoutException()
        {
            _linkObject = new GameObject("Staff Link");
            _linkObject.SetActive(false);
            var behaviour = _linkObject.AddComponent<StaffLinkBehaviour>();
            SetField(behaviour, "_role", StaffLinkBehaviour.LinkRole.Client);
            SetField(behaviour, "_serverAddress", "127.0.0.1");
            SetField(behaviour, "_port", (ushort)7798); // nobody hosts here: the attempt stays pending
            LogAssert.Expect(LogType.Warning, new Regex("status text"));
            LogAssert.Expect(LogType.Log, new Regex("Staff link"));

            _linkObject.SetActive(true);
            yield return null;
            yield return null;
            Object.Destroy(_linkObject); // the user pressed Stop mid-attempt
            yield return null;
            yield return null;

            Assert.That(GameObject.Find(ManagerName), Is.Null);
            // An exception logged by Netcode's shutdown fails the test by default.
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
    }
}
