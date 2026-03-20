using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using FolderDiffIL4DotNet.Models;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Compares two .NET assemblies at the metadata level using <see cref="System.Reflection.Metadata"/>
    /// to detect type, method, property, and field additions/removals and method body changes.
    /// <see cref="System.Reflection.Metadata"/> を使用して 2 つの .NET アセンブリのメタデータを比較し、
    /// 型・メソッド・プロパティ・フィールドの増減およびメソッドボディの変更を検出します。
    /// </summary>
    internal static class AssemblyMethodAnalyzer
    {
        /// <summary>
        /// Analyses two assembly files and returns a summary of member-level changes.
        /// Returns <see langword="null"/> if analysis fails (best-effort).
        /// 2 つのアセンブリファイルを解析し、メンバーレベルの変更要約を返します。
        /// 解析に失敗した場合は <see langword="null"/> を返します（ベストエフォート）。
        /// </summary>
        public static MethodLevelChangesSummary? Analyze(string oldAssemblyPath, string newAssemblyPath)
        {
            try
            {
                var oldSnapshot = ReadAssemblySnapshot(oldAssemblyPath);
                var newSnapshot = ReadAssemblySnapshot(newAssemblyPath);

                // Types
                var addedTypes = newSnapshot.TypeNames.Except(oldSnapshot.TypeNames, StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal).ToList();
                var removedTypes = oldSnapshot.TypeNames.Except(newSnapshot.TypeNames, StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal).ToList();

                // Methods
                var addedMethods = newSnapshot.Methods.Keys.Except(oldSnapshot.Methods.Keys, StringComparer.Ordinal).OrderBy(m => m, StringComparer.Ordinal).ToList();
                var removedMethods = oldSnapshot.Methods.Keys.Except(newSnapshot.Methods.Keys, StringComparer.Ordinal).OrderBy(m => m, StringComparer.Ordinal).ToList();

                var bodyChanged = new List<string>();
                foreach (var key in oldSnapshot.Methods.Keys.Intersect(newSnapshot.Methods.Keys, StringComparer.Ordinal).OrderBy(m => m, StringComparer.Ordinal))
                {
                    if (!oldSnapshot.Methods[key].AsSpan().SequenceEqual(newSnapshot.Methods[key].AsSpan()))
                    {
                        bodyChanged.Add(key);
                    }
                }

                // Properties
                var addedProperties = newSnapshot.PropertyNames.Except(oldSnapshot.PropertyNames, StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToList();
                var removedProperties = oldSnapshot.PropertyNames.Except(newSnapshot.PropertyNames, StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToList();

                // Fields
                var addedFields = newSnapshot.FieldNames.Except(oldSnapshot.FieldNames, StringComparer.Ordinal).OrderBy(f => f, StringComparer.Ordinal).ToList();
                var removedFields = oldSnapshot.FieldNames.Except(newSnapshot.FieldNames, StringComparer.Ordinal).OrderBy(f => f, StringComparer.Ordinal).ToList();

                return new MethodLevelChangesSummary
                {
                    AddedTypes = addedTypes,
                    RemovedTypes = removedTypes,
                    OldMethodCount = oldSnapshot.Methods.Count,
                    NewMethodCount = newSnapshot.Methods.Count,
                    AddedMethods = addedMethods,
                    RemovedMethods = removedMethods,
                    BodyChangedMethods = bodyChanged,
                    AddedProperties = addedProperties,
                    RemovedProperties = removedProperties,
                    AddedFields = addedFields,
                    RemovedFields = removedFields,
                };
            }
#pragma warning disable CA1031 // ベストエフォート解析のため全例外をキャッチ / Catch-all for best-effort analysis
            catch
            {
                return null;
            }
#pragma warning restore CA1031
        }

        // ── Internal snapshot ────────────────────────────────────────────────

        private sealed class AssemblySnapshot
        {
            public HashSet<string> TypeNames { get; } = new(StringComparer.Ordinal);
            public Dictionary<string, byte[]> Methods { get; } = new(StringComparer.Ordinal);
            public HashSet<string> PropertyNames { get; } = new(StringComparer.Ordinal);
            public HashSet<string> FieldNames { get; } = new(StringComparer.Ordinal);
        }

        private static AssemblySnapshot ReadAssemblySnapshot(string assemblyPath)
        {
            var snapshot = new AssemblySnapshot();
            using var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(stream);
            var reader = peReader.GetMetadataReader();
            var typeProvider = new SimpleSignatureTypeProvider(reader);

            foreach (var typeHandle in reader.TypeDefinitions)
            {
                var typeDef = reader.GetTypeDefinition(typeHandle);
                string typeName = GetFullTypeName(reader, typeDef);

                // Skip the special <Module> type
                if (typeName == "<Module>") continue;

                snapshot.TypeNames.Add(typeName);

                // Methods
                foreach (var methodHandle in typeDef.GetMethods())
                {
                    var methodDef = reader.GetMethodDefinition(methodHandle);
                    string methodKey = BuildMethodKey(reader, typeName, methodDef, typeProvider);

                    byte[] ilBytes = ReadIlBytes(peReader, methodDef);
                    snapshot.Methods[methodKey] = ilBytes;
                }

                // Properties
                foreach (var propHandle in typeDef.GetProperties())
                {
                    var propDef = reader.GetPropertyDefinition(propHandle);
                    string propName = reader.GetString(propDef.Name);
                    string propKey = $"{typeName}::{propName}";
                    snapshot.PropertyNames.Add(propKey);
                }

                // Fields
                foreach (var fieldHandle in typeDef.GetFields())
                {
                    var fieldDef = reader.GetFieldDefinition(fieldHandle);
                    string fieldName = reader.GetString(fieldDef.Name);
                    string fieldKey = $"{typeName}::{fieldName}";
                    snapshot.FieldNames.Add(fieldKey);
                }
            }

            return snapshot;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string GetFullTypeName(MetadataReader reader, TypeDefinition typeDef)
        {
            string name = reader.GetString(typeDef.Name);
            string ns = reader.GetString(typeDef.Namespace);

            // Handle nested types
            if (typeDef.IsNested)
            {
                var declaringType = reader.GetTypeDefinition(typeDef.GetDeclaringType());
                string parentName = GetFullTypeName(reader, declaringType);
                return $"{parentName}/{name}";
            }

            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }

        private static string BuildMethodKey(MetadataReader reader, string typeName, MethodDefinition methodDef, SimpleSignatureTypeProvider typeProvider)
        {
            string methodName = reader.GetString(methodDef.Name);
            string accessModifier = GetAccessModifier(methodDef.Attributes);

            try
            {
                var sigBlobReader = reader.GetBlobReader(methodDef.Signature);
                var decoder = new SignatureDecoder<string, object?>(typeProvider, reader, genericContext: null);
                var signature = decoder.DecodeMethodSignature(ref sigBlobReader);
                string parameters = string.Join(", ", signature.ParameterTypes);
                string returnType = signature.ReturnType;
                return $"[{accessModifier}] {typeName}::{methodName}({parameters}) : {returnType}";
            }
#pragma warning disable CA1031 // シグネチャデコード失敗時のフォールバック / Fallback when signature decoding fails
            catch
            {
                // Fallback: use raw signature blob hex for uniqueness
                var sigBytes = reader.GetBlobBytes(methodDef.Signature);
                string sigHex = Convert.ToHexString(sigBytes);
                return $"[{accessModifier}] {typeName}::{methodName}(#{sigHex})";
            }
#pragma warning restore CA1031
        }

        private static string GetAccessModifier(MethodAttributes attributes)
        {
            var access = attributes & MethodAttributes.MemberAccessMask;
            return access switch
            {
                MethodAttributes.Public => "public",
                MethodAttributes.Family => "protected",
                MethodAttributes.FamORAssem => "protected internal",
                MethodAttributes.Assembly => "internal",
                MethodAttributes.FamANDAssem => "private protected",
                MethodAttributes.Private => "private",
                _ => "private"
            };
        }

        private static byte[] ReadIlBytes(PEReader peReader, MethodDefinition methodDef)
        {
            if (methodDef.RelativeVirtualAddress == 0) return [];

            try
            {
                var body = peReader.GetMethodBody(methodDef.RelativeVirtualAddress);
                return body.GetILBytes() ?? [];
            }
#pragma warning disable CA1031 // ベストエフォートの IL バイト読み取り / Best-effort IL body read
            catch
            {
                return [];
            }
#pragma warning restore CA1031
        }

        // ── Signature type provider ──────────────────────────────────────────

        /// <summary>
        /// Minimal <see cref="ISignatureTypeProvider{TType, TGenericContext}"/> that decodes
        /// method parameter and return types into human-readable strings.
        /// メソッドパラメータおよび戻り値の型を可読文字列にデコードする最小限の実装。
        /// </summary>
        private sealed class SimpleSignatureTypeProvider : ISignatureTypeProvider<string, object?>
        {
            private readonly MetadataReader _reader;

            public SimpleSignatureTypeProvider(MetadataReader reader) => _reader = reader;

            public string GetPrimitiveType(PrimitiveTypeCode typeCode)
                => typeCode switch
                {
                    PrimitiveTypeCode.Void => "void",
                    PrimitiveTypeCode.Boolean => "bool",
                    PrimitiveTypeCode.Char => "char",
                    PrimitiveTypeCode.SByte => "sbyte",
                    PrimitiveTypeCode.Byte => "byte",
                    PrimitiveTypeCode.Int16 => "short",
                    PrimitiveTypeCode.UInt16 => "ushort",
                    PrimitiveTypeCode.Int32 => "int",
                    PrimitiveTypeCode.UInt32 => "uint",
                    PrimitiveTypeCode.Int64 => "long",
                    PrimitiveTypeCode.UInt64 => "ulong",
                    PrimitiveTypeCode.Single => "float",
                    PrimitiveTypeCode.Double => "double",
                    PrimitiveTypeCode.String => "string",
                    PrimitiveTypeCode.Object => "object",
                    PrimitiveTypeCode.IntPtr => "nint",
                    PrimitiveTypeCode.UIntPtr => "nuint",
                    PrimitiveTypeCode.TypedReference => "TypedReference",
                    _ => typeCode.ToString()
                };

            public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
            {
                var typeDef = reader.GetTypeDefinition(handle);
                return GetFullTypeName(reader, typeDef);
            }

            public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            {
                var typeRef = reader.GetTypeReference(handle);
                string ns = reader.GetString(typeRef.Namespace);
                string name = reader.GetString(typeRef.Name);
                return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
            }

            public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            {
                var sigReader = reader.GetBlobReader(reader.GetTypeSpecification(handle).Signature);
                return new SignatureDecoder<string, object?>(this, reader, genericContext).DecodeType(ref sigReader);
            }

            public string GetSZArrayType(string elementType) => $"{elementType}[]";
            public string GetPointerType(string elementType) => $"{elementType}*";
            public string GetByReferenceType(string elementType) => $"{elementType}&";
            public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
                => $"{genericType}<{string.Join(", ", typeArguments)}>";
            public string GetGenericMethodParameter(object? genericContext, int index) => $"!!{index}";
            public string GetGenericTypeParameter(object? genericContext, int index) => $"!{index}";
            public string GetPinnedType(string elementType) => elementType;
            public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
            public string GetArrayType(string elementType, ArrayShape shape)
                => $"{elementType}[{new string(',', shape.Rank - 1)}]";
            public string GetFunctionPointerType(MethodSignature<string> signature) => "delegate*";
        }
    }
}
