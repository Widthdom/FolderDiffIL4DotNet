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
    /// Returns structured <see cref="MemberChangeEntry"/> records for table-style rendering.
    /// <see cref="System.Reflection.Metadata"/> を使用して 2 つの .NET アセンブリのメタデータを比較し、
    /// 型・メソッド・プロパティ・フィールドの増減およびメソッドボディの変更を検出します。
    /// 表形式レンダリング向けの構造化 <see cref="MemberChangeEntry"/> レコードを返します。
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

                var entries = new List<MemberChangeEntry>();

                // Types
                foreach (var t in newSnapshot.TypeNames.Except(oldSnapshot.TypeNames, StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal))
                    entries.Add(new MemberChangeEntry("Added", t, "", "Type", "", ""));
                foreach (var t in oldSnapshot.TypeNames.Except(newSnapshot.TypeNames, StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal))
                    entries.Add(new MemberChangeEntry("Removed", t, "", "Type", "", ""));

                // Methods
                foreach (var key in newSnapshot.Methods.Keys.Except(oldSnapshot.Methods.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
                {
                    var m = newSnapshot.Methods[key];
                    entries.Add(new MemberChangeEntry("Added", m.TypeName, m.Access, "Method", m.MethodName, m.Details));
                }
                foreach (var key in oldSnapshot.Methods.Keys.Except(newSnapshot.Methods.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
                {
                    var m = oldSnapshot.Methods[key];
                    entries.Add(new MemberChangeEntry("Removed", m.TypeName, m.Access, "Method", m.MethodName, m.Details));
                }
                foreach (var key in oldSnapshot.Methods.Keys.Intersect(newSnapshot.Methods.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
                {
                    if (!oldSnapshot.Methods[key].IlBytes.AsSpan().SequenceEqual(newSnapshot.Methods[key].IlBytes.AsSpan()))
                    {
                        var m = newSnapshot.Methods[key];
                        entries.Add(new MemberChangeEntry("Modified", m.TypeName, m.Access, "Method", m.MethodName, m.Details));
                    }
                }

                // Properties
                foreach (var key in newSnapshot.Properties.Keys.Except(oldSnapshot.Properties.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
                {
                    var p = newSnapshot.Properties[key];
                    entries.Add(new MemberChangeEntry("Added", p.TypeName, p.Access, "Property", p.PropertyName, p.Details));
                }
                foreach (var key in oldSnapshot.Properties.Keys.Except(newSnapshot.Properties.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
                {
                    var p = oldSnapshot.Properties[key];
                    entries.Add(new MemberChangeEntry("Removed", p.TypeName, p.Access, "Property", p.PropertyName, p.Details));
                }

                // Fields
                foreach (var key in newSnapshot.Fields.Keys.Except(oldSnapshot.Fields.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
                {
                    var f = newSnapshot.Fields[key];
                    entries.Add(new MemberChangeEntry("Added", f.TypeName, f.Access, "Field", f.FieldName, f.Details));
                }
                foreach (var key in oldSnapshot.Fields.Keys.Except(newSnapshot.Fields.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
                {
                    var f = oldSnapshot.Fields[key];
                    entries.Add(new MemberChangeEntry("Removed", f.TypeName, f.Access, "Field", f.FieldName, f.Details));
                }

                return new MethodLevelChangesSummary
                {
                    Entries = entries,
                    OldMethodCount = oldSnapshot.Methods.Count,
                    NewMethodCount = newSnapshot.Methods.Count,
                };
            }
#pragma warning disable CA1031 // ベストエフォート解析のため全例外をキャッチ / Catch-all for best-effort analysis
            catch
            {
                return null;
            }
#pragma warning restore CA1031
        }

        // ── Internal snapshot data ──────────────────────────────────────────

        private sealed class MethodDetail
        {
            public required string TypeName { get; init; }
            public required string Access { get; init; }
            public required string MethodName { get; init; }
            public required string Details { get; init; }
            public required byte[] IlBytes { get; init; }
        }

        private sealed class PropertyDetail
        {
            public required string TypeName { get; init; }
            public required string Access { get; init; }
            public required string PropertyName { get; init; }
            public required string Details { get; init; }
        }

        private sealed class FieldDetail
        {
            public required string TypeName { get; init; }
            public required string Access { get; init; }
            public required string FieldName { get; init; }
            public required string Details { get; init; }
        }

        private sealed class AssemblySnapshot
        {
            public HashSet<string> TypeNames { get; } = new(StringComparer.Ordinal);
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

                snapshot.TypeNames.Add(typeName);

                // Methods
                foreach (var methodHandle in typeDef.GetMethods())
                {
                    var methodDef = reader.GetMethodDefinition(methodHandle);
                    string access = GetAccessModifier(methodDef.Attributes);
                    string methodName = reader.GetString(methodDef.Name);
                    string matchKey = BuildMethodMatchKey(reader, typeName, methodDef, typeProvider);
                    string details = BuildMethodDetails(reader, methodDef, typeProvider);
                    byte[] ilBytes = ReadIlBytes(peReader, methodDef);

                    snapshot.Methods[matchKey] = new MethodDetail
                    {
                        TypeName = typeName,
                        Access = access,
                        MethodName = methodName,
                        Details = details,
                        IlBytes = ilBytes,
                    };
                }

                // Properties
                foreach (var propHandle in typeDef.GetProperties())
                {
                    var propDef = reader.GetPropertyDefinition(propHandle);
                    string propName = reader.GetString(propDef.Name);
                    string propKey = $"{typeName}::{propName}";
                    string propAccess = GetPropertyAccess(reader, propDef);
                    string propDetails = BuildPropertyDetails(reader, propDef, typeProvider);

                    snapshot.Properties[propKey] = new PropertyDetail
                    {
                        TypeName = typeName,
                        Access = propAccess,
                        PropertyName = propName,
                        Details = propDetails,
                    };
                }

                // Fields
                foreach (var fieldHandle in typeDef.GetFields())
                {
                    var fieldDef = reader.GetFieldDefinition(fieldHandle);
                    string fieldName = reader.GetString(fieldDef.Name);
                    string fieldKey = $"{typeName}::{fieldName}";
                    string fieldAccess = GetFieldAccessModifier(fieldDef.Attributes);
                    string fieldDetails = BuildFieldDetails(reader, fieldDef, typeProvider);

                    snapshot.Fields[fieldKey] = new FieldDetail
                    {
                        TypeName = typeName,
                        Access = fieldAccess,
                        FieldName = fieldName,
                        Details = fieldDetails,
                    };
                }
            }

            return snapshot;
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

        /// <summary>Build a key for method matching (without access modifier so access changes don't cause false add/remove pairs).</summary>
        private static string BuildMethodMatchKey(MetadataReader reader, string typeName, MethodDefinition methodDef, SimpleSignatureTypeProvider typeProvider)
        {
            string methodName = reader.GetString(methodDef.Name);

            try
            {
                var sigBlobReader = reader.GetBlobReader(methodDef.Signature);
                var decoder = new SignatureDecoder<string, object?>(typeProvider, reader, genericContext: null);
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

        /// <summary>Build human-readable details for a method: "(Type paramName, Type paramName = defaultValue) : ReturnType".</summary>
        private static string BuildMethodDetails(MetadataReader reader, MethodDefinition methodDef, SimpleSignatureTypeProvider typeProvider)
        {
            try
            {
                var sigBlobReader = reader.GetBlobReader(methodDef.Signature);
                var decoder = new SignatureDecoder<string, object?>(typeProvider, reader, genericContext: null);
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

                return $"({string.Join(", ", parts)}) : {signature.ReturnType}";
            }
#pragma warning disable CA1031
            catch
            {
                return "";
            }
#pragma warning restore CA1031
        }

        /// <summary>Build property details: ": Type { get; set; }".</summary>
        private static string BuildPropertyDetails(MetadataReader reader, PropertyDefinition propDef, SimpleSignatureTypeProvider typeProvider)
        {
            try
            {
                var sigBlobReader = reader.GetBlobReader(propDef.Signature);
                var decoder = new SignatureDecoder<string, object?>(typeProvider, reader, genericContext: null);
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
        private static string BuildFieldDetails(MetadataReader reader, FieldDefinition fieldDef, SimpleSignatureTypeProvider typeProvider)
        {
            try
            {
                string fieldType = fieldDef.DecodeSignature(typeProvider, null);
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

        private static string GetFieldAccessModifier(FieldAttributes attributes)
        {
            var access = attributes & FieldAttributes.FieldAccessMask;
            return access switch
            {
                FieldAttributes.Public => "public",
                FieldAttributes.Family => "protected",
                FieldAttributes.FamORAssem => "protected internal",
                FieldAttributes.Assembly => "internal",
                FieldAttributes.FamANDAssem => "private protected",
                FieldAttributes.Private => "private",
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

        private static string ReadConstantValue(MetadataReader reader, ConstantHandle handle)
        {
            if (handle.IsNil) return "";
            var constant = reader.GetConstant(handle);
            var blobReader = reader.GetBlobReader(constant.Value);

            try
            {
                return constant.TypeCode switch
                {
                    ConstantTypeCode.Boolean => blobReader.ReadBoolean() ? "true" : "false",
                    ConstantTypeCode.Char => $"'{(char)blobReader.ReadUInt16()}'",
                    ConstantTypeCode.SByte => blobReader.ReadSByte().ToString(),
                    ConstantTypeCode.Byte => blobReader.ReadByte().ToString(),
                    ConstantTypeCode.Int16 => blobReader.ReadInt16().ToString(),
                    ConstantTypeCode.UInt16 => blobReader.ReadUInt16().ToString(),
                    ConstantTypeCode.Int32 => blobReader.ReadInt32().ToString(),
                    ConstantTypeCode.UInt32 => blobReader.ReadUInt32().ToString(),
                    ConstantTypeCode.Int64 => blobReader.ReadInt64().ToString(),
                    ConstantTypeCode.UInt64 => blobReader.ReadUInt64().ToString(),
                    ConstantTypeCode.Single => blobReader.ReadSingle().ToString(),
                    ConstantTypeCode.Double => blobReader.ReadDouble().ToString(),
                    ConstantTypeCode.String => $"\"{blobReader.ReadSerializedString() ?? ""}\"",
                    ConstantTypeCode.NullReference => "null",
                    _ => ""
                };
            }
#pragma warning disable CA1031
            catch
            {
                return "";
            }
#pragma warning restore CA1031
        }

        // ── Signature type provider ──────────────────────────────────────────

        /// <summary>
        /// Minimal <see cref="ISignatureTypeProvider{TType, TGenericContext}"/> that decodes
        /// method parameter and return types into human-readable strings.
        /// メソッドパラメータおよび戻り値の型を可読文字列にデコードする最小限の実装。
        /// </summary>
        internal sealed class SimpleSignatureTypeProvider : ISignatureTypeProvider<string, object?>
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
