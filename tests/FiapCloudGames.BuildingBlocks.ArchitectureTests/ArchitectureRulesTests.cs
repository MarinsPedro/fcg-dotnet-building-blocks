using System.Reflection;
using System.Xml.Linq;
using FiapCloudGames.BuildingBlocks;
using Microsoft.AspNetCore.Mvc;

namespace FiapCloudGames.BuildingBlocks.ArchitectureTests;

public sealed class ArchitectureRulesTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>>
        AllowedModuleDependencies =
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                ["Authentication"] = new HashSet<string>(["Exceptions"], StringComparer.Ordinal),
                ["Exceptions"] = new HashSet<string>(StringComparer.Ordinal),
                ["Hosting"] = new HashSet<string>(StringComparer.Ordinal),
                ["Observability"] = new HashSet<string>(StringComparer.Ordinal),
                ["Web"] = new HashSet<string>(["Exceptions", "Observability"], StringComparer.Ordinal)
            };

    [Fact]
    public void Package_RemainsOneProductionProjectAndOneNuGet()
    {
        var root = FindRepositoryRoot();
        var productionProjects = Directory.GetFiles(
            Path.Combine(root, "src"),
            "*.csproj",
            SearchOption.AllDirectories);

        var project = Assert.Single(productionProjects);
        var document = XDocument.Load(project);
        var packageId = document.Descendants("PackageId").Single().Value;

        Assert.Equal("FiapCloudGames.BuildingBlocks", packageId);
        Assert.Empty(document.Descendants("ProjectReference"));
    }

    [Fact]
    public void Package_DoesNotReferencePersistenceOrDomainFrameworks()
    {
        var forbiddenReferences = new[]
        {
            "EntityFrameworkCore",
            "Npgsql",
            "MongoDB",
            "Dapper"
        };
        var references = BuildingBlocksAssemblyReference.Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        var violations = references
            .Where(reference => forbiddenReferences.Any(
                forbidden => reference.Contains(forbidden, StringComparison.Ordinal)))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void PublicApi_ContainsOnlyTechnicalModulesAndNoControllers()
    {
        var exportedTypes = BuildingBlocksAssemblyReference.Assembly.GetExportedTypes();
        var allowedNamespaces = AllowedModuleDependencies.Keys
            .Select(module => $"FiapCloudGames.BuildingBlocks.{module}")
            .Append("FiapCloudGames.BuildingBlocks")
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(exportedTypes, type =>
        {
            Assert.NotNull(type.Namespace);
            Assert.Contains(type.Namespace, allowedNamespaces);
        });
        Assert.DoesNotContain(
            exportedTypes,
            type => typeof(ControllerBase).IsAssignableFrom(type));
        Assert.DoesNotContain(
            exportedTypes,
            type => type.Name.EndsWith("DbContext", StringComparison.Ordinal) ||
                type.Name.EndsWith("Repository", StringComparison.Ordinal) ||
                type.Name.EndsWith("Entity", StringComparison.Ordinal));
    }

    [Fact]
    public void SourceFiles_FollowModuleNamespacesAndDependencyDirection()
    {
        var sourceRoot = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "FiapCloudGames.BuildingBlocks");
        var unexpectedModules = Directory.GetDirectories(sourceRoot)
            .Select(Path.GetFileName)
            .Where(name => name is not "bin" and not "obj")
            .Where(name => name is not null &&
                !AllowedModuleDependencies.ContainsKey(name))
            .ToArray();

        Assert.True(
            unexpectedModules.Length == 0,
            $"Módulos não declarados: {string.Join(", ", unexpectedModules)}.");

        foreach (var module in AllowedModuleDependencies)
        {
            var moduleRoot = Path.Combine(sourceRoot, module.Key);
            Assert.True(Directory.Exists(moduleRoot), $"Módulo ausente: {module.Key}.");

            foreach (var file in Directory.GetFiles(
                         moduleRoot,
                         "*.cs",
                         SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(file);
                Assert.Contains(
                    $"namespace FiapCloudGames.BuildingBlocks.{module.Key};",
                    source,
                    StringComparison.Ordinal);

                var dependencies = FindBuildingBlocksUsings(source)
                    .Where(dependency => !string.Equals(
                        dependency,
                        module.Key,
                        StringComparison.Ordinal))
                    .ToArray();
                var forbidden = dependencies
                    .Where(dependency => !module.Value.Contains(dependency))
                    .ToArray();

                Assert.True(
                    forbidden.Length == 0,
                    $"{Path.GetRelativePath(sourceRoot, file)} possui dependências " +
                    $"não permitidas: {string.Join(", ", forbidden)}.");
            }
        }
    }

    private static IEnumerable<string> FindBuildingBlocksUsings(string source)
    {
        const string prefix = "using FiapCloudGames.BuildingBlocks.";

        return source.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith(prefix, StringComparison.Ordinal))
            .Select(line => line[prefix.Length..])
            .Select(remainder => remainder.Split(['.', ';'], 2)[0])
            .Distinct(StringComparer.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(
                   directory.FullName,
                   "FiapCloudGames.BuildingBlocks.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException(
                "A raiz do repositório não foi encontrada.");
    }
}
