﻿using System.Text.RegularExpressions;

namespace FSharpPacker;

public class FsxPreprocessor
{
    private string basePath;

    private List<SourceFile> sourceFiles = new();
    private List<string> references = new();
    private List<NugetReference> packageReferences = new();

    public FsxPreprocessor WithBasePath(string homedirectory)
    {
        basePath = homedirectory;
        return this;
    }

    public void AddSource(string sourceFile, string content)
    {
        sourceFiles.Add(new SourceFile(sourceFile, content));
    }

    public void AddSource(string sourceFile)
    {
        sourceFiles.Add(new SourceFile(sourceFile, File.ReadAllText(sourceFile)));
    }

    public void Process()
    {
        foreach (var sourceFile in sourceFiles.ToList())
        {
            Console.WriteLine(sourceFile.FileName);
            ProcessFile(sourceFile);
        }
    }

    private void ProcessFile(SourceFile sourceFile)
    {
        sourceFile.WriteLine($"module {Path.GetFileNameWithoutExtension(sourceFile.FileName)}");
        foreach (var line in sourceFile.ReadContent())
        {
            var normalizedLine = Regex.Replace(line, "\\s+\\#\\s+", "#");
            if (normalizedLine.StartsWith("#"))
            {
                if (normalizedLine.StartsWith("#r"))
                {
                    var path = Unquote(normalizedLine.Replace("#r ", string.Empty));
                    var normalizedReference = Regex.Replace(path, "\\s+nuget\\s+:\\s+", "nuget:");
                    if (normalizedReference.StartsWith("nuget:"))
                    {
                        var packageParts = normalizedReference.Substring("nuget:".Length).Split(',');
                        var name = packageParts[0].Trim();
                        var version = packageParts.ElementAtOrDefault(1)?.Trim() ?? "*";
                        this.packageReferences.Add(new (name, version));
                    }
                    else
                    {
                        var relativeReferencePath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sourceFile.FileName)), path);
                        this.references.Add(Path.GetFullPath(relativeReferencePath));
                    }
                }
                else if (normalizedLine.StartsWith("#help"))
                {
                    // Help command make no sense.
                    continue;
                }
                else if (normalizedLine.StartsWith("#quit"))
                {
                    sourceFile.WriteLine("System.Environment.Exit 0");
                }
                else if (normalizedLine.StartsWith("#load"))
                {
                    var path = Unquote(normalizedLine.Replace("#load ", string.Empty));
                    var relativeReferencePath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sourceFile.FileName)), path);
                    Console.WriteLine(relativeReferencePath);
                    var innerFile = new SourceFile(relativeReferencePath);
                    this.sourceFiles.Insert(0, innerFile);
                    ProcessFile(innerFile);
                    // sourceFile.WriteLine(innerFile.ReadProducedFile());
                }
            }
            else
            {
                sourceFile.WriteLine(line);
            }
        }
    }

    private string Unquote(string data)
    {
        return data.Trim('"');
    }

    public string GetSource(string mainFsx)
    {
        return sourceFiles.FirstOrDefault(_ => _.FileName == mainFsx).ReadProducedFile();
    }

    public IReadOnlyList<SourceFile> GetSources()
    {
        return sourceFiles;
    }

    public IReadOnlyList<string> GetReferences() => references;

    public IReadOnlyList<NugetReference> GetPackageReferences() => packageReferences;
}