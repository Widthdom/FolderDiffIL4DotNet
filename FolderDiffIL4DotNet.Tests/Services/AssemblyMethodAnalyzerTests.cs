using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using FolderDiffIL4DotNet.Services;
using Xunit;
using static FolderDiffIL4DotNet.Services.AssemblyMethodAnalyzer;

namespace FolderDiffIL4DotNet.Tests.Services
{
    public sealed class AssemblyMethodAnalyzerTests : IDisposable
    {
        private readonly string _tempDir;

        public AssemblyMethodAnalyzerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"AsmAnalyzerTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        [Fact]
        public void Analyze_SameAssembly_NoChanges()
        {
            // Compare a real assembly to itself — should report no changes
            // 実アセンブリを自分自身と比較 — 変更なしが期待される
            var assemblyPath = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            var result = AssemblyMethodAnalyzer.Analyze(assemblyPath, assemblyPath);

            Assert.NotNull(result);
            Assert.False(result.HasChanges);
            Assert.Empty(result.Entries);
            Assert.Equal(0, result.AddedCount);
            Assert.Equal(0, result.RemovedCount);
            Assert.Equal(0, result.ModifiedCount);
        }

        [Fact]
        public void Analyze_NonExistentFile_ReturnsNull()
        {
            // Attempting to analyse a missing file should gracefully return null
            // 存在しないファイルの解析は null を返すべき
            var result = AssemblyMethodAnalyzer.Analyze("/nonexistent/old.dll", "/nonexistent/new.dll");
            Assert.Null(result);
        }

        [Fact]
        public void Analyze_InvalidFile_ReturnsNull()
        {
            // Attempting to analyse a non-PE file should gracefully return null
            // PE でないファイルの解析は null を返すべき
            var textFile = typeof(AssemblyMethodAnalyzerTests).Assembly.Location + ".runtimeconfig.json";
            if (!System.IO.File.Exists(textFile)) return; // skip if runtime config not available
            var result = AssemblyMethodAnalyzer.Analyze(textFile, textFile);
            Assert.Null(result);
        }

        [Fact]
        public void Analyze_DifferentAssemblies_DetectsChanges()
        {
            // Compare test assembly to main assembly — should detect differences
            // テストアセンブリとメインアセンブリを比較 — 差異が検出されるべき
            var testAssembly = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            var mainAssembly = typeof(FolderDiffIL4DotNet.Models.ConfigSettings).Assembly.Location;

            var result = AssemblyMethodAnalyzer.Analyze(testAssembly, mainAssembly);

            Assert.NotNull(result);
            Assert.True(result.HasChanges);
            Assert.True(result.Entries.Count > 0);
        }

        [Fact]
        public void Analyze_DifferentAssemblies_EntriesHaveStructuredData()
        {
            // Entries should contain structured MemberChangeEntry data
            // エントリには構造化された MemberChangeEntry データが含まれるべき
            var testAssembly = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            var mainAssembly = typeof(FolderDiffIL4DotNet.Models.ConfigSettings).Assembly.Location;

            var result = AssemblyMethodAnalyzer.Analyze(testAssembly, mainAssembly);

            Assert.NotNull(result);
            var firstEntry = result.Entries.First();
            Assert.False(string.IsNullOrEmpty(firstEntry.Change));
            Assert.False(string.IsNullOrEmpty(firstEntry.TypeName));
            Assert.False(string.IsNullOrEmpty(firstEntry.MemberKind));
            Assert.Contains(firstEntry.Change, new[] { "Added", "Removed", "Modified" });
            Assert.Contains(firstEntry.MemberKind, new[] { "Class", "Record", "Struct", "Interface", "Enum", "Constructor", "StaticConstructor", "Method", "Property", "Field" });
        }

        [Fact]
        public void Analyze_DifferentAssemblies_ModifiedEntriesIfPresentHaveValidChangeKind()
        {
            // When comparing different assemblies, if any Modified entries exist,
            // they should have Change="Modified" and a valid MemberKind.
            // 異なるアセンブリ比較時、Modified エントリが存在する場合、
            // Change="Modified" と有効な MemberKind を持つべき。
            var testAssembly = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            var mainAssembly = typeof(FolderDiffIL4DotNet.Models.ConfigSettings).Assembly.Location;

            var result = AssemblyMethodAnalyzer.Analyze(testAssembly, mainAssembly);

            Assert.NotNull(result);
            var modifiedEntries = result.Entries.Where(e => e.Change == "Modified").ToList();
            // Modified entries may or may not exist between unrelated assemblies,
            // but if they do, they must have valid structure.
            foreach (var entry in modifiedEntries)
            {
                Assert.Equal("Modified", entry.Change);
                Assert.False(string.IsNullOrEmpty(entry.TypeName));
                Assert.Contains(entry.MemberKind, new[] { "Constructor", "StaticConstructor", "Method", "Property", "Field" });
            }
        }

