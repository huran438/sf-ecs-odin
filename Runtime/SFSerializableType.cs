using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SFramework.ECS.Runtime
{
    [Serializable]
    public class SFSerializableType : ISerializationCallbackReceiver {
        [SerializeField] string assemblyQualifiedName = string.Empty;
        private string _resolvedName;
        private Type _resolvedType;

        public Type Type
        {
            get
            {
                if (_resolvedName != assemblyQualifiedName) Resolve();
                return _resolvedType;
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }
        void ISerializationCallbackReceiver.OnAfterDeserialize() => Resolve();

        private void Resolve()
        {
            _resolvedName = assemblyQualifiedName;
            _resolvedType = ResolveTypeName(_resolvedName);
        }

        internal static Type ResolveTypeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            try { return System.Type.GetType(name); }
            catch (Exception exception) when (exception is ArgumentException or TypeLoadException or FileNotFoundException
                or FileLoadException or BadImageFormatException)
            {
                return null;
            }
        }
        
        // Implicit conversion from SerializableType to Type
        public static implicit operator Type(SFSerializableType sType) => sType?.Type;

        // Implicit conversion from Type to SerializableType
        public static implicit operator SFSerializableType(Type type)
        {
            var name = type?.AssemblyQualifiedName;
            return new SFSerializableType { assemblyQualifiedName = name, _resolvedName = name, _resolvedType = type };
        }
    }

    public class SFSerializableTypeFilterAttribute : PropertyAttribute {
        public Func<Type, bool> Filter { get; }
        
        public SFSerializableTypeFilterAttribute(Type filterType) {
            Filter = type => !type.IsAbstract &&
                             !type.IsInterface &&
                             !type.IsGenericType &&
                             type.InheritsOrImplements(filterType);
        }
    }

    public static class TypeExtensions {
        /// <summary>
        /// Checks if a given type inherits or implements a specified base type.
        /// </summary>
        /// <param name="type">The type which needs to be checked.</param>
        /// <param name="baseType">The base type/interface which is expected to be inherited or implemented by the 'type'</param>
        /// <returns>Return true if 'type' inherits or implements 'baseType'. False otherwise</returns>        
        public static bool InheritsOrImplements(this Type type, Type baseType) {
            type = ResolveGenericType(type);
            baseType = ResolveGenericType(baseType);

            while (type != typeof(object)) {
                if (baseType == type || HasAnyInterfaces(type, baseType)) return true;
                
                type = ResolveGenericType(type.BaseType);
                if (type == null) return false;
            }
            
            return false;
        }
        
        static Type ResolveGenericType(Type type) {
            if (type is not { IsGenericType: true }) return type;

            var genericType = type.GetGenericTypeDefinition();
            return genericType != type ? genericType : type;
        }

        static bool HasAnyInterfaces(Type type, Type intefaceType) {
            return type.GetInterfaces().Any(i => ResolveGenericType(i) == intefaceType);
        }
    }
}
