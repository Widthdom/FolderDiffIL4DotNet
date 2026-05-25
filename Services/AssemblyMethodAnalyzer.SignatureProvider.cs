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
    /// Generic context and signature type provider for metadata decoding.
    /// メタデータデコード用のジェネリックコンテキストとシグネチャ型プロバイダー。
    /// </summary>
    internal static partial class AssemblyMethodAnalyzer
    {
        // ── Generic context for signature decoding ─────────────────────────

        /// <summary>
        /// Holds generic parameter names for both the enclosing type and the method,
        /// enabling the signature decoder to resolve <c>!0</c> → <c>T</c> and <c>!!0</c> → <c>TResult</c>.
        /// 囲み型およびメソッドのジェネリックパラメータ名を保持し、シグネチャデコーダが
        /// <c>!0</c> → <c>T</c>、<c>!!0</c> → <c>TResult</c> のように解決できるようにします。
        /// </summary>
        internal sealed class GenericContext
        {
            public ImmutableArray<string> TypeParameters { get; }
            public ImmutableArray<string> MethodParameters { get; }

            public GenericContext(ImmutableArray<string> typeParameters, ImmutableArray<string> methodParameters)
            {
                TypeParameters = typeParameters;
                MethodParameters = methodParameters;
            }

            /// <summary>
            /// Build a context with only type-level generic parameters.
            /// 型レベルのジェネリックパラメータのみを持つコンテキストを構築します。
            /// </summary>
            public static GenericContext FromType(MetadataReader reader, TypeDefinition typeDef)
            {
                var typeParams = ReadGenericParamNames(reader, typeDef.GetGenericParameters());
                return new GenericContext(typeParams, ImmutableArray<string>.Empty);
            }

            /// <summary>
            /// Build a context with both type-level and method-level generic parameters.
            /// 型レベルとメソッドレベル両方のジェネリックパラメータを持つコンテキストを構築します。
            /// </summary>
            public static GenericContext FromMethod(MetadataReader reader, TypeDefinition typeDef, MethodDefinition methodDef)
            {
                var typeParams = ReadGenericParamNames(reader, typeDef.GetGenericParameters());
                var methodParams = ReadGenericParamNames(reader, methodDef.GetGenericParameters());
                return new GenericContext(typeParams, methodParams);
            }

            private static ImmutableArray<string> ReadGenericParamNames(MetadataReader reader, GenericParameterHandleCollection handles)
            {
                if (handles.Count == 0) return ImmutableArray<string>.Empty;

                var builder = ImmutableArray.CreateBuilder<string>(handles.Count);
                foreach (var handle in handles)
                {
                    var param = reader.GetGenericParameter(handle);
                    builder.Add(reader.GetString(param.Name));
                }
                return builder.MoveToImmutable();
            }
        }

        // ── Signature type provider ──────────────────────────────────────────

        /// <summary>
        /// <see cref="ISignatureTypeProvider{TType, TGenericContext}"/> that decodes
        /// method parameter and return types into human-readable strings.
        /// Resolves generic parameter indices to their declared names via <see cref="GenericContext"/>,
        /// preserves function pointer signatures, and retains custom modifier annotations.
        /// メソッドパラメータおよび戻り値の型を可読文字列にデコードする実装。
        /// <see cref="GenericContext"/> 経由でジェネリックパラメータインデックスを宣言名に解決し、
        /// 関数ポインタシグネチャを保持し、カスタム修飾子注釈を維持します。
        /// </summary>
        internal sealed class SimpleSignatureTypeProvider : ISignatureTypeProvider<string, GenericContext?>
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
                string name = reader.GetString(typeRef.Name);

                // Follow ResolutionScope for nested type references (e.g. Outer/Inner)
                // ネストされた型参照の ResolutionScope をたどる（例: Outer/Inner）
                if (typeRef.ResolutionScope.Kind == HandleKind.TypeReference)
                {
                    string parentName = GetTypeFromReference(reader, (TypeReferenceHandle)typeRef.ResolutionScope, rawTypeKind);
                    return $"{parentName}/{name}";
                }

                string ns = reader.GetString(typeRef.Namespace);
                return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
            }

            public string GetTypeFromSpecification(MetadataReader reader, GenericContext? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            {
                var sigReader = reader.GetBlobReader(reader.GetTypeSpecification(handle).Signature);
                return new SignatureDecoder<string, GenericContext?>(this, reader, genericContext).DecodeType(ref sigReader);
            }

            public string GetSZArrayType(string elementType) => $"{elementType}[]";
            public string GetPointerType(string elementType) => $"{elementType}*";
            public string GetByReferenceType(string elementType) => $"{elementType}&";
            /// <summary>
            /// Build a generic instantiation string, stripping the arity suffix (e.g. <c>Dictionary`2</c> → <c>Dictionary</c>)
            /// because the type arguments make the arity explicit.
            /// ジェネリックインスタンス文字列を構築し、アリティ接尾辞を除去します（例: <c>Dictionary`2</c> → <c>Dictionary</c>）。
            /// 型引数によりアリティは明示されるため不要です。
            /// </summary>
            public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
            {
                string baseName = StripGenericArity(genericType);
                return $"{baseName}<{string.Join(", ", typeArguments)}>";
            }

            /// <summary>
            /// Resolve a generic method parameter index to its declared name (e.g. <c>!!0</c> → <c>TResult</c>).
            /// Falls back to <c>!!index</c> when context is unavailable.
            /// ジェネリックメソッドパラメータインデックスを宣言名に解決します（例: <c>!!0</c> → <c>TResult</c>）。
            /// コンテキストが無い場合は <c>!!index</c> にフォールバックします。
            /// </summary>
            public string GetGenericMethodParameter(GenericContext? genericContext, int index)
            {
                if (genericContext != null && index >= 0 && index < genericContext.MethodParameters.Length)
                    return genericContext.MethodParameters[index];
                return $"!!{index}";
            }

            /// <summary>
            /// Resolve a generic type parameter index to its declared name (e.g. <c>!0</c> → <c>T</c>).
            /// Falls back to <c>!index</c> when context is unavailable.
            /// ジェネリック型パラメータインデックスを宣言名に解決します（例: <c>!0</c> → <c>T</c>）。
            /// コンテキストが無い場合は <c>!index</c> にフォールバックします。
            /// </summary>
            public string GetGenericTypeParameter(GenericContext? genericContext, int index)
            {
                if (genericContext != null && index >= 0 && index < genericContext.TypeParameters.Length)
                    return genericContext.TypeParameters[index];
                return $"!{index}";
            }

            public string GetPinnedType(string elementType) => $"pinned {elementType}";

            /// <summary>
            /// Preserve custom modifier annotations (modreq / modopt) so that changes to
            /// volatile / IsConst / other modifiers are detected during comparison.
            /// カスタム修飾子注釈（modreq / modopt）を保持し、volatile / IsConst 等の
            /// 修飾子変更を比較時に検出できるようにします。
            /// </summary>
            public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired)
                => isRequired ? $"{unmodifiedType} modreq({modifier})" : $"{unmodifiedType} modopt({modifier})";

            public string GetArrayType(string elementType, ArrayShape shape)
                => $"{elementType}[{new string(',', shape.Rank - 1)}]";

            /// <summary>
            /// Expand function pointer signatures instead of returning a fixed <c>"delegate*"</c> string,
            /// so that changes to the pointed-to signature are detected.
            /// 関数ポインタシグネチャを固定文字列 <c>"delegate*"</c> ではなく展開し、
            /// ポイント先のシグネチャの変更を検出できるようにします。
            /// </summary>
            public string GetFunctionPointerType(MethodSignature<string> signature)
            {
                if (signature.ParameterTypes.Length == 0)
                    return $"delegate*<{signature.ReturnType}>";
                return $"delegate*<{string.Join(", ", signature.ParameterTypes)}, {signature.ReturnType}>";
            }
        }

        /// <summary>
        /// Strip the generic arity suffix from a metadata type name (e.g. <c>Dictionary`2</c> → <c>Dictionary</c>).
        /// Returns the original name unchanged if no backtick is present.
        /// メタデータ型名からジェネリックアリティ接尾辞を除去します（例: <c>Dictionary`2</c> → <c>Dictionary</c>）。
        /// バッククォートがない場合は元の名前をそのまま返します。
        /// </summary>
        private static string StripGenericArity(string name)
        {
            int backtick = name.LastIndexOf('`');
            return backtick >= 0 ? name[..backtick] : name;
        }
    }
}