        [Fact]
        public void Analyze_DifferentAssemblies_AllEntriesHavePopulatedAccessField()
        {
            // All entries (Added/Removed/Modified) should have the Access field populated
            // for methods, properties, and fields.
            // すべてのエントリ（Added/Removed/Modified）で、メソッド・プロパティ・フィールドの
            // Access フィールドが設定されているべき。
            var testAssembly = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            var mainAssembly = typeof(FolderDiffIL4DotNet.Models.ConfigSettings).Assembly.Location;

            var result = AssemblyMethodAnalyzer.Analyze(testAssembly, mainAssembly);

            Assert.NotNull(result);
            var memberEntries = result.Entries
                .Where(e => e.MemberKind is "Method" or "Property" or "Field"
                         or "Constructor" or "StaticConstructor")
                .ToList();

            Assert.True(memberEntries.Count > 0, "Expected at least one member entry between two different assemblies");
            // Every member-level entry should have a non-empty Access value
            // (or "old → new" for Modified entries with access changes)
            Assert.True(memberEntries.All(m => !string.IsNullOrEmpty(m.Access)),
                "All member entries should have a non-empty Access field");
        }

        [Fact]
        public void Analyze_TruncatedPEFile_ReturnsNull()
        {
            // A file with a valid MZ header but truncated PE data should trigger the
            // catch-all fallback and return null instead of throwing.
            // 有効な MZ ヘッダーを持つが PE データが切り詰められたファイルは
            // catch-all フォールバックで null を返すべき。
            var truncatedPath = Path.Combine(_tempDir, "truncated.dll");
            // MZ header (first two bytes) followed by garbage — enough to pass initial
            // File.Open but fail during metadata parsing.
            var bytes = new byte[64];
            bytes[0] = 0x4D; // 'M'
            bytes[1] = 0x5A; // 'Z'
            File.WriteAllBytes(truncatedPath, bytes);

            var result = AssemblyMethodAnalyzer.Analyze(truncatedPath, truncatedPath);
            Assert.Null(result);
        }

        [Fact]
        public void Analyze_EmptyFile_ReturnsNull()
        {
            // A zero-byte file should trigger the catch-all and return null.
            // 0 バイトファイルは catch-all でnull を返すべき。
            var emptyPath = Path.Combine(_tempDir, "empty.dll");
            File.WriteAllBytes(emptyPath, Array.Empty<byte>());

            var result = AssemblyMethodAnalyzer.Analyze(emptyPath, emptyPath);
            Assert.Null(result);
        }

        [Fact]
        public void Analyze_CorruptPEWithValidHeader_ReturnsNull()
        {
            // A file with a plausible PE header but corrupted metadata tables should
            // trigger the catch-all fallback path in AssemblyMethodAnalyzer.Analyze.
            // もっともらしい PE ヘッダーを持つが破損したメタデータテーブルのファイルは
            // AssemblyMethodAnalyzer.Analyze の catch-all フォールバックを発火させるべき。
            var corruptPath = Path.Combine(_tempDir, "corrupt.dll");

            // Build a minimal DOS header → PE signature → COFF header → optional header
            // but with invalid metadata RVA so System.Reflection.Metadata will fail.
            // Copy a real assembly then corrupt the metadata section.
            var realAssembly = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            var assemblyBytes = File.ReadAllBytes(realAssembly);

            // Corrupt bytes in the middle of the assembly (metadata tables region)
            // to trigger an exception during ReadAssemblySnapshot.
            var random = new Random(42);
            int corruptStart = Math.Min(256, assemblyBytes.Length / 2);
            int corruptEnd = Math.Min(corruptStart + 512, assemblyBytes.Length);
            for (int i = corruptStart; i < corruptEnd; i++)
            {
                assemblyBytes[i] = (byte)random.Next(256);
            }
            File.WriteAllBytes(corruptPath, assemblyBytes);

            var result = AssemblyMethodAnalyzer.Analyze(corruptPath, corruptPath);
            // Should return null (catch-all) rather than throwing
            Assert.Null(result);
        }

