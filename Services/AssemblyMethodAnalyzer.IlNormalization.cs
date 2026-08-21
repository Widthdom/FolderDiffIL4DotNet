using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;

namespace FolderDiffIL4DotNet.Services
{
    /// <summary>
    /// Decodes method IL while replacing metadata-token operands with symbolic identities.
    /// Metadata token operand を symbolic identity へ置換しながら method IL をデコードします。
    /// </summary>
    internal static partial class AssemblyMethodAnalyzer
    {
        private static readonly IReadOnlyDictionary<ushort, OpCode> IlOpCodes = BuildIlOpCodeMap();
        private const int MaxTokenIdentityUtf8Bytes = 1024 * 1024;
        private const int MaxMethodTokenIdentityUtf8Bytes = 8 * 1024 * 1024;

        private static string ReadNormalizedIlBody(
            MetadataReader metadataReader,
            PEReader peReader,
            MethodDefinition methodDefinition,
            CanonicalSignatureTypeProvider typeProvider,
            GenericContext genericContext)
        {
            if (methodDefinition.RelativeVirtualAddress == 0) return "";

            try
            {
                var body = peReader.GetMethodBody(methodDefinition.RelativeVirtualAddress);
                byte[] rawIl = body.GetILBytes() ?? [];

                try
                {
                    var ilReader = body.GetILReader();
                    using var normalized = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var tokenIdentityDigests = new Dictionary<int, byte[]>();
                    int totalTokenIdentityBytes = 0;

                    while (ilReader.RemainingBytes > 0)
                    {
                        ushort encodedOpCode = ReadOpCode(ref ilReader);
                        if (!IlOpCodes.TryGetValue(encodedOpCode, out var opCode))
                            throw new BadImageFormatException($"Unknown IL opcode 0x{encodedOpCode:X4}.");

                        AppendUInt16(normalized, encodedOpCode);
                        AppendNormalizedOperand(
                            normalized,
                            ref ilReader,
                            opCode.OperandType,
                            metadataReader,
                            typeProvider,
                            genericContext,
                            tokenIdentityDigests,
                            ref totalTokenIdentityBytes);
                    }

                    return "normalized-sha256:" + Convert.ToHexString(normalized.GetHashAndReset());
                }
                catch (Exception ex) when (IsMetadataDecodeRecoverable(ex))
                {
                    // Preserve the previous raw-byte comparison for malformed or unsupported IL.
                    // 不正または未対応の IL では従来の生バイト比較を維持する。
                    return "raw:" + Convert.ToHexString(rawIl);
                }
            }
            catch (Exception ex) when (IsMetadataDecodeRecoverable(ex))
            {
                return "";
            }
        }

        private static ushort ReadOpCode(ref BlobReader reader)
        {
            byte first = reader.ReadByte();
            return first == 0xFE
                ? (ushort)(0xFE00 | reader.ReadByte())
                : first;
        }

        private static void AppendNormalizedOperand(
            IncrementalHash destination,
            ref BlobReader ilReader,
            OperandType operandType,
            MetadataReader metadataReader,
            CanonicalSignatureTypeProvider typeProvider,
            GenericContext genericContext,
            Dictionary<int, byte[]> tokenIdentityDigests,
            ref int totalTokenIdentityBytes)
        {
            if (IsMetadataTokenOperand(operandType))
            {
                int token = ilReader.ReadInt32();
                ValidateMetadataTokenOperand(metadataReader, operandType, MetadataTokens.Handle(token));
                if (!tokenIdentityDigests.TryGetValue(token, out byte[]? identityDigest))
                {
                    string identity = ResolveMetadataToken(
                        metadataReader,
                        token,
                        operandType,
                        typeProvider,
                        genericContext);
                    int identityByteCount = Encoding.UTF8.GetByteCount(identity);
                    if (identityByteCount > MaxTokenIdentityUtf8Bytes)
                        throw new BadImageFormatException("A normalized metadata-token identity exceeds the size limit.");
                    if (identityByteCount > MaxMethodTokenIdentityUtf8Bytes - totalTokenIdentityBytes)
                        throw new BadImageFormatException("Normalized metadata-token identities exceed the per-method size limit.");

                    totalTokenIdentityBytes += identityByteCount;
                    identityDigest = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
                    tokenIdentityDigests.Add(token, identityDigest);
                }

                destination.AppendData(identityDigest);
                return;
            }

            if (operandType == OperandType.InlineSwitch)
            {
                int targetCount = ilReader.ReadInt32();
                if (targetCount < 0 || targetCount > ilReader.RemainingBytes / sizeof(int))
                    throw new BadImageFormatException("Invalid IL switch target count.");

                AppendInt32(destination, targetCount);
                AppendRawBytes(destination, ref ilReader, targetCount * sizeof(int));
                return;
            }

            AppendRawBytes(destination, ref ilReader, GetRawOperandSize(operandType));
        }

        private static bool IsMetadataTokenOperand(OperandType operandType)
            => operandType is OperandType.InlineField
                or OperandType.InlineMethod
                or OperandType.InlineSig
                or OperandType.InlineString
                or OperandType.InlineTok
                or OperandType.InlineType;

