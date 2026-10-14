using System.Linq;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GolfArcade.Tests
{
    /// ACCESSORIES (work/accessories): cap, glove and bag are their own pieces on named sockets; swapping or removing one never touches the body, the kit or another slot.
    public class HeroAccessoriesTests
    {
        const string PrefabDir = "Assets/Resources/Tennis/Customization/";

        static GameObject Hero(string sex, bool golf)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Player" + sex + (golf ? "GolfKit" : "") + ".prefab");
            Assert.IsNotNull(prefab, "hero prefab " + sex + (golf ? " golf" : ""));
            return Object.Instantiate(prefab);
        }

        [Test]
        public void EveryItemHasDataForBothBodies([ValueSource(nameof(Ids))] string id, [Values("Male", "Female")] string sex)
        {
            var json = Resources.Load<TextAsset>($"Accessories/{sex}/{id}/item");
            Assert.IsNotNull(json, $"Resources/Accessories/{sex}/{id}/item.json (run work/accessories/unity_iter.sh install)");
            var item = JsonUtility.FromJson<ItemProbe>(json.text);
            Assert.AreEqual(id, item.item); Assert.AreEqual(sex, item.sex);
            Assert.IsNotEmpty(item.pieces);
            foreach (var p in item.pieces)
            {
                var mesh = Resources.Load<Mesh>($"Accessories/{sex}/{id}/{p.mesh}");
                Assert.IsNotNull(mesh, p.mesh); Assert.IsTrue(mesh.isReadable, p.mesh + " must stay readable (the lobby export skins it on the CPU)");
                Assert.AreEqual(mesh.vertexCount, mesh.boneWeights.Length, p.mesh);
                Assert.AreEqual(p.bones.Length, mesh.bindposes.Length, p.mesh);
                Assert.IsNotNull(Resources.Load<Texture2D>($"Accessories/{sex}/{id}/Acc_{sex}_{id}_{p.maps}_N"), p.maps + " normal map");
                Assert.IsNotNull(Resources.Load<Texture2D>($"Accessories/{sex}/{id}/Acc_{sex}_{id}_{p.maps}_M"), p.maps + " mask map");
            }
        }
        static string[] Ids => HeroAccessories.Catalog.Select(d => d.id).ToArray();

        [System.Serializable] class PieceProbe { public string name, mesh, maps; public string[] bones; }
        [System.Serializable] class ItemProbe { public string item, sex; public PieceProbe[] pieces; }

        [TestCase("Male", false, "tennis_cap")]
        [TestCase("Female", false, "tennis_cap")]
        [TestCase("Male", true, "golf_cap")]
        [TestCase("Female", true, "golf_cap")]
        public void TheDefaultCapSitsOnItsSocketAndNothingElseChanges(string sex, bool golf, string expected)
        {
            var go = Hero(sex, golf);
            try
            {
                var look = go.GetComponent<MatchHeroLook>();
                var bodyMesh = look.body.sharedMesh; int kitCount = look.kit.Length; var kitMeshes = look.kit.Select(k => k.sharedMesh).ToArray();
                HeroAccessories.Setup(look);
                var acc = HeroAccessories.For(look);
                Assert.AreEqual(expected, acc.Item(AccessorySlot.Headwear));
                var cap = acc.Renderers(AccessorySlot.Headwear).Single();
                Assert.AreEqual("Acc_Cap", cap.name);
                var socket = go.GetComponentsInChildren<Transform>(true).Single(t => t.name == HeroAccessories.SocketHeadwear);
                Assert.AreEqual("Head", socket.parent.name, "the cap socket hangs off the head bone");
                Assert.IsNotNull(go.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == HeroAccessories.SocketHair && t.parent == socket.parent), "the hair socket waits next to it");
                Assert.IsTrue(cap.bones.Contains(socket), "the cap follows the socket");
                // the body, the head and the clothes are as they were (in golf the glove's hand is hidden under it: the same body,
                // the gloved hand's skin left out so it never shows through in a swing)
                if (golf) Assert.AreEqual(bodyMesh.name + " (accessory cover)", look.body.sharedMesh.name);
                else Assert.AreSame(bodyMesh, look.body.sharedMesh);
                var dressed = look.body.sharedMesh;
                Assert.AreEqual(kitCount, look.kit.Length);
                CollectionAssert.AreEqual(kitMeshes, look.kit.Select(k => k.sharedMesh).ToArray());
                // swap, then remove: one cap at a time, the socket stays, the body is untouched
                string other = golf ? "tennis_cap" : "golf_cap";
                Assert.IsTrue(acc.Equip(AccessorySlot.Headwear, other));
                Assert.AreEqual(other, acc.Item(AccessorySlot.Headwear));
                Assert.AreEqual(1, go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Count(r => r.name == "Acc_Cap"));
                acc.Unequip(AccessorySlot.Headwear);
                Assert.IsNull(acc.Item(AccessorySlot.Headwear));
                Assert.AreEqual(0, go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Count(r => r.name == "Acc_Cap"));
                Assert.AreSame(dressed, look.body.sharedMesh, "a cap on or off changes nothing on the body");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ASlotTakesItsOwnColoursAndCanBeHidden()
        {
            var go = Hero("Male", false);
            try
            {
                var look = go.GetComponent<MatchHeroLook>();
                HeroAccessories.Setup(look);
                var acc = HeroAccessories.For(look);
                var cap = acc.Renderers(AccessorySlot.Headwear).Single();
                var cloth = cap.sharedMaterials.First(m => m.shader.name.EndsWith("TennisCloth"));
                var before = cloth.GetColor("_BaseColor");
                acc.SetColours(AccessorySlot.Headwear, Color.red, null);
                Assert.AreNotEqual(before, cloth.GetColor("_BaseColor"), "the cap's own colour pick reaches its material");
                acc.SetVisible(AccessorySlot.Headwear, false);
                Assert.IsFalse(cap.enabled, "hidden = disabled (still listed, never drawn)");
                Assert.IsFalse(acc.IsVisible(AccessorySlot.Headwear));
                acc.SetVisible(AccessorySlot.Headwear, true);
                Assert.IsTrue(cap.enabled);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
