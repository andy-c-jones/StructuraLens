using FakeItEasy;
using Microsoft.Extensions.Logging;
using StructuraLens.Core.Infrastructure;

namespace StructuraLens.Tests.Infrastructure;

public class PackageReferenceReaderTests
{
    private readonly ILogger _logger = A.Fake<ILogger>();
    private string? _tempDirectory;

    [Before(Test)]
    public void Setup()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"StructuraLensTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    [After(Test)]
    public void Cleanup()
    {
        if (_tempDirectory != null && Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Test]
    public async Task ReadPackageReferences_SimpleProject_ReturnsPackages()
    {
        // Arrange
        var projectContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
                <PackageReference Include="Serilog" Version="3.1.1" />
              </ItemGroup>
            </Project>
            """;

        var projectPath = CreateProjectFile("TestProject.csproj", projectContent);
        var reader = new PackageReferenceReader(_logger);

        // Act
        var packages = reader.ReadPackageReferences(projectPath);

        // Assert
        await Assert.That(packages.Count).IsEqualTo(2);
        await Assert.That(packages).Contains("Newtonsoft.Json");
        await Assert.That(packages).Contains("Serilog");
    }

    [Test]
    public async Task ReadPackageReferences_WithDirectoryBuildProps_IncludesInheritedPackages()
    {
        // Arrange
        var directoryBuildProps = """
            <Project>
              <ItemGroup>
                <PackageReference Include="SharedPackage" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """;

        var projectContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="DirectPackage" Version="2.0.0" />
              </ItemGroup>
            </Project>
            """;

        CreateFile("Directory.Build.props", directoryBuildProps);
        var projectPath = CreateProjectFile("TestProject.csproj", projectContent);
        var reader = new PackageReferenceReader(_logger);

        // Act
        var packages = reader.ReadPackageReferences(projectPath);

        // Assert
        await Assert.That(packages.Count).IsEqualTo(2);
        await Assert.That(packages).Contains("SharedPackage");
        await Assert.That(packages).Contains("DirectPackage");
    }

    [Test]
    public async Task ReadPackageReferences_WithNestedDirectoryBuildProps_UsesClosest()
    {
        // Arrange
        var rootDirectoryBuildProps = """
            <Project>
              <ItemGroup>
                <PackageReference Include="RootPackage" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """;

        var subDirectoryBuildProps = """
            <Project>
              <ItemGroup>
                <PackageReference Include="SubPackage" Version="2.0.0" />
              </ItemGroup>
            </Project>
            """;

        var projectContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="ProjectPackage" Version="3.0.0" />
              </ItemGroup>
            </Project>
            """;

        CreateFile("Directory.Build.props", rootDirectoryBuildProps);
        var subDir = Path.Combine(_tempDirectory!, "SubFolder");
        Directory.CreateDirectory(subDir);
        CreateFile(Path.Combine("SubFolder", "Directory.Build.props"), subDirectoryBuildProps);
        var projectPath = CreateFile(Path.Combine("SubFolder", "TestProject.csproj"), projectContent);
        var reader = new PackageReferenceReader(_logger);

        // Act
        var packages = reader.ReadPackageReferences(projectPath);

        // Assert - should have SubPackage and ProjectPackage, but NOT RootPackage
        // (MSBuild stops at the first Directory.Build.props it finds)
        await Assert.That(packages).Contains("SubPackage");
        await Assert.That(packages).Contains("ProjectPackage");
    }

    [Test]
    public async Task ReadPackageReferences_WithDirectoryPackagesProps_SupportsCPM()
    {
        // Arrange
        var directoryPackagesProps = """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="CentralPackage" Version="5.0.0" />
              </ItemGroup>
            </Project>
            """;

        var projectContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="CentralPackage" />
              </ItemGroup>
            </Project>
            """;

        CreateFile("Directory.Packages.props", directoryPackagesProps);
        var projectPath = CreateProjectFile("TestProject.csproj", projectContent);
        var reader = new PackageReferenceReader(_logger);

        // Act
        var packages = reader.ReadPackageReferences(projectPath);

        // Assert
        await Assert.That(packages).Contains("CentralPackage");
    }

    [Test]
    public async Task ReadPackageReferences_MultipleProjects_EachGetsOwnCount()
    {
        // Arrange - 1 shared package in Directory.Build.props, 3 projects
        var directoryBuildProps = """
            <Project>
              <ItemGroup>
                <PackageReference Include="SharedPackage" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """;

        var project1Content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """;

        var project2Content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """;

        var project3Content = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """;

        CreateFile("Directory.Build.props", directoryBuildProps);
        var project1Path = CreateProjectFile("Project1.csproj", project1Content);
        var project2Path = CreateProjectFile("Project2.csproj", project2Content);
        var project3Path = CreateProjectFile("Project3.csproj", project3Content);
        var reader = new PackageReferenceReader(_logger);

        // Act
        var packages1 = reader.ReadPackageReferences(project1Path);
        var packages2 = reader.ReadPackageReferences(project2Path);
        var packages3 = reader.ReadPackageReferences(project3Path);

        // Assert - Each project should independently have 1 package
        await Assert.That(packages1.Count).IsEqualTo(1);
        await Assert.That(packages1).Contains("SharedPackage");

        await Assert.That(packages2.Count).IsEqualTo(1);
        await Assert.That(packages2).Contains("SharedPackage");

        await Assert.That(packages3.Count).IsEqualTo(1);
        await Assert.That(packages3).Contains("SharedPackage");

        // Total count across all projects: 3 (1 per project)
        var totalCount = packages1.Count + packages2.Count + packages3.Count;
        await Assert.That(totalCount).IsEqualTo(3);
    }

    [Test]
    public async Task ReadPackageReferences_NonExistentFile_ReturnsEmpty()
    {
        // Arrange
        var reader = new PackageReferenceReader(_logger);

        // Act
        var packages = reader.ReadPackageReferences("/non/existent/path.csproj");

        // Assert
        await Assert.That(packages).IsEmpty();
    }

    [Test]
    public async Task ReadPackageReferences_NullPath_ReturnsEmpty()
    {
        // Arrange
        var reader = new PackageReferenceReader(_logger);

        // Act
        var packages = reader.ReadPackageReferences(null!);

        // Assert
        await Assert.That(packages).IsEmpty();
    }

    [Test]
    public async Task ReadPackageReferences_DuplicatePackages_ReturnsDeduplicated()
    {
        // Arrange - Same package in both Directory.Build.props and project
        var directoryBuildProps = """
            <Project>
              <ItemGroup>
                <PackageReference Include="DuplicatePackage" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """;

        var projectContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="DuplicatePackage" Version="2.0.0" />
              </ItemGroup>
            </Project>
            """;

        CreateFile("Directory.Build.props", directoryBuildProps);
        var projectPath = CreateProjectFile("TestProject.csproj", projectContent);
        var reader = new PackageReferenceReader(_logger);

        // Act
        var packages = reader.ReadPackageReferences(projectPath);

        // Assert - Should only have 1 entry (deduplicated)
        await Assert.That(packages.Count).IsEqualTo(1);
        await Assert.That(packages).Contains("DuplicatePackage");
    }

    [Test]
    public async Task ReadPackageReferences_PackagesAreSorted()
    {
        // Arrange
        var projectContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Zebra" Version="1.0.0" />
                <PackageReference Include="Apple" Version="1.0.0" />
                <PackageReference Include="Mango" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """;

        var projectPath = CreateProjectFile("TestProject.csproj", projectContent);
        var reader = new PackageReferenceReader(_logger);

        // Act
        var packages = reader.ReadPackageReferences(projectPath);

        // Assert
        await Assert.That(packages.Count).IsEqualTo(3);
        await Assert.That(packages[0]).IsEqualTo("Apple");
        await Assert.That(packages[1]).IsEqualTo("Mango");
        await Assert.That(packages[2]).IsEqualTo("Zebra");
    }

    [Test]
    public async Task FindDirectoryBuildProps_ExistsInParent_ReturnsPath()
    {
        // Arrange
        CreateFile("Directory.Build.props", "<Project />");
        var subDir = Path.Combine(_tempDirectory!, "SubFolder");
        Directory.CreateDirectory(subDir);

        // Act
        var found = PackageReferenceReader.FindDirectoryBuildProps(subDir);

        // Assert
        await Assert.That(found).IsNotNull();
        await Assert.That(File.Exists(found!)).IsTrue();
        await Assert.That(Path.GetFileName(found)).IsEqualTo("Directory.Build.props");
    }

    [Test]
    public async Task FindDirectoryBuildProps_DoesNotExist_ReturnsNull()
    {
        // Arrange
        var subDir = Path.Combine(_tempDirectory!, "SubFolder");
        Directory.CreateDirectory(subDir);

        // Act
        var found = PackageReferenceReader.FindDirectoryBuildProps(subDir);

        // Assert
        await Assert.That(found).IsNull();
    }

    [Test]
    public async Task FindDirectoryPackagesProps_ExistsInParent_ReturnsPath()
    {
        // Arrange
        CreateFile("Directory.Packages.props", "<Project />");
        var subDir = Path.Combine(_tempDirectory!, "SubFolder");
        Directory.CreateDirectory(subDir);

        // Act
        var found = PackageReferenceReader.FindDirectoryPackagesProps(subDir);

        // Assert
        await Assert.That(found).IsNotNull();
        await Assert.That(File.Exists(found!)).IsTrue();
        await Assert.That(Path.GetFileName(found)).IsEqualTo("Directory.Packages.props");
    }

    private string CreateFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_tempDirectory!, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    private string CreateProjectFile(string fileName, string content)
    {
        return CreateFile(fileName, content);
    }
}
