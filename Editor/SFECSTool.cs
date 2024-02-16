using System;
using SFramework.Core.Editor;
using UnityEditor;

namespace SFramework.ECS.Editor
{
    [Serializable]
    public sealed class SFECSTool : ISFEditorTool
    {
        [MenuItem("Edit/SFramework/Regenerate ECS Components")]
        private static void GenerateAuthorings()
        {
          SFComponentsGenerator.Generate(true);
        }

        public string Title => "SF ECS";
    }
}