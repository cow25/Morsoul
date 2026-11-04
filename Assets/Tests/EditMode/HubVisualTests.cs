using System;
using System.Collections.Generic;
using System.IO;
using Morsoul.Hub;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Morsoul.Tests
{
    public class HubVisualTests
    {
        readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _objects)
                if (obj != null)
                    UnityEngine.Object.DestroyImmediate(obj);
            _objects.Clear();
        }

        GameObject NewQuad(string name = "Entity")
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            _objects.Add(go);
            return go;
        }

        Sprite NewSprite()
        {
            var tex = new Texture2D(4, 4);
            _objects.Add(tex);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            _objects.Add(sprite);
            return sprite;
        }

        VisualProfile NewProfile(Sprite sprite = null, RuntimeAnimatorController controller = null)
        {
            var profile = ScriptableObject.CreateInstance<VisualProfile>();
            profile.sprite = sprite;
            profile.animatorController = controller;
            _objects.Add(profile);
            return profile;
        }

        [Test]
        public void EntityVisual_NullProfile_KeepsQuadVisible()
        {
            var go = NewQuad();
            var visual = go.AddComponent<EntityVisual>();

            visual.Apply(null);

            Assert.That(go.GetComponent<MeshRenderer>().enabled, Is.True);
            Assert.That(go.transform.Find(EntityVisual.VisualChildName), Is.Null);
            Assert.That(visual.Renderer, Is.Null);
        }

        [Test]
        public void EntityVisual_SpriteProfile_CreatesChild_AndHidesQuad()
        {
            var go = NewQuad();
            var visual = go.AddComponent<EntityVisual>();
            var profile = NewProfile(NewSprite());
            profile.visualOffset = new Vector2(0f, 0.5f);
            profile.visualScale = new Vector2(2f, 2f);
            profile.sortingOrder = 3;
            profile.tint = new Color(1f, 0.5f, 0.5f);

            visual.Apply(profile);

            var rootRenderer = go.GetComponent<MeshRenderer>();
            Assert.That(rootRenderer.enabled, Is.False);

            var child = go.transform.Find(EntityVisual.VisualChildName);
            Assert.That(child, Is.Not.Null);
            var renderer = child.GetComponent<SpriteRenderer>();
            Assert.That(renderer.sprite, Is.SameAs(profile.sprite));
            Assert.That(renderer.sortingOrder, Is.EqualTo(3));
            Assert.That(renderer.color, Is.EqualTo(profile.tint));
            Assert.That(child.localPosition, Is.EqualTo(new Vector3(0f, 0.5f, 0f)));
            Assert.That(child.localScale, Is.EqualTo(new Vector3(2f, 2f, 1f)));
            Assert.That(visual.Renderer, Is.SameAs(renderer));
        }

        [Test]
        public void EntityVisual_ReapplyEmpty_RestoresQuad()
        {
            var go = NewQuad();
            var visual = go.AddComponent<EntityVisual>();
            visual.Apply(NewProfile(NewSprite()));
            Assert.That(go.GetComponent<MeshRenderer>().enabled, Is.False);

            visual.Apply(NewProfile());

            Assert.That(go.GetComponent<MeshRenderer>().enabled, Is.True);
            var child = go.transform.Find(EntityVisual.VisualChildName);
            Assert.That(child, Is.Not.Null);
            Assert.That(child.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void EntityVisual_ReapplySprite_ReusesChild()
        {
            var go = NewQuad();
            var visual = go.AddComponent<EntityVisual>();
            visual.Apply(NewProfile(NewSprite()));
            var first = go.transform.Find(EntityVisual.VisualChildName);

            visual.Apply(NewProfile(NewSprite()));

            Assert.That(go.transform.Find(EntityVisual.VisualChildName), Is.SameAs(first));
            Assert.That(first.childCount, Is.EqualTo(0));
        }

        [Test]
        public void EntityVisual_AnimatorProfile_AddsAnimatorAndAdapter()
        {
            var go = NewQuad();
            var controllerAsset = new UnityEditor.Animations.AnimatorController();
            _objects.Add(controllerAsset);
            var profile = NewProfile(NewSprite(), controllerAsset);
            var visual = go.AddComponent<EntityVisual>();

            visual.Apply(profile);

            var child = go.transform.Find(EntityVisual.VisualChildName);
            var animator = child.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController, Is.SameAs(controllerAsset));
            Assert.That(go.GetComponent<HubCharacterAnimator>(), Is.Not.Null);
        }

        [Test]
        public void EntityVisual_Profile_SurvivesPersistenceRoundtrip()
        {
            string folder = "Assets/HubVisualTests_" + Guid.NewGuid().ToString("N");
            var go = NewQuad();
            try
            {
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
                var profile = ScriptableObject.CreateInstance<VisualProfile>();
                string profilePath = folder + "/TestProfile.asset";
                AssetDatabase.CreateAsset(profile, profilePath);

                var visual = go.AddComponent<EntityVisual>();
                visual.profile = profile;
                string prefabPath = folder + "/Entity.prefab";
                var saved = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);

                var visualOnAsset = saved.GetComponent<EntityVisual>();
                Assert.That(visualOnAsset.profile, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(visualOnAsset.profile),
                    Is.EqualTo(profilePath));

                var loadedProfile = AssetDatabase.LoadAssetAtPath<VisualProfile>(profilePath);
                Assert.That(loadedProfile, Is.Not.Null);

                var instance = PrefabUtility.InstantiatePrefab(saved) as GameObject;
                _objects.Add(instance);
                var instanceVisual = instance.GetComponent<EntityVisual>();
                Assert.That(instanceVisual.profile, Is.SameAs(loadedProfile));

                loadedProfile.sprite = NewSprite();
                instanceVisual.Apply();
                Assert.That(instance.GetComponent<MeshRenderer>().enabled, Is.False);
                Assert.That(instance.transform.Find(EntityVisual.VisualChildName), Is.Not.Null);
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void EntityVisual_SpriteAssignedAfterEmptyProfile_AppliesOnReapply()
        {
            var go = NewQuad();
            var visual = go.AddComponent<EntityVisual>();
            var profile = NewProfile();

            visual.Apply(profile);
            Assert.That(go.GetComponent<MeshRenderer>().enabled, Is.True);
            Assert.That(visual.profile, Is.SameAs(profile));

            profile.sprite = NewSprite();
            visual.Apply();

            Assert.That(go.GetComponent<MeshRenderer>().enabled, Is.False);
            var child = go.transform.Find(EntityVisual.VisualChildName);
            Assert.That(child, Is.Not.Null);
            Assert.That(child.gameObject.activeSelf, Is.True);
            Assert.That(child.GetComponent<SpriteRenderer>().sprite, Is.SameAs(profile.sprite));
        }

        [Test]
        public void EntityVisual_MaterialCleared_RestoresDefault()
        {
            var go = NewQuad();
            var visual = go.AddComponent<EntityVisual>();
            var profile = NewProfile(NewSprite());

            visual.Apply(profile);
            var renderer = go.transform.Find(EntityVisual.VisualChildName)
                .GetComponent<SpriteRenderer>();
            var defaultMaterial = renderer.sharedMaterial;

            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            var custom = new Material(shader);
            _objects.Add(custom);

            profile.material = custom;
            visual.Apply();
            Assert.That(renderer.sharedMaterial, Is.SameAs(custom));
            var restored = UnityEngine.Object.Instantiate(go);
            _objects.Add(restored);

            profile.material = null;
            visual.Apply();
            restored.GetComponent<EntityVisual>().Apply();
            Assert.That(renderer.sharedMaterial, Is.SameAs(defaultMaterial));
            Assert.That(restored.transform.Find(EntityVisual.VisualChildName)
                .GetComponent<SpriteRenderer>().sharedMaterial, Is.SameAs(defaultMaterial));
        }

        [Test]
        public void HubCharacterAnimator_NoController_UpdateIsSafe()
        {
            var go = NewQuad();
            var adapter = go.AddComponent<HubCharacterAnimator>();
            adapter.Bind(null, null);
            Assert.That(() => adapter.Tick(Vector2.one), Throws.Nothing);
        }

        UnityEditor.Animations.AnimatorController NewController(
            string name, AnimatorControllerParameterType type)
        {
            var controller = new UnityEditor.Animations.AnimatorController();
            controller.AddParameter(name, type);
            _objects.Add(controller);
            return controller;
        }

        [Test]
        public void HubCharacterAnimator_Tick_DrivesTypedParams()
        {
            var go = NewQuad();
            var animator = go.AddComponent<Animator>();
            var controller = new UnityEditor.Animations.AnimatorController();
            controller.AddLayer("Base Layer");
            var idle = controller.layers[0].stateMachine.AddState("Idle");
            controller.layers[0].stateMachine.defaultState = idle;
            controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            _objects.Add(controller);
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Update(0f);
            var adapter = go.AddComponent<HubCharacterAnimator>();
            adapter.Bind(animator, null);

            adapter.Tick(new Vector2(1f, 0f));
            Assert.That(animator.GetBool("Moving"), Is.True);
            Assert.That(animator.GetFloat("MoveX"), Is.EqualTo(1f).Within(0.001f));
            Assert.That(animator.GetFloat("Speed"), Is.EqualTo(1f).Within(0.001f));

            adapter.Tick(Vector2.zero);
            Assert.That(animator.GetBool("Moving"), Is.False);
            Assert.That(animator.GetFloat("Speed"), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void HubCharacterAnimator_WrongParamType_NotDriven()
        {
            var go = NewQuad();
            var animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController =
                NewController("Moving", AnimatorControllerParameterType.Float);
            var adapter = go.AddComponent<HubCharacterAnimator>();
            adapter.Bind(animator, null);

            Assert.That(() => adapter.Tick(Vector2.one), Throws.Nothing);
            Assert.That(animator.GetFloat("Moving"), Is.EqualTo(0f));
        }
    }
}
