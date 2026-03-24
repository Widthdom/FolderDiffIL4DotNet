using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Metadata reading helpers: snapshot data classes, snapshot construction,
    /// type name resolution, and signature/property/field detail builders.
    /// メタデータ読取りヘルパー群: スナップショットデータクラス、スナップショット構築、
    /// 型名解決、およびシグネチャ/プロパティ/フィールド詳細ビルダー。
    /// </summary>
    internal static partial class AssemblyMethodAnalyzer
    {
        // ── Internal snapshot data ──────────────────────────────────────────

        private sealed class MethodDetail
        {
            public required string TypeName { get; init; }
            public required string Access { get; init; }
            public required string Modifiers { get; init; }
            public required string MethodName { get; init; }
            public required string ReturnType { get; init; }
            public required string Parameters { get; init; }
            public required byte[] IlBytes { get; init; }
        }

        private sealed class PropertyDetail
        {
            public required string TypeName { get; init; }
            public required string Access { get; init; }
            public required string Modifiers { get; init; }
            public required string PropertyName { get; init; }
            public required string PropertyType { get; init; }
            public required string Details { get; init; }
        }

        private sealed class FieldDetail
        {
            public required string TypeName { get; init; }
            public required string Access { get; init; }
            public required string Modifiers { get; init; }
            public required string FieldName { get; init; }
            public required string Details { get; init; }
        }

        private sealed class TypeInfo
        {
            public required string Access { get; init; }
            public required string Kind { get; init; }
            public required string BaseType { get; init; }
            public required string Modifiers { get; init; }
        }

        private sealed class AssemblySnapshot
        {
            public Dictionary<string, TypeInfo> TypeNames { get; } = new(StringComparer.Ordinal);
            public Dictionary<string, MethodDetail> Methods { get; } = new(StringComparer.Ordinal);
            public Dictionary<string, PropertyDetail> Properties { get; } = new(StringComparer.Ordinal);
            public Dictionary<string, FieldDetail> Fields { get; } = new(StringComparer.Ordinal);
        }

        // ── Snapshot construction ───────────────────────────────────────────

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

                string typeAccess = GetTypeAccessModifier(typeDef.Attributes);
                string typeKind = GetTypeKind(reader, typeDef);
                string baseType = GetBaseTypeDisplayName(reader, typeDef);
                string typeModifiers = GetTypeModifiers(typeDef.Attributes);
                snapshot.TypeNames[typeName] = new TypeInfo { Access = typeAccess, Kind = typeKind, BaseType = baseType, Modifiers = typeModifiers };

                // Build type-level generic context once per type / 型レベルのジェネリックコンテキストを型ごとに1回構築
                var typeGenericContext = GenericContext.FromType(reader, typeDef);

                ReadMethodsFromType(reader, peReader, typeDef, typeName, typeProvider, typeGenericContext, snapshot);
                ReadPropertiesFromType(reader, typeDef, typeName, typeProvider, typeGenericContext, snapshot);
                ReadFieldsFromType(reader, typeDef, typeName, typeProvider, typeGenericContext, snapshot);
            }

            return snapshot;
        }

        private static void ReadMethodsFromType(MetadataReader reader, PEReader peReader, TypeDefinition typeDef, string typeName, SimpleSignatureTypeProvider typeProvider, GenericContext typeGenericContext, AssemblySnapshot snapshot)
        {
            foreach (var methodHandle in typeDef.GetMethods())
            {
                var methodDef = reader.GetMethodDefinition(methodHandle);
                string access = GetAccessModifier(methodDef.Attributes);
                string modifiers = GetMethodModifiers(methodDef.Attributes);
                string methodName = reader.GetString(methodDef.Name);

                // Build method-level generic context (type + method params) / メソッドレベルのジェネリックコンテキストを構築
                var methodGenericContext = GenericContext.FromMethod(reader, typeDef, methodDef);

                string matchKey = BuildMethodMatchKey(reader, typeName, methodDef, typeProvider, methodGenericContext);
                var (retType, parameters) = BuildMethodSignatureParts(reader, methodDef, typeProvider, methodGenericContext);
                byte[] ilBytes = ReadIlBytes(peReader, methodDef);

                snapshot.Methods[matchKey] = new MethodDetail
                {
                    TypeName = typeName,
                    Access = access,
                    Modifiers = modifiers,
                    MethodName = methodName,
                    ReturnType = retType,
                    Parameters = parameters,
                    IlBytes = ilBytes,
                };
            }
        }

        private static void ReadPropertiesFromType(MetadataReader reader, TypeDefinition typeDef, string typeName, SimpleSignatureTypeProvider typeProvider, GenericContext typeGenericContext, AssemblySnapshot snapshot)
        {
            foreach (var propHandle in typeDef.GetProperties())
            {
                var propDef = reader.GetPropertyDefinition(propHandle);
                string propName = reader.GetString(propDef.Name);
                string propKey = $"{typeName}::{propName}";
                string propAccess = GetPropertyAccess(reader, propDef);
                string propModifiers = GetPropertyModifiers(reader, propDef);
                string propType = BuildPropertyType(reader, propDef, typeProvider, typeGenericContext);
                string propDetails = BuildPropertyDetails(reader, propDef, typeProvider, typeGenericContext);

                snapshot.Properties[propKey] = new PropertyDetail
                {
                    TypeName = typeName,
                    Access = propAccess,
                    Modifiers = propModifiers,
                    PropertyName = propName,
                    PropertyType = propType,
                    Details = propDetails,
                };
            }
        }

        private static void ReadFieldsFromType(MetadataReader reader, TypeDefinition typeDef, string typeName, SimpleSignatureTypeProvider typeProvider, GenericContext typeGenericContext, AssemblySnapshot snapshot)
        {
            foreach (var fieldHandle in typeDef.GetFields())
            {
                var fieldDef = reader.GetFieldDefinition(fieldHandle);
                string fieldName = reader.GetString(fieldDef.Name);
                string fieldKey = $"{typeName}::{fieldName}";
                string fieldAccess = GetFieldAccessModifier(fieldDef.Attributes);
                string fieldModifiers = GetFieldModifiers(fieldDef.Attributes);
                string fieldDetails = BuildFieldDetails(reader, fieldDef, typeProvider, typeGenericContext);

                snapshot.Fields[fieldKey] = new FieldDetail
                {
                    TypeName = typeName,
                    Access = fieldAccess,
                    Modifiers = fieldModifiers,
                    FieldName = fieldName,
                    Details = fieldDetails,
                };
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string GetFullTypeName(MetadataReader reader, TypeDefinition typeDef)
        {
            string name = reader.GetString(typeDef.Name);
            string ns = reader.GetString(typeDef.Namespace);

            if (typeDef.IsNested)
            {
                var declaringType = reader.GetTypeDefinition(typeDef.GetDeclaringType());
                string parentName = GetFullTypeName(reader, declaringType);
                return $"{parentName}/{name}";
            }

            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }

        /// <summary>
        /// Build a key for method matching (without access modifier so access changes
        /// don't cause false add/remove pairs; access changes are detected separately
        /// as Modified entries).
        /// アクセス修飾子の変更が誤った追加/削除ペアを生じさせないよう、アクセス修飾子を
        /// 含まないメソッド一致キーを構築します。アクセス修飾子の変更は別途 Modified として検出します。
        /// </summary>
        private static string BuildMethodMatchKey(MetadataReader reader, string typeName, MethodDefinition methodDef, SimpleSignatureTypeProvider typeProvider, GenericContext? genericContext)
        {
            string methodName = reader.GetString(methodDef.Name);

            try
            {
                var sigBlobReader = reader.GetBlobReader(methodDef.Signature);
                var decoder = new SignatureDecoder<string, GenericContext?>(typeProvider, reader, genericContext);
                var signature = decoder.DecodeMethodSignature(ref sigBlobReader);
                string parameters = string.Join(", ", signature.ParameterTypes);
                return $"{typeName}::{methodName}({parameters}) : {signature.ReturnType}";
            }
#pragma warning disable CA1031 // シグネチャデコード失敗時のフォールバック / Fallback when signature decoding fails
            catch
            {
                var sigBytes = reader.GetBlobBytes(methodDef.Signature);
                return $"{typeName}::{methodName}(#{Convert.ToHexString(sigBytes)})";
            }
#pragma warning restore CA1031
        }

        /// <summary>
        /// Build separate return type and parameter strings for a method.
        /// メソッドの戻り値型とパラメータ文字列を個別に構築します。
        /// </summary>
        private static (string ReturnType, string Parameters) BuildMethodSignatureParts(MetadataReader reader, MethodDefinition methodDef, SimpleSignatureTypeProvider typeProvider, GenericContext? genericContext)
        {
            try
            {
                var sigBlobReader = reader.GetBlobReader(methodDef.Signature);
                var decoder = new SignatureDecoder<string, GenericContext?>(typeProvider, reader, genericContext);
                var signature = decoder.DecodeMethodSignature(ref sigBlobReader);

                // Collect parameter metadata (names and default values)
                var paramsBySeq = new Dictionary<int, Parameter>();
                foreach (var paramHandle in methodDef.GetParameters())
                {
                    var param = reader.GetParameter(paramHandle);
                    if (param.SequenceNumber > 0)
                        paramsBySeq[param.SequenceNumber] = param;
                }

                var parts = new List<string>();
                for (int i = 0; i < signature.ParameterTypes.Length; i++)
                {
                    string paramType = signature.ParameterTypes[i];
                    string part;

                    if (paramsBySeq.TryGetValue(i + 1, out var param))
                    {
                        string paramName = reader.GetString(param.Name);
                        part = string.IsNullOrEmpty(paramName) ? paramType : $"{paramType} {paramName}";

                        var defaultHandle = param.GetDefaultValue();
                        if (!defaultHandle.IsNil)
                        {
                            string defaultVal = ReadConstantValue(reader, defaultHandle);
                            if (!string.IsNullOrEmpty(defaultVal))
                                part += $" = {defaultVal}";
                        }
                    }
                    else
                    {
                        part = paramType;
                    }
                    parts.Add(part);
                }

                return (signature.ReturnType, string.Join(", ", parts));
            }
#pragma warning disable CA1031
            catch
            {
                return ("", "");
            }
#pragma warning restore CA1031
        }

        /// <summary>Extract the declared type of a property (without accessor info). / プロパティの宣言型を抽出（アクセサ情報なし）。</summary>
        private static string BuildPropertyType(MetadataReader reader, PropertyDefinition propDef, SimpleSignatureTypeProvider typeProvider, GenericContext? genericContext)
        {
            try
            {
                var sigBlobReader = reader.GetBlobReader(propDef.Signature);
                var decoder = new SignatureDecoder<string, GenericContext?>(typeProvider, reader, genericContext);
                var signature = decoder.DecodeMethodSignature(ref sigBlobReader);
                return signature.ReturnType;
            }
#pragma warning disable CA1031
            catch
            {
                return "";
            }
#pragma warning restore CA1031
        }

        /// <summary>Build property details: ": Type { get; set; }".</summary>
        private static string BuildPropertyDetails(MetadataReader reader, PropertyDefinition propDef, SimpleSignatureTypeProvider typeProvider, GenericContext? genericContext)
        {
            try
            {
                var sigBlobReader = reader.GetBlobReader(propDef.Signature);
                var decoder = new SignatureDecoder<string, GenericContext?>(typeProvider, reader, genericContext);
                var signature = decoder.DecodeMethodSignature(ref sigBlobReader);
                string propType = signature.ReturnType;

                var accessors = propDef.GetAccessors();
                string accessorInfo = (!accessors.Getter.IsNil, !accessors.Setter.IsNil) switch
                {
                    (true, true) => " { get; set; }",
                    (true, false) => " { get; }",
                    (false, true) => " { set; }",
                    _ => ""
                };

                return $": {propType}{accessorInfo}";
            }
#pragma warning disable CA1031
            catch
            {
                return "";
            }
#pragma warning restore CA1031
        }

        /// <summary>Build field details: ": Type" or ": Type = defaultValue".</summary>
        private static string BuildFieldDetails(MetadataReader reader, FieldDefinition fieldDef, SimpleSignatureTypeProvider typeProvider, GenericContext? genericContext)
        {
            try
            {
                string fieldType = fieldDef.DecodeSignature(typeProvider, genericContext);
                string result = $": {fieldType}";

                var defaultHandle = fieldDef.GetDefaultValue();
                if (!defaultHandle.IsNil)
                {
                    string defaultVal = ReadConstantValue(reader, defaultHandle);
                    if (!string.IsNullOrEmpty(defaultVal))
                        result += $" = {defaultVal}";
                }

                return result;
            }
#pragma warning disable CA1031
            catch
            {
                return "";
            }
#pragma warning restore CA1031
        }

        private static string GetPropertyAccess(MetadataReader reader, PropertyDefinition propDef)
        {
            var accessors = propDef.GetAccessors();
            if (!accessors.Getter.IsNil)
                return GetAccessModifier(reader.GetMethodDefinition(accessors.Getter).Attributes);
            if (!accessors.Setter.IsNil)
                return GetAccessModifier(reader.GetMethodDefinition(accessors.Setter).Attributes);
            return "";
        }
    }
}
