using System;
using SFramework.Core.Editor;
using UnityEditor;

namespace SFramework.ECS.Editor
{
    [Serializable]
    public sealed class SFECSTool : ISFEditorTool
    {
        [MenuItem("Edit/SFramework/Force Regenerate ECS Scripts")]
        private static void GenerateAuthorings()
        {
          SFComponentsGenerator.Generate(true);
        }

        public string Title => "SF ECS";
    }
}