        private static int GetRawOperandSize(OperandType operandType)
            => operandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineBrTarget or OperandType.InlineI or OperandType.ShortInlineR => 4,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                _ => throw new BadImageFormatException($"Unsupported IL operand type {operandType}.")
            };

        private static void AppendRawBytes(IncrementalHash destination, ref BlobReader reader, int byteCount)
        {
            if (byteCount > reader.RemainingBytes)
                throw new BadImageFormatException("IL operand extends beyond the method body.");

            if (byteCount > 0)
                destination.AppendData(reader.ReadBytes(byteCount));
        }

        private static void AppendUInt16(IncrementalHash destination, ushort value)
        {
            Span<byte> bytes = stackalloc byte[sizeof(ushort)];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
            destination.AppendData(bytes);
        }

        private static void AppendInt32(IncrementalHash destination, int value)
        {
            Span<byte> bytes = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
            destination.AppendData(bytes);
        }

        private static string ResolveMetadataToken(
            MetadataReader reader,
            int token,
            OperandType operandType,
            CanonicalSignatureTypeProvider typeProvider,
            GenericContext genericContext)
        {
            Handle handle = MetadataTokens.Handle(token);
            ValidateMetadataTokenOperand(reader, operandType, handle);
            return handle.Kind switch
            {
                HandleKind.UserString => $"string:{reader.GetUserString((UserStringHandle)handle)}",
                HandleKind.TypeDefinition => ResolveTypeDefinition(reader, (TypeDefinitionHandle)handle, typeProvider),
                HandleKind.TypeReference => ResolveTypeReference(reader, (TypeReferenceHandle)handle, typeProvider),
                HandleKind.TypeSpecification => $"type:{typeProvider.GetTypeFromSpecification(reader, genericContext, (TypeSpecificationHandle)handle, 0)}",
                HandleKind.MethodDefinition => ResolveMethodDefinition(reader, (MethodDefinitionHandle)handle, typeProvider),
                HandleKind.FieldDefinition => ResolveFieldDefinition(reader, (FieldDefinitionHandle)handle, typeProvider),
                HandleKind.MemberReference => ResolveMemberReference(reader, (MemberReferenceHandle)handle, typeProvider, genericContext),
                HandleKind.MethodSpecification => ResolveMethodSpecification(reader, (MethodSpecificationHandle)handle, typeProvider, genericContext),
                HandleKind.StandaloneSignature => ResolveStandaloneSignature(reader, (StandaloneSignatureHandle)handle, typeProvider, genericContext),
                _ => throw new BadImageFormatException($"Unsupported metadata token kind {handle.Kind}.")
            };
        }

        private static void ValidateMetadataTokenOperand(
            MetadataReader reader,
            OperandType operandType,
            Handle handle)
        {
            bool valid = operandType switch
            {
                OperandType.InlineString => handle.Kind == HandleKind.UserString,
                OperandType.InlineType => handle.Kind is HandleKind.TypeDefinition
                    or HandleKind.TypeReference
                    or HandleKind.TypeSpecification,
                OperandType.InlineField => handle.Kind == HandleKind.FieldDefinition
                    || handle.Kind == HandleKind.MemberReference
                        && reader.GetMemberReference((MemberReferenceHandle)handle).GetKind() == MemberReferenceKind.Field,
                OperandType.InlineMethod => handle.Kind is HandleKind.MethodDefinition or HandleKind.MethodSpecification
                    || handle.Kind == HandleKind.MemberReference
                        && reader.GetMemberReference((MemberReferenceHandle)handle).GetKind() == MemberReferenceKind.Method,
                OperandType.InlineTok => handle.Kind is HandleKind.TypeDefinition
                    or HandleKind.TypeReference
                    or HandleKind.TypeSpecification
                    or HandleKind.MethodDefinition
                    or HandleKind.MethodSpecification
                    or HandleKind.FieldDefinition
                    || handle.Kind == HandleKind.MemberReference,
                OperandType.InlineSig => handle.Kind == HandleKind.StandaloneSignature
                    && reader.GetStandaloneSignature((StandaloneSignatureHandle)handle).GetKind()
                        == StandaloneSignatureKind.Method,
                _ => false,
            };

            if (!valid)
                throw new BadImageFormatException(
                    $"Metadata token kind {handle.Kind} is invalid for IL operand type {operandType}.");
        }

        private static string ResolveTypeDefinition(
            MetadataReader reader,
            TypeDefinitionHandle handle,
            CanonicalSignatureTypeProvider typeProvider)
            => $"type:{typeProvider.GetTypeFromDefinition(reader, handle, 0)}";

        private static string ResolveTypeReference(
            MetadataReader reader,
            TypeReferenceHandle handle,
            CanonicalSignatureTypeProvider typeProvider)
        {
            string typeName = typeProvider.GetTypeFromReference(reader, handle, 0);
            return $"type:{typeName}";
        }

