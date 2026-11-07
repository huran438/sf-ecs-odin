using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SFramework.ECS.Runtime;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace SFramework.ECS.Editor
{
    [InitializeOnLoad]
    public static class SFComponentsGenerator
    {
        private const string providerFileTemplate =
            @"using SFramework.ECS.Runtime;
using UnityEngine;
using Sirenix.OdinInspector;

@@NAMESPACE_OPEN@@[DisallowMultipleComponent, AddComponentMenu(""SFComponents/@@MENU_NAME@@""), HideMonoScript, RequireComponent(typeof(SFEntity))]
public sealed class _@@NAME@@ : @@BASE_TYPE@@ {}
@@NAMESPACE_CLOSE@@";

        static SFComponentsGenerator()
        {
            Generate();
        }

        public static void Generate(bool force = false)
        {
            var generatedAny = false;
            foreach (var type in TypeCache.GetTypesWithAttribute<SFGenerateComponentAttribute>())
            {
                if (!type.IsValueType || type.IsByRefLike || type.ContainsGenericParameters || type.IsNestedPrivate)
                {
                    Debug.LogWarning($"Skipping ECS provider for unsupported component '{type.FullName}'.");
                    continue;
                }

                var sourcePath = FindSourcePath(type);
                if (sourcePath == null || !File.Exists(sourcePath))
                {
                    Debug.LogWarning($"Could not locate a unique writable '{type.Name}.cs' for '{type.FullName}'.");
                    continue;
                }

                var providerPath = Path.Combine(Path.GetDirectoryName(sourcePath), $"_{type.Name}.cs");
                if (!force && File.Exists(providerPath)) continue;

                string fileContent;
                try
                {
                    fileContent = GenerateProviderSource(type);
                }
                catch (ArgumentException exception)
                {
                    Debug.LogWarning($"Could not generate an ECS provider for '{type.FullName}'. {exception.Message}");
                    continue;
                }

                if (File.Exists(providerPath) && File.ReadAllText(providerPath) == fileContent) continue;
                File.WriteAllText(providerPath, fileContent, Encoding.UTF8);
                generatedAny = true;
            }

            if (generatedAny) AssetDatabase.Refresh();
        }

        private static string FindSourcePath(Type type)
        {
            var fileName = $"{type.Name}.cs";
            var candidates = AssetDatabase.FindAssets($"t:Script {type.Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetFileName(path) == fileName)
                .ToArray();

            var exactMatch = candidates.FirstOrDefault(path => AssetDatabase.LoadAssetAtPath<MonoScript>(path)?.GetClass() == type);
            if (exactMatch != null) return exactMatch;

            var assemblyName = type.Assembly.GetName().Name;
            var assemblyMatches = candidates.Where(path =>
                Path.GetFileNameWithoutExtension(CompilationPipeline.GetAssemblyNameFromScriptPath(path)) == assemblyName)
                .Take(2).ToArray();
            return assemblyMatches.Length == 1 ? assemblyMatches[0] : null;
        }

        private static string GenerateProviderSource(Type type)
        {
            var baseType = type.GetCustomAttribute<SFGenerateComponentAttribute>()?.CustomBaseType ?? typeof(SFComponent<>);
            if (baseType.IsGenericTypeDefinition && baseType.GetGenericArguments().Length == 1)
                baseType = baseType.MakeGenericType(type);
            if (baseType.ContainsGenericParameters || baseType.IsSealed || !typeof(MonoBehaviour).IsAssignableFrom(baseType))
                throw new ArgumentException($"'{baseType}' must be an inheritable MonoBehaviour type.");

            return providerFileTemplate
                .Replace("@@NAMESPACE_OPEN@@", type.Namespace == null ? "" : $"namespace {type.Namespace}\n{{\n")
                .Replace("@@NAMESPACE_CLOSE@@", type.Namespace == null ? "" : "}\n")
                .Replace("@@NAME@@", type.Name)
                .Replace("@@MENU_NAME@@", AddSpacesToSentence(type.Name).Replace("Ref", "Reference"))
                .Replace("@@BASE_TYPE@@", GetTypeName(baseType));
        }

        private static string GetTypeName(Type type)
        {
            if (!type.IsGenericType) return $"global::{type.FullName.Replace('+', '.')}";

            var fullName = type.GetGenericTypeDefinition().FullName;
            var arguments = string.Join(", ", type.GetGenericArguments().Select(GetTypeName));
            return $"global::{fullName.Substring(0, fullName.IndexOf('`')).Replace('+', '.')}<{arguments}>";
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
