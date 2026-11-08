using System;
using System.Collections.Generic;
using SFramework.ECS.Runtime;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using static SFramework.ECS.Runtime.SFEcsSystemsProfiler;

namespace SFramework.ECS.Editor
{
    public sealed class SFEcsSystemsProfilerWindow : OdinEditorWindow
    {
        private const float MetricWidth = 58;
        private const float CallbackWidth = 4 * MetricWidth;
        private const double RefreshInterval = 0.2;
        private readonly Recording _samples = new();
        private readonly List<PipelineHandle> _pipelines = new();
        private readonly List<Row> _rows = new();
        private string[] _pipelineLabels = Array.Empty<string>();
        private PipelineHandle _pipeline;
        private GUIStyle _metricStyle;
        private double _refreshAt;
        private bool _recording = true;
        private bool _playing;
        private Vector2 _scroll;

        [MenuItem("Window/SFramework/ECS Systems Profiler")]
        private static void Open() => GetWindow<SFEcsSystemsProfilerWindow>("ECS Systems Profiler");

        protected override void Initialize()
        {
            base.Initialize();
            minSize = new Vector2(740, 280);
            UseScrollView = false;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (_recording && EditorApplication.isPlaying) _samples.Resume();
        }

        protected override void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            _samples.Dispose();
            _samples.Clear();
            base.OnDisable();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                _samples.Pause();
                _samples.Clear();
            }
            else if (state == PlayModeStateChange.EnteredPlayMode && _recording) _samples.Resume();

            _refreshAt = 0;
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying || _playing) Repaint();
        }

        protected override void DrawEditors()
        {
            if (Event.current.type == EventType.Layout && EditorApplication.timeSinceStartup >= _refreshAt)
                Refresh();

            if (!_playing)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to profile ECS systems.", MessageType.Info);
                return;
            }

            DrawToolbar();
            if (_pipelines.Count == 0)
            {
                EditorGUILayout.HelpBox("Waiting for initialized ECS pipelines.", MessageType.Info);
                return;
            }
            if (_rows.Count == 0)
            {
                EditorGUILayout.HelpBox("This phase has no enabled systems.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Times are in milliseconds. Group timings include their children and state checks.",
                EditorStyles.miniLabel);
            _metricStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            using var scroll = new EditorGUILayout.ScrollViewScope(_scroll);
            _scroll = scroll.scrollPosition;
            DrawHeader();
            foreach (var row in _rows) DrawRow(row);
        }

        private void DrawToolbar()
        {
            using var toolbar = new EditorGUILayout.HorizontalScope(EditorStyles.toolbar);
            if (_pipelines.Count > 0)
            {
                var index = Mathf.Max(0, _pipelines.IndexOf(_pipeline));
                var next = EditorGUILayout.Popup(index, _pipelineLabels, EditorStyles.toolbarPopup, GUILayout.MinWidth(280));
                if (next != index)
                {
                    _pipeline = _pipelines[next];
                    _scroll = Vector2.zero;
                    _refreshAt = 0;
                    GUIUtility.ExitGUI();
                }
            }
            GUILayout.FlexibleSpace();
            var record = GUILayout.Toggle(_recording, "Record", EditorStyles.toolbarButton, GUILayout.Width(54));
            if (record != _recording)
            {
                _recording = record;
                if (record) _samples.Resume();
                else _samples.Pause();
            }
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(54)))
            {
                _samples.Clear();
                _refreshAt = 0;
            }
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("System", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
                GUILayout.Label("Run", EditorStyles.boldLabel, GUILayout.Width(CallbackWidth + 12));
                GUILayout.Label("PostRun", EditorStyles.boldLabel, GUILayout.Width(CallbackWidth + 12));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                DrawMetricHeaders();
                DrawMetricHeaders();
            }
        }

        private void DrawMetricHeaders()
        {
            Metric("Last ms");
            Metric("Avg ms");
            Metric("Peak ms");
            Metric("Calls");
        }

        private void DrawRow(Row row)
        {
            using var line = new EditorGUILayout.HorizontalScope();
            using var disabled = new EditorGUI.DisabledScope(!row.Enabled);
            var rect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.label,
                GUILayout.MinWidth(160), GUILayout.ExpandWidth(true));
            rect.xMin += Mathf.Min(4 + row.Depth * 14, Mathf.Max(0, rect.width - 45));
            GUI.Label(rect, row.Name, EditorStyles.label);
            DrawSample(row.Run);
            DrawSample(row.PostRun);
        }

        private void DrawSample(Sample? sample)
        {
            var value = sample.GetValueOrDefault();
            Metric(value.Count == 0 ? "—" : value.LastMilliseconds.ToString("F3"));
            Metric(value.Count == 0 ? "—" : value.AverageMilliseconds.ToString("F3"));
            Metric(value.Count == 0 ? "—" : value.PeakMilliseconds.ToString("F3"));
            Metric(sample.HasValue ? value.Count.ToString("N0") : "—");
        }

        private void Metric(string label) => GUILayout.Label(label, _metricStyle, GUILayout.Width(MetricWidth));

        private void Refresh()
        {
            _refreshAt = EditorApplication.timeSinceStartup + RefreshInterval;
            _playing = EditorApplication.isPlaying;
            _rows.Clear();
            if (!_playing)
            {
                _pipeline = null;
                _pipelines.Clear();
                return;
            }

            GetLivePipelines(_pipelines);
            if (_pipelineLabels.Length != _pipelines.Count) _pipelineLabels = new string[_pipelines.Count];
            for (var i = 0; i < _pipelines.Count; i++)
            {
                var label = $"{_pipelines[i].WorldName}   /   {_pipelines[i].Phase}";
                _pipelineLabels[i] = Array.IndexOf(_pipelineLabels, label, 0, i) < 0 ? label : $"{label}  ({i + 1})";
            }

            if (!_pipelines.Contains(_pipeline))
            {
                _pipeline = _pipelines.Find(handle => handle.TryGetPipeline(out var live) && GetSystems(live).Count != 0);
                if (_pipeline == null && _pipelines.Count != 0) _pipeline = _pipelines[0];
                _scroll = Vector2.zero;
            }
            if (_pipeline != null && _pipeline.TryGetPipeline(out var pipeline))
                AddRows(GetSystems(pipeline), depth: 0, parentEnabled: true);
        }

        private void AddRows(IReadOnlyList<SystemSlot> slots, int depth, bool parentEnabled)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var groupEnabled = slot.GroupEnabled;
                var enabled = parentEnabled && groupEnabled != false;
                var label = $"{i + 1}. {slot.Name}";
                if (groupEnabled.HasValue) label += groupEnabled.Value ? "  (enabled)" : "  (disabled)";
                if (!parentEnabled) label += "  (parent disabled)";
                _rows.Add(new Row(new GUIContent(label, label), depth, enabled,
                    slot.HasRun ? _samples.GetSample(slot, postRun: false) : null,
                    slot.HasPostRun ? _samples.GetSample(slot, postRun: true) : null));
                if (slot.Children.Count != 0) AddRows(slot.Children, depth + 1, enabled);
            }
        }

        private readonly struct Row
        {
            internal readonly GUIContent Name;
            internal readonly int Depth;
            internal readonly bool Enabled;
            internal readonly Sample? Run;
            internal readonly Sample? PostRun;

            internal Row(GUIContent name, int depth, bool enabled, Sample? run, Sample? postRun)
            {
                Name = name;
                Depth = depth;
                Enabled = enabled;
                Run = run;
                PostRun = postRun;
            }
        }
    }
}
