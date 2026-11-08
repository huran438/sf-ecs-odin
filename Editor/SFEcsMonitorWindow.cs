using System;
using System.Collections.Generic;
using System.Linq;
using SFramework.ECS.Runtime;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace SFramework.ECS.Editor
{
    public sealed class SFEcsMonitorWindow : OdinEditorWindow
    {
        private const int PageSize = 100;
        private const double RefreshInterval = 0.2;

        private readonly List<SFEcsEditorWorlds.Handle> _worlds = new();
        private readonly List<SFEcsPackedEntity> _matches = new();
        private readonly List<SFEcsComponentPreview> _previews = new();
        private string[] _worldNames = Array.Empty<string>();
        private int[] _entities;
        private Type[] _componentTypes;
        private object[] _components;
        private int _entityCount;
        private int _page;
        private string _search = "";
        private double _refreshAt;
        private SFEcsEditorWorlds.Handle _world;
        private SFEcsPackedEntity? _selection;
        private bool _selectionAlive;
        private bool _ignoreDeferredInput;
        private Vector2 _entityScroll;
        private Vector2 _componentScroll;

        [MenuItem("Window/SFramework/ECS Monitor")]
        private static void Open() => GetWindow<SFEcsMonitorWindow>("ECS Monitor");

        protected override void Initialize()
        {
            base.Initialize();
            minSize = new Vector2(540, 320);
            UseScrollView = false;
        }

        private void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying || _worlds.Count != 0) Repaint();
        }

        protected override void DrawEditors()
        {
            var input = Event.current;
            if (input.type == EventType.MouseDown || (input.type == EventType.KeyDown && GUIUtility.keyboardControl != 0))
                _ignoreDeferredInput = false;
            if (Event.current.type == EventType.Layout && EditorApplication.timeSinceStartup >= _refreshAt)
                Refresh();

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect live ECS worlds.", MessageType.Info);
                return;
            }
            if (_worlds.Count == 0)
            {
                EditorGUILayout.HelpBox("Waiting for an ECS world.", MessageType.Info);
                return;
            }

            DrawToolbar();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawEntities();
                DrawComponents();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var index = Mathf.Max(0, _worlds.IndexOf(_world));
                var next = EditorGUILayout.Popup(index, _worldNames, EditorStyles.toolbarPopup, GUILayout.Width(220));
                if (next != index)
                {
                    CancelComponentInput();
                    _world = _worlds[next];
                    _selection = null;
                    _page = 0;
                    _refreshAt = 0;
                    GUIUtility.ExitGUI();
                }

                GUILayout.Label("Search", EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
                var search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
                if (search != _search)
                {
                    _search = search;
                    _page = 0;
                    _refreshAt = 0;
                }
            }
        }

        private void DrawEntities()
        {
            var width = Mathf.Clamp(position.width * 0.35f, 175, 300);
            using var column = new EditorGUILayout.VerticalScope(GUILayout.Width(width));
            EditorGUILayout.LabelField($"Entities  {_matches.Count} / {_entityCount}", EditorStyles.boldLabel);

            using (var scroll = new EditorGUILayout.ScrollViewScope(_entityScroll))
            {
                _entityScroll = scroll.scrollPosition;
                var end = Math.Min((_page + 1) * PageSize, _matches.Count);
                for (var i = _page * PageSize; i < end; i++)
                {
                    var entity = _matches[i];
                    var selected = _selection.HasValue && _selection.Value.EqualsTo(entity);
                    var label = $"Entity {entity.Id}  ·  gen {entity.Gen}";
                    if (GUILayout.Toggle(selected, label, EditorStyles.miniButton) && !selected)
                    {
                        CancelComponentInput();
                        _selection = entity;
                        _componentScroll = Vector2.zero;
                        _refreshAt = 0;
                        GUIUtility.ExitGUI();
                    }
                }
            }

            var pages = Math.Max(1, (_matches.Count + PageSize - 1) / PageSize);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_page == 0))
                    if (GUILayout.Button("Previous", EditorStyles.miniButton)) ChangePage(-1);
                GUILayout.Label($"{_page + 1} / {pages}", GUILayout.ExpandWidth(false));
                using (new EditorGUI.DisabledScope(_page >= pages - 1))
                    if (GUILayout.Button("Next", EditorStyles.miniButton)) ChangePage(1);
            }
        }

        private void DrawComponents()
        {
            using var column = new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true));
            if (!_selection.HasValue)
            {
                EditorGUILayout.HelpBox("Select an entity. Search by ID or component type.", MessageType.Info);
                return;
            }
            if (!_selectionAlive)
            {
                EditorGUILayout.HelpBox("The selected entity no longer exists.", MessageType.Info);
                return;
            }

            var world = _world;
            var entity = _selection.Value;
            world.TryGetWorld(out var liveWorld);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Entity {entity.Id}  ·  {_previews.Count} components", EditorStyles.boldLabel);
                if (GUILayout.Button("Add component", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                    ShowComponentPicker(world, entity);
            }
            using var scroll = new EditorGUILayout.ScrollViewScope(_componentScroll);
            _componentScroll = scroll.scrollPosition;
            foreach (var preview in _previews)
            {
                var type = preview.ComponentType;
                preview.Draw(
                    SFEcsComponentEditing.GetCurrentValue(liveWorld, entity, type),
                    (path, value) =>
                    {
                        if (!_ignoreDeferredInput || Event.current.rawType != EventType.ExecuteCommand)
                            ChangeComponent(world, entity, (live, target) =>
                                SFEcsComponentEditing.TrySetField(live, target, type, path, value));
                    },
                    () => ChangeComponent(world, entity, (live, target) => SFEcsComponentEditing.TryRemove(live, target, type)),
                    CancelComponentInput,
                    removesEntity: _previews.Count == 1);
            }
        }

        private void ShowComponentPicker(SFEcsEditorWorlds.Handle world, SFEcsPackedEntity entity)
        {
            if (!world.TryGetWorld(out var live)) return;
            var types = SFEcsComponentEditing.GetAddableTypes(live, entity);
            if (types.Length == 0)
            {
                ShowNotification(new GUIContent("No components available to add."));
                return;
            }

            var selector = new GenericSelector<Type>("Add component", false, type => type.FullName, types);
            selector.SelectionConfirmed += selection =>
            {
                var type = selection.FirstOrDefault();
                if (type != null)
                    ChangeComponent(world, entity, (targetWorld, target) => SFEcsComponentEditing.TryAdd(targetWorld, target, type));
            };
            selector.ShowInPopup();
        }

        private void ChangeComponent(SFEcsEditorWorlds.Handle world, SFEcsPackedEntity entity,
            Func<SFEcsWorld, SFEcsPackedEntity, bool> change)
        {
            if (this == null || !EditorApplication.isPlaying) return;
            try
            {
                if (!world.TryGetWorld(out var live) || !change(live, entity))
                    ShowNotification(new GUIContent("The ECS entity or component changed before the edit."));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ShowNotification(new GUIContent(exception.Message));
            }

            _refreshAt = 0;
            Repaint();
        }

        private void Refresh()
        {
            _refreshAt = EditorApplication.timeSinceStartup + RefreshInterval;
            if (!EditorApplication.isPlaying)
            {
                _worlds.Clear();
                _matches.Clear();
                _world = null;
                _selection = null;
                _previews.Clear();
                return;
            }

            SFEcsEditorWorlds.GetLiveWorlds(_worlds);
            if (_worldNames.Length != _worlds.Count) _worldNames = new string[_worlds.Count];
            for (var i = 0; i < _worlds.Count; i++)
                _worldNames[i] = _worlds[i].TryGetWorld(out var live) ? live.DebugName : "Destroyed world";
            if (!_worlds.Contains(_world))
            {
                CancelComponentInput();
                _world = _worlds.Count == 0 ? null : _worlds[0];
                _selection = null;
                _page = 0;
            }

            _matches.Clear();
            if (_world == null || !_world.TryGetWorld(out var world))
            {
                _entityCount = 0;
                _selectionAlive = false;
                _previews.Clear();
                return;
            }

            _entityCount = world.GetAllEntities(ref _entities);
            var query = _search.Trim();
            var searchById = int.TryParse(query, out var id);
            for (var i = 0; i < _entityCount; i++)
            {
                var entity = _entities[i];
                if (query.Length == 0 || (searchById ? entity == id : HasComponent(world, entity, query)))
                    _matches.Add(world.PackEntity(entity));
            }

            _page = Math.Min(_page, Math.Max(0, (_matches.Count - 1) / PageSize));
            if (!_selection.HasValue && _matches.Count != 0) _selection = _matches[0];
            RefreshPreviews(world);
        }

        private bool HasComponent(SFEcsWorld world, int entity, string query)
        {
            var count = world.GetComponentTypes(entity, ref _componentTypes);
            for (var i = 0; i < count; i++)
            {
                if (_componentTypes[i].Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private void RefreshPreviews(SFEcsWorld world)
        {
            _selectionAlive = false;
            if (!_selection.HasValue || !_selection.Value.Unpack(world, out var entity))
            {
                if (_previews.Count != 0) CancelComponentInput();
                _previews.Clear();
                return;
            }

            _selectionAlive = true;
            var count = world.GetComponents(entity, ref _components);
            var controlsChanged = count != _previews.Count;
            for (var i = 0; i < count; i++)
            {
                var value = _components[i];
                if (i == _previews.Count) _previews.Add(new SFEcsComponentPreview(value.GetType()));
                else if (_previews[i].ComponentType != value.GetType())
                {
                    controlsChanged = true;
                    _previews[i] = new SFEcsComponentPreview(value.GetType());
                }
                var fields = _previews[i].FieldCount;
                _previews[i].Update(value);
                controlsChanged |= fields != _previews[i].FieldCount;
            }
            if (controlsChanged) CancelComponentInput();
            if (count < _previews.Count) _previews.RemoveRange(count, _previews.Count - count);
            if (_components != null) Array.Clear(_components, 0, _components.Length);
        }

        private void CancelComponentInput()
        {
            // IMGUI can recycle control IDs after rows change. Discard pending commands from the old controls.
            _ignoreDeferredInput = true;
            GUIUtility.hotControl = 0;
            GUI.FocusControl(null);
            EditorGUIUtility.editingTextField = false;
        }

        private void ChangePage(int offset)
        {
            _page += offset;
            _entityScroll = Vector2.zero;
            GUIUtility.ExitGUI();
        }
    }
}
