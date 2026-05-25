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
    /// Access modifier, type kind, and IL byte reading helpers.
    /// アクセス修飾子、型種別、IL バイト読み取りヘルパー。
    /// </summary>
    internal static partial class AssemblyMethodAnalyzer
    {
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

        /// <summary>
        /// Get the full name of an interface from its EntityHandle, including generic
        /// base types via TypeSpecification decoding (e.g. <c>IComparable&lt;int&gt;</c>).
        /// EntityHandle からインターフェース名を取得。TypeSpecification のデコードにより
        /// ジェネリック基底型（例: <c>IComparable&lt;int&gt;</c>）にも対応。
        /// </summary>
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
            if (handle.Kind == HandleKind.TypeSpecification)
            {
                return DecodeTypeSpecification(reader, (TypeSpecificationHandle)handle);
            }
            return "";
        }

        /// <summary>
        /// Get the full name of a base type from its EntityHandle, including constructed
        /// generic types via TypeSpecification decoding (e.g. <c>List&lt;int&gt;</c>).
        /// EntityHandle から基底型名を取得。TypeSpecification のデコードにより
        /// 構築済みジェネリック型（例: <c>List&lt;int&gt;</c>）にも対応。
        /// </summary>
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
            if (baseTypeHandle.Kind == HandleKind.TypeSpecification)
            {
                return DecodeTypeSpecification(reader, (TypeSpecificationHandle)baseTypeHandle);
            }
            return "";
        }

        /// <summary>
        /// Decode a TypeSpecification handle into its human-readable string representation
        /// using the signature type provider. Used for generic base types and interfaces.
        /// TypeSpecification ハンドルをシグネチャ型プロバイダーで可読文字列にデコード。
        /// ジェネリック基底型やインターフェースの表示に使用。
        /// </summary>
        private static string DecodeTypeSpecification(MetadataReader reader, TypeSpecificationHandle handle)
        {
            try
            {
                var spec = reader.GetTypeSpecification(handle);
                var typeProvider = new SimpleSignatureTypeProvider(reader);
                var sigReader = reader.GetBlobReader(spec.Signature);
                return new SignatureDecoder<string, GenericContext?>(typeProvider, reader, genericContext: null).DecodeType(ref sigReader);
            }
            catch (Exception ex) when (IsMetadataDecodeRecoverable(ex))
            {
                return "";
            }
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
            catch (Exception ex) when (IsMetadataDecodeRecoverable(ex))
            {
                return [];
            }
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
            catch (Exception ex) when (IsMetadataDecodeRecoverable(ex))
            {
                return "";
            }
        }
    }
}
