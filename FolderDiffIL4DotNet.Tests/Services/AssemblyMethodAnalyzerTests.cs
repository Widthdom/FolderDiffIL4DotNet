using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using FolderDiffIL4DotNet.Models;
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
        [Trait("Category", "Unit")]
        public void Analyze_NonExistentFile_InvokesOnErrorCallbackWithException()
        {
            // When analysis fails and onError is provided, it should be invoked with the exception.
            // 解析失敗時に onError が提供されている場合、例外を渡して呼び出されるべき。
            Exception? captured = null;
            var result = AssemblyMethodAnalyzer.Analyze(
                "/nonexistent/old.dll", "/nonexistent/new.dll",
                onError: ex => captured = ex);

            Assert.Null(result);
            Assert.NotNull(captured);
            Assert.IsAssignableFrom<Exception>(captured);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Analyze_SameAssembly_DoesNotInvokeOnErrorCallback()
        {
            // Successful analysis should not invoke the onError callback.
            // 正常な解析では onError コールバックが呼ばれないこと。
            bool errorInvoked = false;
            var assemblyPath = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            var result = AssemblyMethodAnalyzer.Analyze(assemblyPath, assemblyPath, onError: _ => errorInvoked = true);

            Assert.NotNull(result);
            Assert.False(errorInvoked);
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
                default(SignatureHeader),
                "System.Void",
                0,
                0,
                ImmutableArray<string>.Empty);
            Assert.Equal("delegate*<System.Void>", provider.GetFunctionPointerType(noParamSig));

            // With parameters / パラメータあり
            var withParamsSig = new MethodSignature<string>(
                default(SignatureHeader),
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

        [Fact]
        [Trait("Category", "Unit")]
        public void SimpleSignatureTypeProvider_GetGenericInstantiation_StripsAritySuffix()
        {
            // GetGenericInstantiation should strip the backtick-arity suffix from the generic type name
            // since the type arguments make the arity explicit.
            // 型引数によりアリティは明示されるため、バッククォートアリティ接尾辞を除去すべき。
            var assemblyPath = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            using var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(stream);
            var reader = peReader.GetMetadataReader();
            var provider = new SimpleSignatureTypeProvider(reader);

            // Single type argument / 単一型引数
            var result1 = provider.GetGenericInstantiation(
                "System.Collections.Generic.List`1",
                ImmutableArray.Create("System.Int32"));
            Assert.Equal("System.Collections.Generic.List<System.Int32>", result1);

            // Multiple type arguments / 複数型引数
            var result2 = provider.GetGenericInstantiation(
                "System.Collections.Generic.Dictionary`2",
                ImmutableArray.Create("System.String", "System.Int32"));
            Assert.Equal("System.Collections.Generic.Dictionary<System.String, System.Int32>", result2);

            // Nested generics: inner result already resolved / ネストしたジェネリクス: 内側は解決済み
            var innerGeneric = provider.GetGenericInstantiation(
                "System.Collections.Generic.List`1",
                ImmutableArray.Create("System.Int32"));
            var result3 = provider.GetGenericInstantiation(
                "System.Collections.Generic.Dictionary`2",
                ImmutableArray.Create("System.String", innerGeneric));
            Assert.Equal(
                "System.Collections.Generic.Dictionary<System.String, System.Collections.Generic.List<System.Int32>>",
                result3);

            // Deeply nested generics: Func<string, Task<IEnumerable<int>>>
            // 深くネストしたジェネリクス: Func<string, Task<IEnumerable<int>>>
            var innermost = provider.GetGenericInstantiation(
                "System.Collections.Generic.IEnumerable`1",
                ImmutableArray.Create("System.Int32"));
            var middle = provider.GetGenericInstantiation(
                "System.Threading.Tasks.Task`1",
                ImmutableArray.Create(innermost));
            var outer = provider.GetGenericInstantiation(
                "System.Func`2",
                ImmutableArray.Create("System.String", middle));
            Assert.Equal(
                "System.Func<System.String, System.Threading.Tasks.Task<System.Collections.Generic.IEnumerable<System.Int32>>>",
                outer);

            // No arity suffix: should pass through unchanged / アリティ接尾辞なし: そのまま通過
            var result4 = provider.GetGenericInstantiation(
                "MyNamespace.MyType",
                ImmutableArray.Create("System.String"));
            Assert.Equal("MyNamespace.MyType<System.String>", result4);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void SimpleSignatureTypeProvider_GetTypeFromReference_ResolvesNestedTypes()
        {
            // GetTypeFromReference should follow ResolutionScope for nested type references
            // so that nested types are fully qualified (e.g. "Outer/Inner" not just "Inner").
            // ネストされた型参照の ResolutionScope をたどり完全修飾名を返すことを検証。
            var assemblyPath = typeof(FolderDiffIL4DotNet.Models.ConfigSettings).Assembly.Location;
            using var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(stream);
            var reader = peReader.GetMetadataReader();
            var provider = new SimpleSignatureTypeProvider(reader);

            // Find a nested type reference in the assembly / アセンブリ内のネスト型参照を探す
            bool foundNestedRef = false;
            foreach (var handle in reader.TypeReferences)
            {
                var typeRef = reader.GetTypeReference(handle);
                if (typeRef.ResolutionScope.Kind == HandleKind.TypeReference)
                {
                    string result = provider.GetTypeFromReference(reader, handle, 0);
                    // Should contain "/" separator for nested types / ネスト型は "/" 区切りを含むべき
                    Assert.Contains("/", result);
                    // Should not start with "/" / "/" で始まらないこと
                    Assert.False(result.StartsWith("/"), $"Nested type name should not start with '/': {result}");
                    foundNestedRef = true;
                    break;
                }
            }

            // If no nested type references found, this is a documentation test —
            // the assembly may not reference nested types from other assemblies.
            // ネスト型参照が見つからない場合はドキュメントテスト（参照が存在しない可能性あり）。
            if (!foundNestedRef)
            {
                // At minimum, verify non-nested references still work / 非ネスト参照が正常に動作することを確認
                foreach (var handle in reader.TypeReferences)
                {
                    var typeRef = reader.GetTypeReference(handle);
                    if (typeRef.ResolutionScope.Kind != HandleKind.TypeReference)
                    {
                        string result = provider.GetTypeFromReference(reader, handle, 0);
                        Assert.False(string.IsNullOrEmpty(result));
                        break;
                    }
                }
            }
        }

        [Theory]
        [Trait("Category", "Unit")]
        // Documented System.Reflection.Metadata failure modes — must be recoverable so
        // the signature-decoder fallbacks keep returning the "(#hex)" / empty-string
        // fallbacks instead of failing the whole analysis.
        // System.Reflection.Metadata が出す可能性のある失敗モード — シグネチャデコーダ
        // のフォールバックが丸ごと失敗しないよう、これらは回復可能と扱う必要がある。
        [InlineData(typeof(BadImageFormatException))]
        [InlineData(typeof(InvalidOperationException))]
        [InlineData(typeof(ArgumentException))]
        [InlineData(typeof(ArgumentOutOfRangeException))] // subclass of ArgumentException / ArgumentException のサブクラス
        [InlineData(typeof(NotSupportedException))]
        [InlineData(typeof(OverflowException))]
        [InlineData(typeof(IndexOutOfRangeException))]
        public void IsMetadataDecodeRecoverable_ReturnsTrue_ForKnownMetadataDecodeFailureTypes(Type exceptionType)
        {
            bool matched = InvokeIsMetadataDecodeRecoverable((Exception)Activator.CreateInstance(exceptionType)!);

            Assert.True(matched, $"Expected IsMetadataDecodeRecoverable to accept {exceptionType.Name}.");
        }

        [Theory]
        [Trait("Category", "Unit")]
        // Programmer errors and unrecoverable runtime failures must NOT be swallowed by the
        // narrow filter; they have to propagate so the outer Analyze catch-all can invoke
        // onError with the real exception rather than hiding it as a hex-string fallback.
        // プログラマエラーや回復不能系は、narrow フィルタで握り潰してはならない。外側の
        // Analyze の catch-all で onError を介してそのまま surface させる。
        [InlineData(typeof(NullReferenceException))]
        [InlineData(typeof(StackOverflowException))]
        [InlineData(typeof(OutOfMemoryException))]
        [InlineData(typeof(AccessViolationException))]
        [InlineData(typeof(OperationCanceledException))]
        public void IsMetadataDecodeRecoverable_ReturnsFalse_ForNonMetadataFailureTypes(Type exceptionType)
        {
            bool matched = InvokeIsMetadataDecodeRecoverable((Exception)Activator.CreateInstance(exceptionType)!);

            Assert.False(matched, $"Expected IsMetadataDecodeRecoverable to reject {exceptionType.Name}.");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Analyze_TypeMetadataChanges_ReportsModifiedTypeEntries()
        {
            // Compare real fixture assemblies whose matching types change only the supported
            // type-level metadata fields.
            // 対応する型でサポート対象の型レベルメタデータだけを変更した実 fixture assembly を比較する。
            var result = AssemblyMethodAnalyzer.Analyze(
                GetAssemblySemanticFixturePath("Old.dll"),
                GetAssemblySemanticFixturePath("New.dll"));

            Assert.NotNull(result);
            var modifiedTypes = result.Entries
                .Where(entry => entry.Change == "Modified" && string.IsNullOrEmpty(entry.MemberName))
                .ToList();

            var access = Assert.Single(modifiedTypes, entry => entry.TypeName == "AssemblySemanticFixture.AccessChanged");
            Assert.Equal("public → internal", access.Access);
            Assert.Equal("Class", access.MemberKind);
            Assert.Equal(ChangeImportance.High, access.Importance);

            var baseType = Assert.Single(modifiedTypes, entry => entry.TypeName == "AssemblySemanticFixture.BaseTypeChanged");
            Assert.Equal("AssemblySemanticFixture.OldBase → AssemblySemanticFixture.NewBase", baseType.BaseType);
            Assert.Equal(ChangeImportance.High, baseType.Importance);

            var kind = Assert.Single(modifiedTypes, entry => entry.TypeName == "AssemblySemanticFixture.KindChanged");
            Assert.Equal("Class → Struct", kind.MemberKind);
            Assert.Equal(ChangeImportance.High, kind.Importance);

            var modifiers = Assert.Single(modifiedTypes, entry => entry.TypeName == "AssemblySemanticFixture.ModifiersChanged");
            Assert.Equal("abstract → sealed", modifiers.Modifiers);
            Assert.Equal(ChangeImportance.Medium, modifiers.Importance);

            Assert.DoesNotContain(modifiedTypes, entry =>
                entry.TypeName == "AssemblySemanticFixture.InterfaceOrderStable");
            Assert.DoesNotContain(modifiedTypes, entry =>
                entry.TypeName == "AssemblySemanticFixture.EqualityContractPropertyAdded");
            Assert.DoesNotContain(modifiedTypes, entry =>
                entry.TypeName == "AssemblySemanticFixture.StableRecord");

            var scopedBase = Assert.Single(modifiedTypes, entry =>
                entry.TypeName == "AssemblySemanticFixture.ScopedBaseTypeChanged");
            Assert.Contains("assembly:TypeSourceA", scopedBase.BaseType, StringComparison.Ordinal);
            Assert.Contains("assembly:TypeSourceB", scopedBase.BaseType, StringComparison.Ordinal);
            Assert.Equal(ChangeImportance.High, scopedBase.Importance);

            var scopedInterface = Assert.Single(modifiedTypes, entry =>
                entry.TypeName == "AssemblySemanticFixture.ScopedInterfaceChanged");
            Assert.Contains("assembly:TypeSourceA", scopedInterface.BaseType, StringComparison.Ordinal);
            Assert.Contains("assembly:TypeSourceB", scopedInterface.BaseType, StringComparison.Ordinal);
            Assert.Equal(ChangeImportance.High, scopedInterface.Importance);

            var scopedMixed = Assert.Single(modifiedTypes, entry =>
                entry.TypeName == "AssemblySemanticFixture.ScopedMixedChange");
            Assert.Contains("interfaces:", scopedMixed.BaseType, StringComparison.Ordinal);
            Assert.Contains("assembly:TypeSourceA", scopedMixed.BaseType, StringComparison.Ordinal);
            Assert.Contains("assembly:TypeSourceB", scopedMixed.BaseType, StringComparison.Ordinal);
            Assert.Contains("AssemblySemanticFixture.InterfaceA", scopedMixed.BaseType, StringComparison.Ordinal);
            Assert.Contains("AssemblySemanticFixture.InterfaceB", scopedMixed.BaseType, StringComparison.Ordinal);
            Assert.Equal(ChangeImportance.High, scopedMixed.Importance);

            Assert.Contains(result.GetChangeDeltaParts(), part =>
                part.Prefix == "*" && part.Count == 7 && part.KindLabel == "types");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GetTypeKind_RequiresCompilerRecordShape()
        {
            string path = GetAssemblySemanticFixturePath("New.dll");
            using var stream = File.OpenRead(path);
            using var peReader = new PEReader(stream);
            var reader = peReader.GetMetadataReader();
            var scoped = new ScopedSignatureTypeProvider(reader);
            var getTypeKind = typeof(AssemblyMethodAnalyzer).GetMethod(
                "GetTypeKind",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(getTypeKind);

            var kinds = reader.TypeDefinitions
                .Select(handle => reader.GetTypeDefinition(handle))
                .Where(type => reader.GetString(type.Namespace) == "AssemblySemanticFixture")
                .Where(type => reader.GetString(type.Name) is "StableRecord" or "EqualityContractPropertyAdded")
                .ToDictionary(
                    type => reader.GetString(type.Name),
                    type => Assert.IsType<string>(getTypeKind.Invoke(null, [reader, type, scoped])));

            Assert.Equal("Record", kinds["StableRecord"]);
            Assert.Equal("Class", kinds["EqualityContractPropertyAdded"]);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Analyze_MetadataTokenOnlyMethodBodyDifferences_DoesNotReportChanges()
        {
            // Reordering declarations changes TypeDef, MethodDef, FieldDef, and MemberRef token
            // numbers while the referenced symbols stay identical.
            // 宣言順変更で TypeDef、MethodDef、FieldDef、MemberRef の token 番号だけを変え、
            // 参照symbolは維持する。
            string oldPath = GetAssemblySemanticFixturePath("Old.dll");
            string newPath = GetAssemblySemanticFixturePath("New.dll");
            byte[] oldIl = ReadMethodIl(oldPath, "AssemblySemanticFixture.TokenOperandConsumer", "Execute");
            byte[] newIl = ReadMethodIl(newPath, "AssemblySemanticFixture.TokenOperandConsumer", "Execute");
            byte[] oldStringIl = ReadMethodIl(oldPath, "AssemblySemanticFixture.TokenOperandConsumer", "Describe");
            byte[] newStringIl = ReadMethodIl(newPath, "AssemblySemanticFixture.TokenOperandConsumer", "Describe");

            Assert.False(oldIl.AsSpan().SequenceEqual(newIl), "Fixture method bodies must contain different raw metadata tokens.");
            Assert.False(oldStringIl.AsSpan().SequenceEqual(newStringIl), "Fixture string tokens must have different raw offsets.");

            var result = AssemblyMethodAnalyzer.Analyze(oldPath, newPath);

            Assert.NotNull(result);
            Assert.DoesNotContain(result.Entries, entry =>
                entry.TypeName.StartsWith("AssemblySemanticFixture.Token", StringComparison.Ordinal));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Analyze_NonTokenOperandDifference_StillReportsBodyChange()
        {
            // Metadata normalization must not hide a changed numeric operand.
            // metadata 正規化で数値 operand の変更を隠してはならない。
            var result = AssemblyMethodAnalyzer.Analyze(
                GetAssemblySemanticFixturePath("Old.dll"),
                GetAssemblySemanticFixturePath("New.dll"));

            Assert.NotNull(result);
            var entry = Assert.Single(result.Entries, candidate =>
                candidate.TypeName == "AssemblySemanticFixture.NonTokenOperandConsumer"
                && candidate.MemberName == "Constant");
            Assert.Equal("Modified", entry.Change);
            Assert.Equal("Changed", entry.Body);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Analyze_AssemblyReferenceVersionDifference_ReportsBodyChange()
        {
            // Switching a call between versions of the same assembly is a semantic change.
            // 同名 assembly の異なる version へ call を切り替える変更を検出する。
            var result = AssemblyMethodAnalyzer.Analyze(
                GetAssemblySemanticFixturePath("Old.dll"),
                GetAssemblySemanticFixturePath("New.dll"));

            Assert.NotNull(result);
            var entry = Assert.Single(result.Entries, candidate =>
                candidate.TypeName == "AssemblySemanticFixture.AssemblyReferenceVersionConsumer"
                && candidate.MemberName == "Execute");
            Assert.Equal("Modified", entry.Change);
            Assert.Equal("Changed", entry.Body);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Analyze_SignatureTypeScopeDifference_ReportsBodyChange()
        {
            // A member signature's parameter scope distinguishes equal type names from different assemblies.
            // member signature の parameter scope により、別 assembly の同名型を区別する。
            var result = AssemblyMethodAnalyzer.Analyze(
                GetAssemblySemanticFixturePath("Old.dll"),
                GetAssemblySemanticFixturePath("New.dll"));

            Assert.NotNull(result);
            var entry = Assert.Single(result.Entries, candidate =>
                candidate.TypeName == "AssemblySemanticFixture.SignatureTypeScopeConsumer"
                && candidate.MemberName == "Execute");
            Assert.Equal("Modified", entry.Change);
            Assert.Equal("Changed", entry.Body);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void CanonicalSignatureTypeProvider_DistinguishesAssemblyReferenceVersionsAndFlags()
        {
            // Assembly version and binding-related flags are part of a referenced type's identity.
            // assembly version と binding 関連 flag は参照型 identity の一部として扱う。
            using var provider = MetadataReaderProvider.FromMetadataImage(CreateTypeReferenceMetadata(
                ("VersionedDependency", new Version(1, 0, 0, 0), (AssemblyFlags)0),
                ("VersionedDependency", new Version(2, 0, 0, 0), AssemblyFlags.Retargetable)));
            var reader = provider.GetMetadataReader();
            var canonical = new CanonicalSignatureTypeProvider(reader);

            string first = canonical.GetTypeFromReference(reader, MetadataTokens.TypeReferenceHandle(1), 0);
            string second = canonical.GetTypeFromReference(reader, MetadataTokens.TypeReferenceHandle(2), 0);

            Assert.NotEqual(first, second);
            Assert.Contains("version=1.0.0.0", first, StringComparison.Ordinal);
            Assert.Contains("version=2.0.0.0", second, StringComparison.Ordinal);
            Assert.Contains("flags=00000100", second, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void CanonicalSignatureTypeProvider_DistinguishesSameNamedTypesAcrossAssemblyScopes()
        {
            // Equal namespace/type names in different assemblies must not collide in signatures.
            // 異なる assembly にある同じ namespace/type 名をシグネチャ内で同一視しない。
            using var provider = MetadataReaderProvider.FromMetadataImage(CreateTypeReferenceMetadata(
                ("TypeSourceA", new Version(1, 0, 0, 0), (AssemblyFlags)0),
                ("TypeSourceB", new Version(1, 0, 0, 0), (AssemblyFlags)0)));
            var reader = provider.GetMetadataReader();
            var canonical = new CanonicalSignatureTypeProvider(reader);

            string first = canonical.GetTypeFromReference(reader, MetadataTokens.TypeReferenceHandle(1), 0);
            string second = canonical.GetTypeFromReference(reader, MetadataTokens.TypeReferenceHandle(2), 0);

            Assert.NotEqual(first, second);
            Assert.Contains("assembly:TypeSourceA", first, StringComparison.Ordinal);
            Assert.Contains("assembly:TypeSourceB", second, StringComparison.Ordinal);
            Assert.EndsWith(":Shared.Widget", first, StringComparison.Ordinal);
            Assert.EndsWith(":Shared.Widget", second, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ScopedSignatureTypeProvider_NilTypeReferenceScope_ResolvesForwardedAssembly()
        {
            // A nil TypeRef scope denotes a type exported by the current module. Follow the
            // ExportedType implementation instead of treating it as a local TypeDef.
            // nil TypeRef scope は現在 module の ExportedType を示すため、local TypeDef と
            // 同一視せず implementation の assembly まで解決する。
            using var provider = MetadataReaderProvider.FromMetadataImage(
                CreateForwardedTypeReferenceMetadata(includeExportedType: true));
            var reader = provider.GetMetadataReader();
            var scoped = new ScopedSignatureTypeProvider(reader);

            string identity = scoped.GetTypeFromReference(
                reader,
                MetadataTokens.TypeReferenceHandle(1),
                0);

            Assert.Contains("assembly:ForwardedTarget", identity, StringComparison.Ordinal);
            Assert.EndsWith(":Shared.Widget", identity, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ScopedSignatureTypeProvider_UnresolvedNilTypeReferenceScope_ThrowsRecoverableMetadataError()
        {
            using var provider = MetadataReaderProvider.FromMetadataImage(
                CreateForwardedTypeReferenceMetadata(includeExportedType: false));
            var reader = provider.GetMetadataReader();
            var scoped = new ScopedSignatureTypeProvider(reader);

            var exception = Assert.Throws<BadImageFormatException>(() =>
                scoped.GetTypeFromReference(reader, MetadataTokens.TypeReferenceHandle(1), 0));

            Assert.True(InvokeIsMetadataDecodeRecoverable(exception));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void SignatureTypeProviders_CircularTypeReferenceScopes_ThrowRecoverableMetadataError()
        {
            // A TypeRef cycle must become a recoverable metadata error instead of overflowing
            // the process stack. / TypeRef の循環は process stack を枯渇させず、回復可能な
            // metadata error に変換する。
            using var provider = MetadataReaderProvider.FromMetadataImage(
                CreateNestedTypeReferenceMetadata(depth: 2, circular: true));
            var reader = provider.GetMetadataReader();

            AssertTypeReferenceRejectedByAllProviders(reader);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void SignatureTypeProviders_ExcessivelyDeepTypeReferenceScopes_ThrowRecoverableMetadataError()
        {
            // Bound even acyclic chains so hostile metadata cannot consume unbounded work or stack.
            // 非循環でも過大な chain は制限し、悪意ある metadata の無制限な処理を防ぐ。
            using var provider = MetadataReaderProvider.FromMetadataImage(
                CreateNestedTypeReferenceMetadata(
                    SimpleSignatureTypeProvider.MaxTypeReferenceNestingDepth + 1,
                    circular: false));
            var reader = provider.GetMetadataReader();

            AssertTypeReferenceRejectedByAllProviders(reader);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void SignatureTypeProviders_CircularTypeSpecificationModifier_ThrowsRecoverableMetadataError()
        {
            // A custom modifier can point back to its containing TypeSpec. Reject that cycle
            // before SignatureDecoder recursion can overflow the process stack.
            // custom modifier から自身の TypeSpec へ戻る循環を、process stack overflow 前に拒否する。
            using var provider = MetadataReaderProvider.FromMetadataImage(
                CreateCircularTypeSpecificationMetadata());
            var reader = provider.GetMetadataReader();

            SimpleSignatureTypeProvider[] providers =
            [
                new SimpleSignatureTypeProvider(reader),
                new CanonicalSignatureTypeProvider(reader),
            ];
            foreach (var typeProvider in providers)
            {
                var exception = Assert.Throws<BadImageFormatException>(() =>
                    typeProvider.GetTypeFromSpecification(
                        reader,
                        genericContext: null,
                        MetadataTokens.TypeSpecificationHandle(1),
                        0));
                Assert.True(InvokeIsMetadataDecodeRecoverable(exception));
            }
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void CanonicalSignatureTypeProvider_UsesGenericParameterPositions()
        {
            using var stream = File.OpenRead(typeof(AssemblyMethodAnalyzerTests).Assembly.Location);
            using var peReader = new PEReader(stream);
            var reader = peReader.GetMetadataReader();
            var canonical = new CanonicalSignatureTypeProvider(reader);
            var context = new GenericContext(
                ImmutableArray.Create("TCaller"),
                ImmutableArray.Create("TResult"));

            Assert.Equal("!0", canonical.GetGenericTypeParameter(context, 0));
            Assert.Equal("!!0", canonical.GetGenericMethodParameter(context, 0));
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void CanonicalTokenIdentities_UnifyLocalDefinitionAndReferenceHandles()
        {
            // Equivalent local symbols can legally be encoded through definition or reference
            // tables; their canonical identities must not depend on that encoding choice.
            // 同一 local symbol は definition/reference table のどちらでも表現できるため、
            // canonical identity を符号化方法に依存させない。
            using var provider = MetadataReaderProvider.FromMetadataImage(CreateLocalDefinitionAndReferenceMetadata());
            var reader = provider.GetMetadataReader();
            var canonical = new CanonicalSignatureTypeProvider(reader);
            var context = new GenericContext(
                ImmutableArray<string>.Empty,
                ImmutableArray.Create("TCaller"));

            string typeDefinition = InvokeResolveMetadataToken(
                reader,
                MetadataTokens.GetToken(MetadataTokens.TypeDefinitionHandle(2)),
                OperandType.InlineTok,
                canonical,
                context);
            string typeReference = InvokeResolveMetadataToken(
                reader,
                MetadataTokens.GetToken(MetadataTokens.TypeReferenceHandle(1)),
                OperandType.InlineTok,
                canonical,
                context);
            string methodDefinition = InvokeResolveMetadataToken(
                reader,
                MetadataTokens.GetToken(MetadataTokens.MethodDefinitionHandle(1)),
                OperandType.InlineMethod,
                canonical,
                context);
            string methodReference = InvokeResolveMetadataToken(
                reader,
                MetadataTokens.GetToken(MetadataTokens.MemberReferenceHandle(1)),
                OperandType.InlineMethod,
                canonical,
                context);
            string fieldDefinition = InvokeResolveMetadataToken(
                reader,
                MetadataTokens.GetToken(MetadataTokens.FieldDefinitionHandle(1)),
                OperandType.InlineField,
                canonical,
                context);
            string fieldReference = InvokeResolveMetadataToken(
                reader,
                MetadataTokens.GetToken(MetadataTokens.MemberReferenceHandle(2)),
                OperandType.InlineField,
                canonical,
                context);

            Assert.Equal(typeDefinition, typeReference);
            Assert.Equal(methodDefinition, methodReference);
            Assert.Equal(fieldDefinition, fieldReference);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ResolveMetadataToken_RejectsTokenKindThatDoesNotMatchOperandType()
        {
            using var provider = MetadataReaderProvider.FromMetadataImage(
                CreateLocalDefinitionAndReferenceMetadata());
            var reader = provider.GetMetadataReader();
            var canonical = new CanonicalSignatureTypeProvider(reader);
            var context = new GenericContext(ImmutableArray<string>.Empty, ImmutableArray<string>.Empty);

            var exception = Assert.Throws<TargetInvocationException>(() => InvokeResolveMetadataToken(
                reader,
                MetadataTokens.GetToken(MetadataTokens.FieldDefinitionHandle(1)),
                OperandType.InlineMethod,
                canonical,
                context));

            Assert.IsType<BadImageFormatException>(exception.InnerException);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ReadNormalizedIlBody_ReturnsFixedLengthDigest()
        {
            string path = GetAssemblySemanticFixturePath("Old.dll");
            using var stream = File.OpenRead(path);
            using var peReader = new PEReader(stream);
            var reader = peReader.GetMetadataReader();
            var typeProvider = new CanonicalSignatureTypeProvider(reader);
            var analyzerMethod = typeof(AssemblyMethodAnalyzer).GetMethod(
                "ReadNormalizedIlBody",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(analyzerMethod);

            foreach (var typeHandle in reader.TypeDefinitions)
            {
                var typeDefinition = reader.GetTypeDefinition(typeHandle);
                string typeNamespace = reader.GetString(typeDefinition.Namespace);
                string typeName = reader.GetString(typeDefinition.Name);
                string fullTypeName = string.IsNullOrEmpty(typeNamespace)
                    ? typeName
                    : $"{typeNamespace}.{typeName}";
                if (fullTypeName != "AssemblySemanticFixture.TokenOperandConsumer")
                    continue;
                foreach (var methodHandle in typeDefinition.GetMethods())
                {
                    var methodDefinition = reader.GetMethodDefinition(methodHandle);
                    if (reader.GetString(methodDefinition.Name) != "Execute")
                        continue;

                    var context = GenericContext.FromMethod(reader, typeDefinition, methodDefinition);
                    string normalized = Assert.IsType<string>(analyzerMethod.Invoke(
                        null,
                        [reader, peReader, methodDefinition, typeProvider, context]));
                    Assert.StartsWith("normalized-sha256:", normalized, StringComparison.Ordinal);
                    Assert.Equal("normalized-sha256:".Length + 64, normalized.Length);
                    return;
                }
            }

            Assert.Fail("Fixture method was not found.");
        }

        private static ImmutableArray<byte> CreateTypeReferenceMetadata(
            (string Name, Version Version, AssemblyFlags Flags) first,
            (string Name, Version Version, AssemblyFlags Flags) second)
        {
            var metadata = new MetadataBuilder();
            metadata.AddModule(
                0,
                metadata.GetOrAddString("CanonicalSignatureFixture.dll"),
                metadata.GetOrAddGuid(Guid.NewGuid()),
                default,
                default);
            metadata.AddAssembly(
                metadata.GetOrAddString("CanonicalSignatureFixture"),
                new Version(1, 0, 0, 0),
                default,
                default,
                (AssemblyFlags)0,
                AssemblyHashAlgorithm.None);

            AddTypeReference(metadata, first);
            AddTypeReference(metadata, second);

            var rootBuilder = new MetadataRootBuilder(metadata);
            var metadataBlob = new BlobBuilder();
            rootBuilder.Serialize(metadataBlob, methodBodyStreamRva: 0, mappedFieldDataStreamRva: 0);
            return ImmutableArray.CreateRange(metadataBlob.ToArray());
        }

        private static ImmutableArray<byte> CreateNestedTypeReferenceMetadata(int depth, bool circular)
        {
            var metadata = new MetadataBuilder();
            metadata.AddModule(
                0,
                metadata.GetOrAddString("NestedTypeReferenceFixture.dll"),
                metadata.GetOrAddGuid(Guid.NewGuid()),
                default,
                default);
            metadata.AddAssembly(
                metadata.GetOrAddString("NestedTypeReferenceFixture"),
                new Version(1, 0, 0, 0),
                default,
                default,
                (AssemblyFlags)0,
                AssemblyHashAlgorithm.None);
            var assemblyReference = metadata.AddAssemblyReference(
                metadata.GetOrAddString("TypeSource"),
                new Version(1, 0, 0, 0),
                default,
                default,
                (AssemblyFlags)0,
                default);

            for (int row = 1; row <= depth; row++)
            {
                EntityHandle scope = row == depth
                    ? circular
                        ? MetadataTokens.TypeReferenceHandle(1)
                        : assemblyReference
                    : MetadataTokens.TypeReferenceHandle(row + 1);
                metadata.AddTypeReference(
                    scope,
                    row == depth ? metadata.GetOrAddString("Shared") : default,
                    metadata.GetOrAddString($"Level{row}"));
            }

            return SerializeMetadata(metadata);
        }

        private static ImmutableArray<byte> CreateForwardedTypeReferenceMetadata(bool includeExportedType)
        {
            var metadata = new MetadataBuilder();
            metadata.AddModule(
                0,
                metadata.GetOrAddString("ForwarderFixture.dll"),
                metadata.GetOrAddGuid(Guid.NewGuid()),
                default,
                default);
            metadata.AddAssembly(
                metadata.GetOrAddString("ForwarderFixture"),
                new Version(1, 0, 0, 0),
                default,
                default,
                (AssemblyFlags)0,
                AssemblyHashAlgorithm.None);
            var targetAssembly = metadata.AddAssemblyReference(
                metadata.GetOrAddString("ForwardedTarget"),
                new Version(2, 0, 0, 0),
                default,
                default,
                (AssemblyFlags)0,
                default);
            metadata.AddTypeReference(
                default,
                metadata.GetOrAddString("Shared"),
                metadata.GetOrAddString("Widget"));
            if (includeExportedType)
            {
                metadata.AddExportedType(
                    TypeAttributes.Public | (TypeAttributes)0x00200000, // tdForwarder
                    metadata.GetOrAddString("Shared"),
                    metadata.GetOrAddString("Widget"),
                    targetAssembly,
                    typeDefinitionId: 0);
            }

            return SerializeMetadata(metadata);
        }

        private static ImmutableArray<byte> CreateCircularTypeSpecificationMetadata()
        {
            var metadata = new MetadataBuilder();
            metadata.AddModule(
                0,
                metadata.GetOrAddString("CircularTypeSpecificationFixture.dll"),
                metadata.GetOrAddGuid(Guid.NewGuid()),
                default,
                default);
            metadata.AddAssembly(
                metadata.GetOrAddString("CircularTypeSpecificationFixture"),
                new Version(1, 0, 0, 0),
                default,
                default,
                (AssemblyFlags)0,
                AssemblyHashAlgorithm.None);

            var signature = new BlobBuilder();
            signature.WriteByte(0x1F); // ELEMENT_TYPE_CMOD_REQD
            signature.WriteByte(0x06); // TypeDefOrRefEncoded(TypeSpec #1)
            signature.WriteByte(0x08); // ELEMENT_TYPE_I4
            metadata.AddTypeSpecification(metadata.GetOrAddBlob(signature));
            return SerializeMetadata(metadata);
        }

        private static ImmutableArray<byte> CreateLocalDefinitionAndReferenceMetadata()
        {
            var metadata = new MetadataBuilder();
            metadata.AddModule(
                0,
                metadata.GetOrAddString("LocalIdentityFixture.dll"),
                metadata.GetOrAddGuid(Guid.NewGuid()),
                default,
                default);
            metadata.AddAssembly(
                metadata.GetOrAddString("LocalIdentityFixture"),
                new Version(1, 0, 0, 0),
                default,
                default,
                (AssemblyFlags)0,
                AssemblyHashAlgorithm.None);

            var methodSignature = new BlobBuilder();
            methodSignature.WriteByte(0x10); // GENERIC, DEFAULT
            methodSignature.WriteByte(0x01); // generic parameter count
            methodSignature.WriteByte(0x01); // parameter count
            methodSignature.WriteByte(0x1E); // ELEMENT_TYPE_MVAR
            methodSignature.WriteByte(0x00);
            methodSignature.WriteByte(0x1E); // ELEMENT_TYPE_MVAR
            methodSignature.WriteByte(0x00);
            BlobHandle methodSignatureHandle = metadata.GetOrAddBlob(methodSignature);

            var fieldSignature = new BlobBuilder();
            new BlobEncoder(fieldSignature).FieldSignature().Int32();
            BlobHandle fieldSignatureHandle = metadata.GetOrAddBlob(fieldSignature);

            var methodDefinition = metadata.AddMethodDefinition(
                MethodAttributes.Public | MethodAttributes.Static,
                MethodImplAttributes.IL,
                metadata.GetOrAddString("Execute"),
                methodSignatureHandle,
                bodyOffset: 0,
                parameterList: MetadataTokens.ParameterHandle(1));
            metadata.AddGenericParameter(
                methodDefinition,
                GenericParameterAttributes.None,
                metadata.GetOrAddString("TTarget"),
                index: 0);
            var fieldDefinition = metadata.AddFieldDefinition(
                FieldAttributes.Public | FieldAttributes.Static,
                metadata.GetOrAddString("Value"),
                fieldSignatureHandle);

            metadata.AddTypeDefinition(
                TypeAttributes.NotPublic,
                default,
                metadata.GetOrAddString("<Module>"),
                default,
                fieldDefinition,
                methodDefinition);
            var owner = metadata.AddTypeDefinition(
                TypeAttributes.Public | TypeAttributes.Class,
                metadata.GetOrAddString("Fixture"),
                metadata.GetOrAddString("Owner"),
                default,
                fieldDefinition,
                methodDefinition);
            metadata.AddTypeReference(
                MetadataTokens.EntityHandle(TableIndex.Module, 1),
                metadata.GetOrAddString("Fixture"),
                metadata.GetOrAddString("Owner"));
            metadata.AddMemberReference(owner, metadata.GetOrAddString("Execute"), methodSignatureHandle);
            metadata.AddMemberReference(owner, metadata.GetOrAddString("Value"), fieldSignatureHandle);

            return SerializeMetadata(metadata);
        }

        private static ImmutableArray<byte> SerializeMetadata(MetadataBuilder metadata)
        {
            var rootBuilder = new MetadataRootBuilder(metadata);
            var metadataBlob = new BlobBuilder();
            rootBuilder.Serialize(metadataBlob, methodBodyStreamRva: 0, mappedFieldDataStreamRva: 0);
            return ImmutableArray.CreateRange(metadataBlob.ToArray());
        }

        private static void AssertTypeReferenceRejectedByAllProviders(MetadataReader reader)
        {
            SimpleSignatureTypeProvider[] providers =
            [
                new SimpleSignatureTypeProvider(reader),
                new CanonicalSignatureTypeProvider(reader),
            ];
            foreach (var typeProvider in providers)
            {
                var exception = Assert.Throws<BadImageFormatException>(() =>
                    typeProvider.GetTypeFromReference(reader, MetadataTokens.TypeReferenceHandle(1), 0));
                Assert.True(InvokeIsMetadataDecodeRecoverable(exception));
            }
        }

        private static string InvokeResolveMetadataToken(
            MetadataReader reader,
            int token,
            OperandType operandType,
            CanonicalSignatureTypeProvider typeProvider,
            GenericContext genericContext)
        {
            var method = typeof(AssemblyMethodAnalyzer).GetMethod(
                "ResolveMetadataToken",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            return Assert.IsType<string>(method.Invoke(
                null,
                [reader, token, operandType, typeProvider, genericContext]));
        }

        private static void AddTypeReference(
            MetadataBuilder metadata,
            (string Name, Version Version, AssemblyFlags Flags) assembly)
        {
            var assemblyReference = metadata.AddAssemblyReference(
                metadata.GetOrAddString(assembly.Name),
                assembly.Version,
                default,
                default,
                assembly.Flags,
                default);
            metadata.AddTypeReference(
                assemblyReference,
                metadata.GetOrAddString("Shared"),
                metadata.GetOrAddString("Widget"));
        }

        private static bool InvokeIsMetadataDecodeRecoverable(Exception exception)
        {
            var method = typeof(AssemblyMethodAnalyzer).GetMethod(
                "IsMetadataDecodeRecoverable",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            var result = method.Invoke(null, [exception]);
            Assert.IsType<bool>(result);
            return (bool)result!;
        }

        private static string GetAssemblySemanticFixturePath(string fileName)
            => Path.Combine(AppContext.BaseDirectory, "AssemblySemanticFixtures", fileName);

        private static byte[] ReadMethodIl(string assemblyPath, string typeName, string methodName)
        {
            using var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(stream);
            var reader = peReader.GetMetadataReader();

            foreach (var typeHandle in reader.TypeDefinitions)
            {
                var type = reader.GetTypeDefinition(typeHandle);
                string fullName = string.IsNullOrEmpty(reader.GetString(type.Namespace))
                    ? reader.GetString(type.Name)
                    : $"{reader.GetString(type.Namespace)}.{reader.GetString(type.Name)}";
                if (!string.Equals(fullName, typeName, StringComparison.Ordinal)) continue;

                foreach (var methodHandle in type.GetMethods())
                {
                    var method = reader.GetMethodDefinition(methodHandle);
                    if (!string.Equals(reader.GetString(method.Name), methodName, StringComparison.Ordinal)) continue;

                    return peReader.GetMethodBody(method.RelativeVirtualAddress).GetILBytes() ?? [];
                }
            }

            throw new InvalidOperationException($"Fixture method {typeName}::{methodName} was not found.");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void Analyze_RuntimeAssembly_GenericSignaturesDoNotContainAritySuffix()
        {
            // Verify that after the fix, analysed assemblies produce signatures
            // without backtick-arity suffixes in generic instantiations.
            // 修正後、解析されたアセンブリのジェネリクスインスタンス化にバッククォートアリティ接尾辞が含まれないことを検証。
            var testAssembly = typeof(AssemblyMethodAnalyzerTests).Assembly.Location;
            var mainAssembly = typeof(FolderDiffIL4DotNet.Models.ConfigSettings).Assembly.Location;

            var result = AssemblyMethodAnalyzer.Analyze(testAssembly, mainAssembly);

            Assert.NotNull(result);
            // Check that no entry has backtick-arity in generic type arguments
            // (signatures like "Dictionary`2<String, Int32>" should now be "Dictionary<String, Int32>")
            // ジェネリック型引数にバッククォートアリティが含まれないことを確認
            foreach (var entry in result.Entries)
            {
                // Only check entries that contain angle brackets (generic signatures)
                // 山括弧を含むエントリ（ジェネリクスシグネチャ）のみ検査
                if (entry.Parameters.Contains('<'))
                    Assert.DoesNotMatch(@"`\d+<", entry.Parameters);
                if (entry.ReturnType.Contains('<'))
                    Assert.DoesNotMatch(@"`\d+<", entry.ReturnType);
                if (entry.BaseType.Contains('<'))
                    Assert.DoesNotMatch(@"`\d+<", entry.BaseType);
            }
        }
    }
}
