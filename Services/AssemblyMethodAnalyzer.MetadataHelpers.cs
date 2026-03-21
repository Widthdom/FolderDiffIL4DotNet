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
    /// Metadata reading helpers: snapshot construction, access/modifier extraction,
    /// signature decoding, and the <see cref="SimpleSignatureTypeProvider"/>.
    /// メタデータ読取りヘルパー群: スナップショット構築、アクセス修飾子/修飾子抽出、
    /// シグネチャデコード、および <see cref="SimpleSignatureTypeProvider"/>。
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

                ReadMethodsFromType(reader, peReader, typeDef, typeName, typeProvider, snapshot);
                ReadPropertiesFromType(reader, typeDef, typeName, typeProvider, snapshot);
                ReadFieldsFromType(reader, typeDef, typeName, typeProvider, snapshot);
            }

            return snapshot;
        }

        private static void ReadMethodsFromType(MetadataReader reader, PEReader peReader, TypeDefinition typeDef, string typeName, SimpleSignatureTypeProvider typeProvider, AssemblySnapshot snapshot)
        {
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
        }

        private static void ReadPropertiesFromType(MetadataReader reader, TypeDefinition typeDef, string typeName, SimpleSignatureTypeProvider typeProvider, AssemblySnapshot snapshot)
        {
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
        }

        private static void ReadFieldsFromType(MetadataReader reader, TypeDefinition typeDef, string typeName, SimpleSignatureTypeProvider typeProvider, AssemblySnapshot snapshot)
        {
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

        /// <summary>Extract non-access modifiers from type attributes (sealed, abstract, static). / TypeAttributes から非アクセス修飾子を取得。</summary>
        private static string GetTypeModifiers(System.Reflection.TypeAttributes attributes)
        {
            var parts = new List<string>();
            bool isAbstract = (attributes & System.Reflection.TypeAttributes.Abstract) != 0;
            bool isSealed = (attributes & System.Reflection.TypeAttributes.Sealed) != 0;
            bool isInterface = (attributes & System.Reflection.TypeAttributes.Interface) != 0;

            if (isAbstract && isSealed && !isInterface)
                parts.Add("static");          // static classes are abstract + sealed in IL
            else if (isSealed && !isInterface)
                parts.Add("sealed");
            else if (isAbstract && !isInterface)
                parts.Add("abstract");

            return string.Join(" ", parts);
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

        /// <summary>
        /// Get the base type and implemented interfaces display string for a type definition,
        /// omitting trivial bases (System.Object, System.ValueType, System.Enum) since those
        /// are implied by the type kind. Returns e.g. "MyApp.BaseController, System.IDisposable".
        /// 型定義の基底型および実装インターフェースの表示文字列を取得。自明な基底型は省略。
        /// </summary>
        private static string GetBaseTypeDisplayName(MetadataReader reader, TypeDefinition typeDef)
        {
            var parts = new List<string>();

            // Base type (skip trivial)
            if (!typeDef.BaseType.IsNil)
            {
                string baseTypeName = GetBaseTypeName(reader, typeDef.BaseType);
                if (baseTypeName is not ("System.Object" or "System.ValueType" or "System.Enum" or ""))
                    parts.Add(baseTypeName);
            }

            // Implemented interfaces
            foreach (var ifaceHandle in typeDef.GetInterfaceImplementations())
            {
                var iface = reader.GetInterfaceImplementation(ifaceHandle);
                string ifaceName = GetInterfaceTypeName(reader, iface.Interface);
                if (!string.IsNullOrEmpty(ifaceName))
                    parts.Add(ifaceName);
            }

            return string.Join(", ", parts);
        }

        /// <summary>Get the full name of an interface from its EntityHandle.</summary>
        private static string GetInterfaceTypeName(MetadataReader reader, EntityHandle handle)
        {
            if (handle.Kind == HandleKind.TypeReference)
            {
                var typeRef = reader.GetTypeReference((TypeReferenceHandle)handle);
                string ns = reader.GetString(typeRef.Namespace);
                string name = reader.GetString(typeRef.Name);
                return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
            }
            if (handle.Kind == HandleKind.TypeDefinition)
            {
                var typeDef = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
                return GetFullTypeName(reader, typeDef);
            }
            return "";
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
                    PrimitiveTypeCode.Void => "System.Void",
                    PrimitiveTypeCode.Boolean => "System.Boolean",
                    PrimitiveTypeCode.Char => "System.Char",
                    PrimitiveTypeCode.SByte => "System.SByte",
                    PrimitiveTypeCode.Byte => "System.Byte",
                    PrimitiveTypeCode.Int16 => "System.Int16",
                    PrimitiveTypeCode.UInt16 => "System.UInt16",
                    PrimitiveTypeCode.Int32 => "System.Int32",
                    PrimitiveTypeCode.UInt32 => "System.UInt32",
                    PrimitiveTypeCode.Int64 => "System.Int64",
                    PrimitiveTypeCode.UInt64 => "System.UInt64",
                    PrimitiveTypeCode.Single => "System.Single",
                    PrimitiveTypeCode.Double => "System.Double",
                    PrimitiveTypeCode.String => "System.String",
                    PrimitiveTypeCode.Object => "System.Object",
                    PrimitiveTypeCode.IntPtr => "System.IntPtr",
                    PrimitiveTypeCode.UIntPtr => "System.UIntPtr",
                    PrimitiveTypeCode.TypedReference => "System.TypedReference",
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