        private static string ResolveMethodDefinition(
            MetadataReader reader,
            MethodDefinitionHandle handle,
            CanonicalSignatureTypeProvider typeProvider)
        {
            var method = reader.GetMethodDefinition(handle);
            var declaringType = reader.GetTypeDefinition(method.GetDeclaringType());
            var context = GenericContext.FromMethod(reader, declaringType, method);
            string declaringTypeName = ResolveTypeDefinition(reader, method.GetDeclaringType(), typeProvider);
            string methodName = reader.GetString(method.Name);
            return $"method:{declaringTypeName}::{methodName}{FormatMethodSignature(method.DecodeSignature(typeProvider, context))}";
        }

        private static string ResolveFieldDefinition(
            MetadataReader reader,
            FieldDefinitionHandle handle,
            CanonicalSignatureTypeProvider typeProvider)
        {
            var field = reader.GetFieldDefinition(handle);
            var declaringType = reader.GetTypeDefinition(field.GetDeclaringType());
            var context = GenericContext.FromType(reader, declaringType);
            string declaringTypeName = ResolveTypeDefinition(reader, field.GetDeclaringType(), typeProvider);
            string fieldName = reader.GetString(field.Name);
            string fieldType = field.DecodeSignature(typeProvider, context);
            return $"field:{declaringTypeName}::{fieldName}:{fieldType}";
        }

        private static string ResolveMemberReference(
            MetadataReader reader,
            MemberReferenceHandle handle,
            CanonicalSignatureTypeProvider typeProvider,
            GenericContext genericContext)
        {
            var member = reader.GetMemberReference(handle);
            string parent = ResolveEntityIdentity(reader, member.Parent, typeProvider, genericContext);
            string name = reader.GetString(member.Name);
            return member.GetKind() switch
            {
                MemberReferenceKind.Method => $"method:{parent}::{name}{FormatMethodSignature(member.DecodeMethodSignature(typeProvider, genericContext))}",
                MemberReferenceKind.Field => $"field:{parent}::{name}:{member.DecodeFieldSignature(typeProvider, genericContext)}",
                _ => throw new BadImageFormatException($"Unsupported member reference kind {member.GetKind()}.")
            };
        }

        private static string ResolveMethodSpecification(
            MetadataReader reader,
            MethodSpecificationHandle handle,
            CanonicalSignatureTypeProvider typeProvider,
            GenericContext genericContext)
        {
            var specification = reader.GetMethodSpecification(handle);
            string method = ResolveEntityIdentity(reader, specification.Method, typeProvider, genericContext);
            string arguments = string.Join(",", specification.DecodeSignature(typeProvider, genericContext));
            return $"method-spec:{method}<{arguments}>";
        }

        private static string ResolveStandaloneSignature(
            MetadataReader reader,
            StandaloneSignatureHandle handle,
            CanonicalSignatureTypeProvider typeProvider,
            GenericContext genericContext)
        {
            var signature = reader.GetStandaloneSignature(handle);
            return signature.GetKind() switch
            {
                StandaloneSignatureKind.Method => "signature:" + FormatMethodSignature(signature.DecodeMethodSignature(typeProvider, genericContext)),
                StandaloneSignatureKind.LocalVariables => "locals:" + string.Join(",", signature.DecodeLocalSignature(typeProvider, genericContext)),
                _ => throw new BadImageFormatException($"Unsupported standalone signature kind {signature.GetKind()}.")
            };
        }

        private static string ResolveEntityIdentity(
            MetadataReader reader,
            EntityHandle handle,
            CanonicalSignatureTypeProvider typeProvider,
            GenericContext genericContext)
            => handle.Kind switch
            {
                HandleKind.TypeDefinition => ResolveTypeDefinition(reader, (TypeDefinitionHandle)handle, typeProvider),
                HandleKind.TypeReference => ResolveTypeReference(reader, (TypeReferenceHandle)handle, typeProvider),
                HandleKind.TypeSpecification => $"type:{typeProvider.GetTypeFromSpecification(reader, genericContext, (TypeSpecificationHandle)handle, 0)}",
                HandleKind.MethodDefinition => ResolveMethodDefinition(reader, (MethodDefinitionHandle)handle, typeProvider),
                HandleKind.MemberReference => ResolveMemberReference(reader, (MemberReferenceHandle)handle, typeProvider, genericContext),
                HandleKind.ModuleReference => $"module:{reader.GetString(reader.GetModuleReference((ModuleReferenceHandle)handle).Name)}",
                HandleKind.ModuleDefinition => "module:<current>",
                _ => throw new BadImageFormatException($"Unsupported metadata entity kind {handle.Kind}.")
            };

        private static string FormatMethodSignature(MethodSignature<string> signature)
            => $"h{signature.Header.RawValue:X2}g{signature.GenericParameterCount}r{signature.RequiredParameterCount}({string.Join(",", signature.ParameterTypes)}):{signature.ReturnType}";

        private static IReadOnlyDictionary<ushort, OpCode> BuildIlOpCodeMap()
        {
            var result = new Dictionary<ushort, OpCode>();
            foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType == typeof(OpCode) && field.GetValue(null) is OpCode opCode)
                    result[unchecked((ushort)opCode.Value)] = opCode;
            }
            return result;
        }
    }
}
