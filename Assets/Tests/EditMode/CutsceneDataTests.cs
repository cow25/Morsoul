using System.Collections.Generic;
using Morsoul.Cutscene;
using NUnit.Framework;
using UnityEngine;

namespace Morsoul.Tests
{
    public class CutsceneDataTests
    {
        readonly List<Object> _assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets)
                if (asset != null)
                    Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        Sprite NewSprite()
        {
            var tex = new Texture2D(2, 2);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), Vector2.zero);
            _assets.Add(tex);
            _assets.Add(sprite);
            return sprite;
        }

        static CutsceneShotData Shot(Sprite image = null, string caption = null, bool wait = false)
        {
            return new CutsceneShotData
            {
                image = image,
                caption = caption,
                waitForInput = wait,
            };
        }

        [Test]
        public void Shot_Defaults_AreSane()
        {
            var shot = new CutsceneShotData();
            Assert.AreEqual(4f, shot.durationSeconds);
            Assert.IsFalse(shot.waitForInput);
            Assert.IsNull(shot.image);
            Assert.IsNull(shot.video);
            Assert.IsNull(shot.caption);
            Assert.IsNull(shot.captionSpeaker);
        }

        [Test]
        public void Planner_HasVisual_RequiresImageOrVideo()
        {
            Assert.IsFalse(CutscenePlanner.HasVisual(null));
            Assert.IsFalse(CutscenePlanner.HasVisual(Shot()));
            Assert.IsTrue(CutscenePlanner.HasVisual(Shot(image: NewSprite())));
        }

        [Test]
        public void Planner_FirstIndex_SkipsNullAndVisualLessShots()
        {
            var sprite = NewSprite();
            var shots = new[]
            {
                null,
                Shot(),                      // no visual - skipped
                Shot(caption: "caption"),    // caption only, no visual - skipped
                Shot(image: sprite),         // playable
            };
            Assert.AreEqual(3, CutscenePlanner.FirstIndex(shots));
        }

        [Test]
        public void Planner_AdvanceIndex_ReturnsNextPlayableThenMinusOne()
        {
            var sprite = NewSprite();
            var shots = new[]
            {
                Shot(image: sprite),
                null,
                Shot(),
                Shot(image: sprite, wait: true),
            };
            Assert.AreEqual(0, CutscenePlanner.FirstIndex(shots));
            Assert.AreEqual(3, CutscenePlanner.AdvanceIndex(shots, 0));
            Assert.AreEqual(-1, CutscenePlanner.AdvanceIndex(shots, 3));
        }

        [Test]
        public void Planner_NullShotList_HasNoPlayableShots()
        {
            Assert.AreEqual(-1, CutscenePlanner.FirstIndex(null));
            Assert.AreEqual(-1, CutscenePlanner.AdvanceIndex(null, 0));
        }

        [Test]
        public void Planner_HasCaption_RequiresNonEmptyText()
        {
            Assert.IsFalse(CutscenePlanner.HasCaption(null));
            Assert.IsFalse(CutscenePlanner.HasCaption(Shot()));
            Assert.IsFalse(CutscenePlanner.HasCaption(Shot(caption: "")));
            Assert.IsTrue(CutscenePlanner.HasCaption(Shot(caption: "line")));
        }

        [Test]
        public void CutsceneData_HoldsShotsAndSceneRouting()
        {
            var data = ScriptableObject.CreateInstance<CutsceneData>();
            _assets.Add(data);
            data.id = "CS_TEST";
            data.shots = new[] { Shot(image: NewSprite()) };
            data.nextSceneName = "Hub";
            data.skipToScene = true;

            Assert.AreEqual("CS_TEST", data.id);
            Assert.AreEqual(1, data.shots.Length);
            Assert.AreEqual("Hub", data.nextSceneName);
            Assert.IsTrue(data.skipToScene);
            Assert.AreEqual(0, CutscenePlanner.FirstIndex(data.shots));
        }
    }
}
