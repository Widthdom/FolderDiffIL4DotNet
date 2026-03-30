using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using FolderDiffIL4DotNet.Models;
using Xunit;

namespace FolderDiffIL4DotNet.Tests.Models
{
    public sealed partial class ConfigSettingsTests
    {
        /// <summary>
        /// Verifies that config.schema.json properties match ConfigSettingsBuilder public properties,
        /// ensuring the schema stays in sync with the C# model.
        /// config.schema.json のプロパティが ConfigSettingsBuilder の公開プロパティと一致することを検証し、
        /// スキーマと C# モデルの同期を保証します。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void Schema_Properties_MatchConfigSettingsBuilder_Properties()
        {
            var schemaPath = Path.Combine(FindRepoRoot(), "doc", "config.schema.json");
            var schemaJson = File.ReadAllText(schemaPath);
            var schemaDoc = JsonDocument.Parse(schemaJson);

            var schemaProps = schemaDoc.RootElement
                .GetProperty("properties")
                .EnumerateObject()
                .Select(p => p.Name)
                .Where(n => n != "$schema")
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            var builderProps = typeof(ConfigSettingsBuilder)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != "ExtensionData")
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(builderProps, schemaProps);
        }

        /// <summary>
        /// Verifies that config.schema.json disallows unknown properties (additionalProperties: false).
        /// config.schema.json が未知のプロパティを拒否する（additionalProperties: false）ことを検証します。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void Schema_DisallowsAdditionalProperties()
        {
            var schemaPath = Path.Combine(FindRepoRoot(), "doc", "config.schema.json");
            var schemaJson = File.ReadAllText(schemaPath);
            var schemaDoc = JsonDocument.Parse(schemaJson);

            Assert.True(
                schemaDoc.RootElement.TryGetProperty("additionalProperties", out var val),
                "config.schema.json must have 'additionalProperties'");
            Assert.False(val.GetBoolean(), "config.schema.json additionalProperties must be false");
        }

        /// <summary>
        /// Verifies that every schema property has a bilingual description (contains \n for EN+JA separation).
        /// すべてのスキーマプロパティがバイリンガル description（英日改行区切り）を持つことを検証します。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void Schema_AllProperties_HaveBilingualDescriptions()
        {
            var schemaPath = Path.Combine(FindRepoRoot(), "doc", "config.schema.json");
            var schemaJson = File.ReadAllText(schemaPath);
            var schemaDoc = JsonDocument.Parse(schemaJson);

            var missingNewline = new List<string>();
            foreach (var prop in schemaDoc.RootElement.GetProperty("properties").EnumerateObject())
            {
                if (!prop.Value.TryGetProperty("description", out var desc)) continue;
                var descText = desc.GetString() ?? string.Empty;
                if (!descText.Contains('\n'))
                {
                    missingNewline.Add(prop.Name);
                }
            }

            Assert.True(
                missingNewline.Count == 0,
                $"Schema properties missing bilingual description (no newline separator): {string.Join(", ", missingNewline)}");
        }

        /// <summary>
        /// Verifies that JSON with a $schema property deserializes successfully into ConfigSettingsBuilder.
        /// $schema プロパティを含む JSON が ConfigSettingsBuilder に正常にデシリアライズされることを検証します。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void JsonDeserialize_WithSchemaProperty_Succeeds()
        {
            const string json = """
                {
                  "$schema": "./doc/config.schema.json",
                  "MaxLogGenerations": 10
                }
                """;

            var builder = JsonSerializer.Deserialize<ConfigSettingsBuilder>(json);

            Assert.NotNull(builder);
            var config = builder!.Build();
            Assert.Equal(10, config.MaxLogGenerations);
        }

        /// <summary>
        /// Verifies that config.schema.json is valid JSON and has required top-level keys.
        /// config.schema.json が有効な JSON であり、必須のトップレベルキーを持つことを検証します。
        /// </summary>
        [Fact]
        [Trait("Category", "Unit")]
        public void Schema_IsValidJsonWithRequiredTopLevelKeys()
        {
            var schemaPath = Path.Combine(FindRepoRoot(), "doc", "config.schema.json");
            var schemaJson = File.ReadAllText(schemaPath);
            var schemaDoc = JsonDocument.Parse(schemaJson);
            var root = schemaDoc.RootElement;

            Assert.True(root.TryGetProperty("$schema", out _), "Missing $schema");
            Assert.True(root.TryGetProperty("title", out _), "Missing title");
            Assert.True(root.TryGetProperty("description", out _), "Missing description");
            Assert.True(root.TryGetProperty("type", out var typeVal), "Missing type");
            Assert.Equal("object", typeVal.GetString());
            Assert.True(root.TryGetProperty("properties", out _), "Missing properties");
        }
    }
}
