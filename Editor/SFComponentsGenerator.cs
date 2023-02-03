using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SFramework.Core.Editor;
using SFramework.Core.Runtime;
using SFramework.ECS.Runtime;
using UnityEditor;
using UnityEngine;

namespace SFramework.ECS.Editor
{
    [InitializeOnLoad]
    public static class SFComponentsGenerator
    {
        private static string providerFileTemplate =
            @"using SFramework.ECS.Runtime;
using @@COMPONENTNAMESPACE@@;
using UnityEngine;
using Sirenix.OdinInspector;

namespace @@NAMESPACE@@
{
#if IL2CPP_OPTIMIZATIONS
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
    [DisallowMultipleComponent, AddComponentMenu(""SFComponents/@@NAME@@""), HideMonoScript, RequireComponent(typeof(SFEntity))]
    public sealed class _@@COMPONENTNAME@@ : SFComponent<@@COMPONENTNAME@@> {}
}
";

        static SFComponentsGenerator()
        {
            Generate();
        }

        public static void Generate(bool force = false)
        {
            var settings = AssetDatabase.LoadAssetAtPath<SFCoreSettings>(SFCoreSettings._assetPath);

            var authoringsToGenerate = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsValueType && t.GetCustomAttribute<SFGenerateComponentAttribute>() != null)
                .ToList();

            var dirPath = Path.GetFullPath(Path.Combine(
                    Application.dataPath + Path.DirectorySeparatorChar + settings.GeneratorScriptsPath));

            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
                AssetDatabase.Refresh();
            }

            foreach (var type in authoringsToGenerate)
            {
                var providerClassName = $"_{type.Name}";
                var providerFileName = $"{providerClassName}.cs";

                var path = Path
                    .GetFullPath(Path.Combine(
                        Application.dataPath + Path.DirectorySeparatorChar + settings.GeneratorScriptsPath,
                        providerFileName));

                if (!force)
                {
                    if (File.Exists(path))
                    {
                        continue;
                    }
                }


                var fileContent = providerFileTemplate
                    .Replace("@@COMPONENTNAMESPACE@@", type.Namespace)
                    .Replace("@@NAMESPACE@@", "SFramework.Generated")
                    .Replace("@@COMPONENTNAME@@", type.Name)
                    .Replace("@@NAME@@", AddSpacesToSentence(type.Name).Replace("Ref", "Reference"));


                File.WriteAllText(path, fileContent, Encoding.UTF8);
            }

            AssetDatabase.Refresh();
        }

        static string AddSpacesToSentence(string text, bool preserveAcronyms = true)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            StringBuilder newText = new StringBuilder(text.Length * 2);
            newText.Append(text[0]);
            for (int i = 1; i < text.Length; i++)
            {
                if (char.IsUpper(text[i]))
                    if (!char.IsNumber(text[i - 1]) && (text[i - 1] != ' ' && !char.IsUpper(text[i - 1])) ||
                        (preserveAcronyms && char.IsUpper(text[i - 1]) &&
                         i < text.Length - 1 && !char.IsUpper(text[i + 1])))
                        newText.Append(' ');
                newText.Append(text[i]);
            }

            return newText.ToString();
        }
    }
}