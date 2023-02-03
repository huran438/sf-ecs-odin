using System;
using SFramework.Core.Editor;
using UnityEditor;

namespace SFramework.ECS.Editor
{
    [Serializable]
    public sealed class SFLeoEcsTool : ISFEditorTool
    {
        [MenuItem("Edit/SFramework/Generate ECS Scripts")]
        private static void GenerateAuthorings()
        {
          SFComponentsGenerator.Generate(true);
        }

        public string Title => "Leo Ecs";
    }
}