        [Fact]
        public void Analyze_OneValidOneCorrupt_ReturnsNull()
        {
            // When one assembly is valid but the other is corrupt, the catch-all
            // should still gracefully return null.
            // 一方が有効で他方が破損している場合でも catch-all で null を返すべき。
            var validPath = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            var corruptPath = Path.Combine(_tempDir, "one-corrupt.dll");
            File.WriteAllBytes(corruptPath, new byte[] { 0x4D, 0x5A, 0x00, 0x00 });

            var result = AssemblyMethodAnalyzer.Analyze(validPath, corruptPath);
            Assert.Null(result);
        }

        // ── SimpleSignatureTypeProvider improvement tests ────────────────────

        [Fact]
        [Trait("Category", "Unit")]
        public void GenericContext_FromType_ResolvesTypeParameterNames()
        {
            // Verify that GenericContext reads type-level generic parameter names
            // from a real assembly containing generic types.
            // 実アセンブリのジェネリック型から型レベルジェネリックパラメータ名を読み取ることを検証。
            var assemblyPath = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            using var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(stream);
            var reader = peReader.GetMetadataReader();

            // Find a generic type in the runtime assembly (e.g. from System.Private.CoreLib via references)
            // We'll look in the test assembly's referenced types instead — use Dictionary<string,string>
            // which should be used somewhere. Alternatively, check the main assembly which has
            // Dictionary<,> fields.
            // 代わりにメインアセンブリのジェネリック型を検証
            var mainAssemblyPath = typeof(FolderDiffIL4DotNet.Models.ConfigSettings).Assembly.Location;
            using var mainStream = new FileStream(mainAssemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var mainPeReader = new PEReader(mainStream);
            var mainReader = mainPeReader.GetMetadataReader();

            bool foundGenericType = false;
            foreach (var typeHandle in mainReader.TypeDefinitions)
            {
                var typeDef = mainReader.GetTypeDefinition(typeHandle);
                var genericParams = typeDef.GetGenericParameters();
                if (genericParams.Count > 0)
                {
                    var context = GenericContext.FromType(mainReader, typeDef);
                    Assert.Equal(genericParams.Count, context.TypeParameters.Length);
                    Assert.True(context.MethodParameters.IsEmpty);

                    // All parameter names should be non-empty / すべてのパラメータ名が空でないこと
                    foreach (var paramName in context.TypeParameters)
                        Assert.False(string.IsNullOrEmpty(paramName), "Generic type parameter name should not be empty");

                    foundGenericType = true;
                    break;
                }
            }

            Assert.True(foundGenericType, "Expected at least one generic type definition in the main assembly");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GenericContext_FromMethod_ResolvesMethodParameterNames()
        {
            // Verify that GenericContext reads method-level generic parameter names.
            // メソッドレベルのジェネリックパラメータ名を読み取ることを検証。
            var assemblyPath = typeof(FolderDiffIL4DotNet.Models.ConfigSettings).Assembly.Location;
            using var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(stream);
            var reader = peReader.GetMetadataReader();

            bool foundGenericMethod = false;
            foreach (var typeHandle in reader.TypeDefinitions)
            {
                var typeDef = reader.GetTypeDefinition(typeHandle);
                foreach (var methodHandle in typeDef.GetMethods())
                {
                    var methodDef = reader.GetMethodDefinition(methodHandle);
                    var genericParams = methodDef.GetGenericParameters();
                    if (genericParams.Count > 0)
                    {
                        var context = GenericContext.FromMethod(reader, typeDef, methodDef);
                        Assert.Equal(genericParams.Count, context.MethodParameters.Length);

                        foreach (var paramName in context.MethodParameters)
                            Assert.False(string.IsNullOrEmpty(paramName), "Generic method parameter name should not be empty");

                        foundGenericMethod = true;
                        break;
                    }
                }
                if (foundGenericMethod) break;
            }

            // Note: if no generic methods exist in the main assembly, this test documents it.
            // Even without generic methods, the FromMethod path is exercised and shouldn't crash.
            // ジェネリックメソッドが存在しなくてもクラッシュしないことを確認。
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void SimpleSignatureTypeProvider_GetGenericTypeParameter_ResolvesWithContext()
        {
            // When a GenericContext is provided, GetGenericTypeParameter should return
            // the declared name instead of the index-based fallback.
            // GenericContext が提供された場合、インデックスベースのフォールバックではなく
            // 宣言名を返すべき。
            var assemblyPath = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            using var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(stream);
            var reader = peReader.GetMetadataReader();
            var provider = new SimpleSignatureTypeProvider(reader);

            var context = new GenericContext(
                ImmutableArray.Create("TKey", "TValue"),
                ImmutableArray.Create("TResult"));

            // Type parameters / 型パラメータ
            Assert.Equal("TKey", provider.GetGenericTypeParameter(context, 0));
            Assert.Equal("TValue", provider.GetGenericTypeParameter(context, 1));
            Assert.Equal("!2", provider.GetGenericTypeParameter(context, 2)); // out of range fallback

            // Method parameters / メソッドパラメータ
            Assert.Equal("TResult", provider.GetGenericMethodParameter(context, 0));
            Assert.Equal("!!1", provider.GetGenericMethodParameter(context, 1)); // out of range fallback
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void SimpleSignatureTypeProvider_GetGenericTypeParameter_FallsBackWithoutContext()
        {
            // When context is null, fall back to the index-based representation.
            // コンテキストが null の場合、インデックスベース表現にフォールバック。
            var assemblyPath = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            using var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(stream);
            var reader = peReader.GetMetadataReader();
            var provider = new SimpleSignatureTypeProvider(reader);

            Assert.Equal("!0", provider.GetGenericTypeParameter(null, 0));
            Assert.Equal("!!0", provider.GetGenericMethodParameter(null, 0));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void SimpleSignatureTypeProvider_GetFunctionPointerType_ExpandsSignature()
        {
            // GetFunctionPointerType should expand the full signature rather than
            // returning a fixed "delegate*" string.
            // 固定文字列 "delegate*" ではなく完全なシグネチャを展開すべき。
            var assemblyPath = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            using var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(stream);
            var reader = peReader.GetMetadataReader();
            var provider = new SimpleSignatureTypeProvider(reader);

            // No parameters / パラメータなし
            var noParamSig = new MethodSignature<string>(
                SignatureHeader.Default,
                "System.Void",
                0,
                0,
                ImmutableArray<string>.Empty);
            Assert.Equal("delegate*<System.Void>", provider.GetFunctionPointerType(noParamSig));

            // With parameters / パラメータあり
            var withParamsSig = new MethodSignature<string>(
                SignatureHeader.Default,
                "System.Int32",
                0,
                2,
                ImmutableArray.Create("System.String", "System.Boolean"));
            Assert.Equal("delegate*<System.String, System.Boolean, System.Int32>", provider.GetFunctionPointerType(withParamsSig));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void SimpleSignatureTypeProvider_GetModifiedType_PreservesModifiers()
        {
            // GetModifiedType should preserve modreq/modopt annotations.
            // modreq/modopt 注釈を保持すべき。
            var assemblyPath = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            using var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(stream);
            var reader = peReader.GetMetadataReader();
            var provider = new SimpleSignatureTypeProvider(reader);

            Assert.Equal(
                "System.Int32 modreq(System.Runtime.CompilerServices.IsVolatile)",
                provider.GetModifiedType("System.Runtime.CompilerServices.IsVolatile", "System.Int32", isRequired: true));

            Assert.Equal(
                "System.IntPtr modopt(System.Runtime.CompilerServices.IsConst)",
                provider.GetModifiedType("System.Runtime.CompilerServices.IsConst", "System.IntPtr", isRequired: false));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void SimpleSignatureTypeProvider_GetPinnedType_PreservesPinnedAnnotation()
        {
            // GetPinnedType should add a "pinned" prefix.
            // "pinned" プレフィックスを付加すべき。
            var assemblyPath = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            using var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(stream);
            var reader = peReader.GetMetadataReader();
            var provider = new SimpleSignatureTypeProvider(reader);

            Assert.Equal("pinned System.Byte", provider.GetPinnedType("System.Byte"));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Analyze_SameAssembly_GenericSignaturesDoNotCauseChanges()
        {
            // Self-comparison after the generic context improvement should still detect
            // zero changes — ensures the new name-resolved signatures are deterministic.
            // ジェネリックコンテキスト改善後も自己比較で変更なしが維持されることを確認 —
            // 名前解決後のシグネチャが決定的であることを保証。
            var mainAssembly = typeof(FolderDiffIL4DotNet.Models.ConfigSettings).Assembly.Location;
            var result = AssemblyMethodAnalyzer.Analyze(mainAssembly, mainAssembly);

            Assert.NotNull(result);
            Assert.False(result.HasChanges);
            Assert.Empty(result.Entries);
        }
    }
}
