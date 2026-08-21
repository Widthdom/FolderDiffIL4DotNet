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
        internal class SimpleSignatureTypeProvider : ISignatureTypeProvider<string, GenericContext?>
        {
            internal const int MaxTypeReferenceNestingDepth = 256;
            internal const int MaxTypeSpecificationNestingDepth = 256;
            internal const int MaxTypeSpecificationDecodeNodes = 4096;

            private readonly MetadataReader _reader;
            private readonly HashSet<TypeSpecificationHandle> _activeTypeSpecifications = [];
            private int _typeSpecificationDecodeNodes;

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

            public virtual string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
            {
                var typeDef = reader.GetTypeDefinition(handle);
                return GetFullTypeName(reader, typeDef);
            }

            public virtual string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            {
                var resolved = ResolveTypeReferenceName(reader, handle);
                return resolved.TypeName;
            }

            /// <summary>
            /// Resolves a possibly nested TypeRef chain iteratively, rejecting cycles and
            /// unreasonable nesting before malformed metadata can exhaust the process stack.
            /// 入れ子になった TypeRef chain を反復的に解決し、不正 metadata による stack 枯渇を
            /// 防ぐため、循環と過大な深さを拒否します。
            /// </summary>
            protected static (string TypeName, string RootNamespace, string RootName, EntityHandle ResolutionScope) ResolveTypeReferenceName(
                MetadataReader reader,
                TypeReferenceHandle handle)
            {
                var nestedNames = new List<string>();
                var visited = new HashSet<TypeReferenceHandle>();
                TypeReferenceHandle current = handle;

                while (true)
                {
                    if (nestedNames.Count >= MaxTypeReferenceNestingDepth)
                        throw new BadImageFormatException($"Type reference nesting exceeds {MaxTypeReferenceNestingDepth} levels.");
                    if (!visited.Add(current))
                        throw new BadImageFormatException("Type reference resolution scope contains a cycle.");

                    var typeReference = reader.GetTypeReference(current);
                    nestedNames.Add(reader.GetString(typeReference.Name));
                    if (typeReference.ResolutionScope.Kind == HandleKind.TypeReference)
                    {
                        current = (TypeReferenceHandle)typeReference.ResolutionScope;
                        continue;
                    }

                    string rootName = nestedNames[^1];
                    string rootNamespace = reader.GetString(typeReference.Namespace);
                    string typeName = string.IsNullOrEmpty(rootNamespace)
                        ? rootName
                        : $"{rootNamespace}.{rootName}";
                    for (int i = nestedNames.Count - 2; i >= 0; i--)
                        typeName += $"/{nestedNames[i]}";

                    return (typeName, rootNamespace, rootName, typeReference.ResolutionScope);
                }
            }

            public string GetTypeFromSpecification(MetadataReader reader, GenericContext? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            {
                bool isRootDecode = _activeTypeSpecifications.Count == 0;
                if (isRootDecode)
                    _typeSpecificationDecodeNodes = 0;
                if (_activeTypeSpecifications.Count >= MaxTypeSpecificationNestingDepth)
                    throw new BadImageFormatException(
                        $"Type specification nesting exceeds {MaxTypeSpecificationNestingDepth} levels.");
                if (++_typeSpecificationDecodeNodes > MaxTypeSpecificationDecodeNodes)
                    throw new BadImageFormatException(
                        $"Type specification decoding exceeds {MaxTypeSpecificationDecodeNodes} nodes.");
                if (!_activeTypeSpecifications.Add(handle))
                    throw new BadImageFormatException("Type specification signature contains a cycle.");

                try
                {
                    var sigReader = reader.GetBlobReader(reader.GetTypeSpecification(handle).Signature);
                    return new SignatureDecoder<string, GenericContext?>(this, reader, genericContext).DecodeType(ref sigReader);
                }
                finally
                {
                    _activeTypeSpecifications.Remove(handle);
                    if (isRootDecode)
                        _typeSpecificationDecodeNodes = 0;
                }
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
            public virtual string GetGenericMethodParameter(GenericContext? genericContext, int index)
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
            public virtual string GetGenericTypeParameter(GenericContext? genericContext, int index)
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
        /// Produces scope-qualified type identities for semantic comparison while retaining
        /// the human-readable provider separately for report output.
        /// report 表示用の可読 provider とは分離して、semantic 比較用の scope 付き型 identity を
        /// 生成します。
        /// </summary>
        internal class ScopedSignatureTypeProvider : SimpleSignatureTypeProvider
        {
            public ScopedSignatureTypeProvider(MetadataReader reader) : base(reader) { }

            public override string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
                => $"module:<current>:{base.GetTypeFromDefinition(reader, handle, rawTypeKind)}";

            public override string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            {
                var resolved = ResolveTypeReferenceName(reader, handle);
                string scope = ResolveScopedTypeReferenceScope(
                    reader,
                    resolved.ResolutionScope,
                    resolved.RootNamespace,
                    resolved.RootName);
                return $"{scope}:{resolved.TypeName}";
            }
        }

        private static string ResolveScopedTypeReferenceScope(
            MetadataReader reader,
            EntityHandle scope,
            string rootNamespace,
            string rootName)
        {
            if (scope.IsNil)
                return ResolveForwardedTypeScope(reader, rootNamespace, rootName);

            return scope.Kind switch
            {
                HandleKind.AssemblyReference => ResolveScopedAssemblyReference(reader, (AssemblyReferenceHandle)scope),
                HandleKind.ModuleReference => $"module:{reader.GetString(reader.GetModuleReference((ModuleReferenceHandle)scope).Name)}",
                HandleKind.ModuleDefinition => "module:<current>",
                _ => throw new BadImageFormatException($"Unsupported type reference scope {scope.Kind}.")
            };
        }

        private static string ResolveForwardedTypeScope(
            MetadataReader reader,
            string rootNamespace,
            string rootName)
        {
            ExportedTypeHandle matchingHandle = default;
            foreach (var handle in reader.ExportedTypes)
            {
                var exportedType = reader.GetExportedType(handle);
                if (reader.GetString(exportedType.Namespace) != rootNamespace
                    || reader.GetString(exportedType.Name) != rootName)
                {
                    continue;
                }

                if (!matchingHandle.IsNil)
                    throw new BadImageFormatException($"Multiple exported types match {rootNamespace}.{rootName}.");
                matchingHandle = handle;
            }

            if (matchingHandle.IsNil)
                throw new BadImageFormatException($"No exported type matches {rootNamespace}.{rootName}.");

            var visited = new HashSet<ExportedTypeHandle>();
            ExportedTypeHandle current = matchingHandle;
            for (int depth = 0; depth < SimpleSignatureTypeProvider.MaxTypeReferenceNestingDepth; depth++)
            {
                if (!visited.Add(current))
                    throw new BadImageFormatException("Exported type implementation contains a cycle.");

                EntityHandle implementation = reader.GetExportedType(current).Implementation;
                if (implementation.IsNil)
                    throw new BadImageFormatException("Exported type implementation is nil.");

                switch (implementation.Kind)
                {
                    case HandleKind.AssemblyReference:
                        return ResolveScopedAssemblyReference(reader, (AssemblyReferenceHandle)implementation);
                    case HandleKind.AssemblyFile:
                        var assemblyFile = reader.GetAssemblyFile((AssemblyFileHandle)implementation);
                        string fileName = reader.GetString(assemblyFile.Name);
                        string hashValue = Convert.ToHexString(reader.GetBlobBytes(assemblyFile.HashValue));
                        return $"module-file:{fileName}:contains-metadata={assemblyFile.ContainsMetadata}:hash={hashValue}";
                    case HandleKind.ExportedType:
                        current = (ExportedTypeHandle)implementation;
                        break;
                    default:
                        throw new BadImageFormatException(
                            $"Unsupported exported type implementation {implementation.Kind}.");
                }
            }

            throw new BadImageFormatException(
                $"Exported type implementation exceeds {SimpleSignatureTypeProvider.MaxTypeReferenceNestingDepth} levels.");
        }

        private static string ResolveScopedAssemblyReference(MetadataReader reader, AssemblyReferenceHandle handle)
        {
            var reference = reader.GetAssemblyReference(handle);
            string name = reader.GetString(reference.Name);
            string culture = reader.GetString(reference.Culture);
            string publicKeyToken = Convert.ToHexString(reader.GetBlobBytes(reference.PublicKeyOrToken));
            uint flags = unchecked((uint)reference.Flags);
            return $"assembly:{name}:version={reference.Version}:culture={culture}:public-key-or-token={publicKeyToken}:flags={flags:X8}";
        }

        /// <summary>
        /// Produces comparison-only type identities that retain the assembly or module scope
        /// of every type reference, including references nested inside signatures.
        /// シグネチャ内を含むすべての型参照について assembly/module scope を保持する、
        /// 比較専用の型 identity を生成します。
        /// </summary>
        internal sealed class CanonicalSignatureTypeProvider : ScopedSignatureTypeProvider
        {
            public CanonicalSignatureTypeProvider(MetadataReader reader) : base(reader) { }

            public override string GetGenericMethodParameter(GenericContext? genericContext, int index)
                => $"!!{index}";

            public override string GetGenericTypeParameter(GenericContext? genericContext, int index)
                => $"!{index}";
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
