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
                foreach (var t in newSnapshot.TypeNames.Keys.Except(oldSnapshot.TypeNames.Keys, StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal))
                {
                    var info = newSnapshot.TypeNames[t];
                    entries.Add(new MemberChangeEntry("Added", t, info.Access, "", info.Kind, "", "", "", ""));
                }
                foreach (var t in oldSnapshot.TypeNames.Keys.Except(newSnapshot.TypeNames.Keys, StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal))
                {
                    var info = oldSnapshot.TypeNames[t];
                    entries.Add(new MemberChangeEntry("Removed", t, info.Access, "", info.Kind, "", "", "", ""));
                }

                // Methods (including constructors)
                foreach (var key in newSnapshot.Methods.Keys.Except(oldSnapshot.Methods.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
                {
                    var m = newSnapshot.Methods[key];
                    string kind = ToMemberKind(m.MethodName);
                    entries.Add(new MemberChangeEntry("Added", m.TypeName, m.Access, m.Modifiers, kind, ToCSharpMethodName(m.MethodName, m.TypeName), "", m.ReturnType, m.Parameters));
                }
                foreach (var key in oldSnapshot.Methods.Keys.Except(newSnapshot.Methods.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
                {
                    var m = oldSnapshot.Methods[key];
                    string kind = ToMemberKind(m.MethodName);
                    entries.Add(new MemberChangeEntry("Removed", m.TypeName, m.Access, m.Modifiers, kind, ToCSharpMethodName(m.MethodName, m.TypeName), "", m.ReturnType, m.Parameters));
                }
                foreach (var key in oldSnapshot.Methods.Keys.Intersect(newSnapshot.Methods.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
                {
                    if (!oldSnapshot.Methods[key].IlBytes.AsSpan().SequenceEqual(newSnapshot.Methods[key].IlBytes.AsSpan()))
                    {
                        var m = newSnapshot.Methods[key];
                        string kind = ToMemberKind(m.MethodName);
                        entries.Add(new MemberChangeEntry("Modified", m.TypeName, m.Access, m.Modifiers, kind, ToCSharpMethodName(m.MethodName, m.TypeName), "", m.ReturnType, m.Parameters));
                    }
                }

                // Properties
                foreach (var key in newSnapshot.Properties.Keys.Except(oldSnapshot.Properties.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
                {
                    var p = newSnapshot.Properties[key];
                    entries.Add(new MemberChangeEntry("Added", p.TypeName, p.Access, p.Modifiers, "Property", p.PropertyName, p.PropertyType, "", ""));
                }
                foreach (var key in oldSnapshot.Properties.Keys.Except(newSnapshot.Properties.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
                {
                    var p = oldSnapshot.Properties[key];
                    entries.Add(new MemberChangeEntry("Removed", p.TypeName, p.Access, p.Modifiers, "Property", p.PropertyName, p.PropertyType, "", ""));
                }

                // Fields
                foreach (var key in newSnapshot.Fields.Keys.Except(oldSnapshot.Fields.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
                {
                    var f = newSnapshot.Fields[key];
                    entries.Add(new MemberChangeEntry("Added", f.TypeName, f.Access, f.Modifiers, "Field", f.FieldName, StripColonPrefix(f.Details), "", ""));
                }
                foreach (var key in oldSnapshot.Fields.Keys.Except(newSnapshot.Fields.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
                {
                    var f = oldSnapshot.Fields[key];
                    entries.Add(new MemberChangeEntry("Removed", f.TypeName, f.Access, f.Modifiers, "Field", f.FieldName, StripColonPrefix(f.Details), "", ""));
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
                snapshot.TypeNames[typeName] = new TypeInfo { Access = typeAccess, Kind = typeKind };

                // Methods
                foreach (var methodHandle in typeDef.GetMethods())
                {
                    var methodDef = reader.GetMethodDefinition(methodHandle);
                    string access = GetAccessModifier(methodDef.Attributes);
                    string modifiers = GetMethodModifiers(methodDef.Attributes);
                    string methodName = reader.GetString(methodDef.Name);
                    string matchKey = BuildMethodMatchKey(reader, typeName, methodDef, typeProvider);
                    var (retType, parameters) = BuildMethodSignatureParts(reader, methodDef, typeProvider);
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

                // Properties
                foreach (var propHandle in typeDef.GetProperties())
                {
                    var propDef = reader.GetPropertyDefinition(propHandle);
                    string propName = reader.GetString(propDef.Name);
                    string propKey = $"{typeName}::{propName}";
                    string propAccess = GetPropertyAccess(reader, propDef);
                    string propModifiers = GetPropertyModifiers(reader, propDef);
                    string propType = BuildPropertyType(reader, propDef, typeProvider);
                    string propDetails = BuildPropertyDetails(reader, propDef, typeProvider);

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

                // Fields
                foreach (var fieldHandle in typeDef.GetFields())
                {
                    var fieldDef = reader.GetFieldDefinition(fieldHandle);
                    string fieldName = reader.GetString(fieldDef.Name);
                    string fieldKey = $"{typeName}::{fieldName}";
                    string fieldAccess = GetFieldAccessModifier(fieldDef.Attributes);
                    string fieldModifiers = GetFieldModifiers(fieldDef.Attributes);
                    string fieldDetails = BuildFieldDetails(reader, fieldDef, typeProvider);

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

        /// <summary>Determine the member kind from the IL method name: .ctor → Constructor, .cctor → StaticConstructor, else → Method. / IL メソッド名からメンバー種別を判定。</summary>
        private static string ToMemberKind(string ilMethodName)
            => ilMethodName switch
            {
                ".ctor" => "Constructor",
                ".cctor" => "StaticConstructor",
                _ => "Method"
            };

        /// <summary>Convert IL method name to C# name: .ctor/.cctor → simple class name. / IL メソッド名を C# 名に変換。</summary>
        private static string ToCSharpMethodName(string ilMethodName, string typeName)
        {
            if (ilMethodName is ".ctor" or ".cctor")
            {
                // Extract simple class name from potentially nested or namespaced type name
                int slashIdx = typeName.LastIndexOf('/');
                string leaf = slashIdx >= 0 ? typeName[(slashIdx + 1)..] : typeName;
                int dotIdx = leaf.LastIndexOf('.');
                return dotIdx >= 0 ? leaf[(dotIdx + 1)..] : leaf;
            }
            return ilMethodName;
        }

        /// <summary>Strip leading ": " prefix from field/property details to extract the type portion. / フィールド・プロパティの詳細から先頭 ": " を除去して型部分を抽出。</summary>
        private static string StripColonPrefix(string details)
            => details.StartsWith(": ", StringComparison.Ordinal) ? details[2..] : details;

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

        /// <summary>
        /// Build separate return type and parameter strings for a method.
        /// メソッドの戻り値型とパラメータ文字列を個別に構築します。
        /// </summary>
        private static (string ReturnType, string Parameters) BuildMethodSignatureParts(MetadataReader reader, MethodDefinition methodDef, SimpleSignatureTypeProvider typeProvider)
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
        private static string BuildPropertyType(MetadataReader reader, PropertyDefinition propDef, SimpleSignatureTypeProvider typeProvider)
        {
            try
            {
                var sigBlobReader = reader.GetBlobReader(propDef.Signature);
                var decoder = new SignatureDecoder<string, object?>(typeProvider, reader, genericContext: null);
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

        /// <summary>Extract access modifier from type attributes. / TypeAttributes からアクセス修飾子を取得。</summary>
        private static string GetTypeAccessModifier(System.Reflection.TypeAttributes attributes)
        {
            var visibility = attributes & System.Reflection.TypeAttributes.VisibilityMask;
            return visibility switch
            {
                System.Reflection.TypeAttributes.Public => "public",
                System.Reflection.TypeAttributes.NotPublic => "internal",
                System.Reflection.TypeAttributes.NestedPublic => "public",
                System.Reflection.TypeAttributes.NestedFamily => "protected",
                System.Reflection.TypeAttributes.NestedFamORAssem => "protected internal",
                System.Reflection.TypeAttributes.NestedAssembly => "internal",
                System.Reflection.TypeAttributes.NestedFamANDAssem => "private protected",
                System.Reflection.TypeAttributes.NestedPrivate => "private",
                _ => "internal"
            };
        }

        /// <summary>
        /// Determine the type kind: Class, Record, Struct, Interface, or Enum.
        /// Record is detected heuristically by the presence of an EqualityContract property.
        /// 型の種別を判定: Class, Record, Struct, Interface, Enum。
        /// Record は EqualityContract プロパティの有無で推定。
        /// </summary>
        private static string GetTypeKind(MetadataReader reader, TypeDefinition typeDef)
        {
            var attributes = typeDef.Attributes;

            if ((attributes & System.Reflection.TypeAttributes.Interface) != 0)
                return "Interface";

            // Check base type for enum / struct (value type)
            if (!typeDef.BaseType.IsNil)
            {
                string baseTypeName = GetBaseTypeName(reader, typeDef.BaseType);
                if (baseTypeName is "System.Enum")
                    return "Enum";
                if (baseTypeName is "System.ValueType")
                    return "Struct";
            }

            // Heuristic: C# records have a compiler-generated EqualityContract property
            foreach (var propHandle in typeDef.GetProperties())
            {
                var propDef = reader.GetPropertyDefinition(propHandle);
                if (reader.GetString(propDef.Name) == "EqualityContract")
                    return "Record";
            }

            return "Class";
        }

        /// <summary>Get the full name of a base type from its EntityHandle.</summary>
        private static string GetBaseTypeName(MetadataReader reader, EntityHandle baseTypeHandle)
        {
            if (baseTypeHandle.Kind == HandleKind.TypeReference)
            {
                var typeRef = reader.GetTypeReference((TypeReferenceHandle)baseTypeHandle);
                string ns = reader.GetString(typeRef.Namespace);
                string name = reader.GetString(typeRef.Name);
                return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
            }
            if (baseTypeHandle.Kind == HandleKind.TypeDefinition)
            {
                var typeDef = reader.GetTypeDefinition((TypeDefinitionHandle)baseTypeHandle);
                return GetFullTypeName(reader, typeDef);
            }
            return "";
        }

        /// <summary>Extract non-access modifiers from method attributes (static, abstract, virtual, sealed, override, etc.).</summary>
        private static string GetMethodModifiers(MethodAttributes attributes)
        {
            var parts = new List<string>();
            if ((attributes & MethodAttributes.Static) != 0) parts.Add("static");
            if ((attributes & MethodAttributes.Abstract) != 0)
                parts.Add("abstract");
            else if ((attributes & MethodAttributes.Final) != 0 && (attributes & MethodAttributes.Virtual) != 0 && (attributes & MethodAttributes.NewSlot) == 0)
                parts.Add("sealed override");
            else if ((attributes & MethodAttributes.Virtual) != 0 && (attributes & MethodAttributes.NewSlot) != 0)
                parts.Add("virtual");
            else if ((attributes & MethodAttributes.Virtual) != 0)
                parts.Add("override");
            return string.Join(" ", parts);
        }

        /// <summary>Extract modifiers for a property by inspecting its getter/setter method attributes.</summary>
        private static string GetPropertyModifiers(MetadataReader reader, PropertyDefinition propDef)
        {
            var accessors = propDef.GetAccessors();
            MethodDefinition? accessor = !accessors.Getter.IsNil
                ? reader.GetMethodDefinition(accessors.Getter)
                : !accessors.Setter.IsNil ? reader.GetMethodDefinition(accessors.Setter) : null;
            return accessor.HasValue ? GetMethodModifiers(accessor.Value.Attributes) : "";
        }

        /// <summary>Extract modifiers from field attributes (static, readonly, const, volatile).</summary>
        private static string GetFieldModifiers(FieldAttributes attributes)
        {
            var parts = new List<string>();
            if ((attributes & FieldAttributes.Static) != 0 && (attributes & FieldAttributes.Literal) != 0)
            {
                parts.Add("const");
            }
            else
            {
                if ((attributes & FieldAttributes.Static) != 0) parts.Add("static");
                if ((attributes & FieldAttributes.InitOnly) != 0) parts.Add("readonly");
            }
            return string.Join(" ", parts);
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
