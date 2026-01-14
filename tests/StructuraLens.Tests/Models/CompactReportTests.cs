using System.Text.Json;
using StructuraLens.Core.Models;

namespace StructuraLens.Tests.Models;

public class CompactReportTests
{
    [Test]
    public async Task CompactNamespace_JsonSerialization_UsesShortPropertyNames()
    {
        // Arrange
        var types = new List<CompactType>
        {
            new() { Name = "TestClass", CyclomaticComplexity = 5, LinesOfCode = 100, AvgMaintainabilityIndex = 75.0, DepthOfInheritance = 1 }
        };
        
        var compactNamespace = new CompactNamespace
        {
            Name = "TestNamespace",
            TypeCount = 1,
            MethodCount = 5,
            CyclomaticComplexity = 10,
            LinesOfCode = 100,
            MaxDepthOfInheritance = 2,
            AvgMaintainabilityIndex = 75.5,
            Types = types
        };

        // Act
        var json = JsonSerializer.Serialize(compactNamespace, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert - Verify short property names are used
        await Assert.That(json).Contains("\"n\":");
        await Assert.That(json).Contains("\"tc\":");
        await Assert.That(json).Contains("\"mc\":");
        await Assert.That(json).Contains("\"cc\":");
        await Assert.That(json).Contains("\"loc\":");
        await Assert.That(json).Contains("\"dit\":");
        await Assert.That(json).Contains("\"mi\":");
        await Assert.That(json).Contains("\"types\":");
        
        // Verify long names are not used
        await Assert.That(json).DoesNotContain("\"name\":");
        await Assert.That(json).DoesNotContain("\"typeCount\":");
        await Assert.That(json).DoesNotContain("\"methodCount\":");
    }

    [Test]
    public async Task CompactNamespace_Deserialization_WorksCorrectly()
    {
        // Arrange
        var json = """
        {
            "n": "TestNamespace",
            "tc": 2,
            "mc": 10,
            "cc": 15,
            "loc": 200,
            "dit": 3,
            "mi": 80.5,
            "types": []
        }
        """;

        // Act
        var compactNamespace = JsonSerializer.Deserialize<CompactNamespace>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert
        await Assert.That(compactNamespace).IsNotNull();
        await Assert.That(compactNamespace!.Name).IsEqualTo("TestNamespace");
        await Assert.That(compactNamespace.TypeCount).IsEqualTo(2);
        await Assert.That(compactNamespace.MethodCount).IsEqualTo(10);
        await Assert.That(compactNamespace.CyclomaticComplexity).IsEqualTo(15);
        await Assert.That(compactNamespace.LinesOfCode).IsEqualTo(200);
        await Assert.That(compactNamespace.MaxDepthOfInheritance).IsEqualTo(3);
        await Assert.That(compactNamespace.AvgMaintainabilityIndex).IsEqualTo(80.5);
    }

    [Test]
    public async Task CompactType_FullName_SerializedWhenPresent()
    {
        // Arrange
        var compactType = new CompactType
        {
            Name = "TestClass",
            FullName = "MyNamespace.TestClass",
            CyclomaticComplexity = 5,
            LinesOfCode = 100,
            AvgMaintainabilityIndex = 75.0,
            DepthOfInheritance = 1
        };

        // Act
        var json = JsonSerializer.Serialize(compactType, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert
        await Assert.That(json).Contains("\"fn\":");
        await Assert.That(json).Contains("MyNamespace.TestClass");
    }

    [Test]
    public async Task CompactType_FullName_OmittedWhenNull()
    {
        // Arrange
        var compactType = new CompactType
        {
            Name = "TestClass",
            FullName = null,
            CyclomaticComplexity = 5,
            LinesOfCode = 100,
            AvgMaintainabilityIndex = 75.0,
            DepthOfInheritance = 1
        };

        // Act
        var json = JsonSerializer.Serialize(compactType, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert - FullName should be omitted when null due to JsonIgnoreCondition
        await Assert.That(json).DoesNotContain("\"fn\":");
    }

    [Test]
    public async Task CompactProject_Namespaces_OmittedWhenNull()
    {
        // Arrange
        var compactProject = new CompactProject
        {
            Name = "TestProject",
            TypeCount = 5,
            MethodCount = 25,
            CyclomaticComplexity = 50,
            LinesOfCode = 1000,
            MaxDepthOfInheritance = 2,
            AvgMaintainabilityIndex = 75.0,
            Namespaces = null
        };

        // Act
        var json = JsonSerializer.Serialize(compactProject, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert
        await Assert.That(json).DoesNotContain("\"ns\":");
    }

    [Test]
    public async Task CompactProject_Namespaces_IncludedWhenPresent()
    {
        // Arrange
        var compactProject = new CompactProject
        {
            Name = "TestProject",
            TypeCount = 5,
            MethodCount = 25,
            CyclomaticComplexity = 50,
            LinesOfCode = 1000,
            MaxDepthOfInheritance = 2,
            AvgMaintainabilityIndex = 75.0,
            Namespaces = new List<CompactNamespace>
            {
                new() { Name = "NS1", TypeCount = 2, MethodCount = 10, CyclomaticComplexity = 20, LinesOfCode = 500, MaxDepthOfInheritance = 1, AvgMaintainabilityIndex = 80.0 }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(compactProject, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert
        await Assert.That(json).Contains("\"ns\":");
        await Assert.That(json).Contains("\"NS1\"");
    }

    [Test]
    public async Task CompactNamespace_WithTypesAndMethods_SerializesCompletely()
    {
        // Arrange
        var methods = new List<CompactMethod>
        {
            new() { Name = "Method1", CyclomaticComplexity = 2, LinesOfCode = 10, HalsteadVolume = 50.0, MaintainabilityIndex = 85.0, StartLine = 1, EndLine = 10 }
        };
        
        var types = new List<CompactType>
        {
            new() { Name = "Class1", FullName = "NS.Class1", CyclomaticComplexity = 5, LinesOfCode = 100, AvgMaintainabilityIndex = 80.0, DepthOfInheritance = 1, Methods = methods }
        };
        
        var compactNamespace = new CompactNamespace
        {
            Name = "NS",
            TypeCount = 1,
            MethodCount = 1,
            CyclomaticComplexity = 5,
            LinesOfCode = 100,
            MaxDepthOfInheritance = 1,
            AvgMaintainabilityIndex = 80.0,
            Types = types
        };

        // Act
        var json = JsonSerializer.Serialize(compactNamespace, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
        
        var deserialized = JsonSerializer.Deserialize<CompactNamespace>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert - Round-trip serialization should preserve all data
        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.Name).IsEqualTo("NS");
        await Assert.That(deserialized.Types).IsNotNull();
        await Assert.That(deserialized.Types!.Count).IsEqualTo(1);
        await Assert.That(deserialized.Types![0].Methods).IsNotNull();
        await Assert.That(deserialized.Types![0].Methods!.Count).IsEqualTo(1);
        await Assert.That(deserialized.Types![0].FullName).IsEqualTo("NS.Class1");
    }
}
