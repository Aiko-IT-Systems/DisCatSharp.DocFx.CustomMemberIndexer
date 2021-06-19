using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CsQuery;
using Microsoft.DocAsCode.Common;
using Microsoft.DocAsCode.Plugins;
using Newtonsoft.Json;

namespace HSNXT.DocFx.CustomMemberIndexer
{
    [Export(nameof(CustomMemberIndexer), typeof(IPostProcessor))]
    public class CustomMemberIndexer : IPostProcessor
    {
        public const string IndexFileName = "index.json";
        
        public ImmutableDictionary<string, object> PrepareMetadata(ImmutableDictionary<string, object> metadata) 
            => metadata;

        public Manifest Process(Manifest manifest, string outputFolder)
        {
            //Console.WriteLine(JsonConvert.SerializeObject(manifest, Formatting.Indented));

            var indexDataFilePath = Path.Combine(outputFolder, IndexFileName);
            
            var indexData = JsonUtility.Deserialize<Dictionary<string, SearchIndexItem>>(indexDataFilePath);
            
            foreach (var kvp in indexData)
            {
                var item = kvp.Value;
                var filePath = Path.Combine(outputFolder,item.Href);
                
                CQ cq;
                
                try
                {
                    using (var stream = EnvironmentContext.FileAbstractLayer.OpenRead(filePath))
                    {
                        cq = CQ.Create(stream, Encoding.UTF8);
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Can't load content from existing item {filePath}", ex);
                }

                var summaryElement = cq["h1 + div.summary"].FirstOrDefault();
                if (summaryElement != null) // the Count operation uses an enumerable underneath so let's avoid it
                {
                    item.Summary = summaryElement.TextContent;
                }
            }
            
            var htmlFiles = (
                from item in manifest.Files ?? Enumerable.Empty<ManifestItem>()
                from output in item.OutputFiles
                where item.DocumentType == "ManagedReference" 
                where output.Key.Equals(".html", StringComparison.OrdinalIgnoreCase)
                select output.Value.RelativePath
            ).ToArray();
            if (htmlFiles.Length == 0)
            {
                return manifest;
            }

            Console.WriteLine($"[D#+] Extracting method index data from {htmlFiles.Length} html files");
            foreach (var relativePath in htmlFiles)
            {
                var filePath = Path.Combine(outputFolder, relativePath);
                //Logger.LogDiagnostic($"Extracting index data from {filePath}");

                CQ cq;
                
                if (!EnvironmentContext.FileAbstractLayer.Exists(filePath)) continue;
                
                try
                {
                    using (var stream = EnvironmentContext.FileAbstractLayer.OpenRead(filePath))
                    {
                        cq = CQ.Create(stream, Encoding.UTF8);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[D#+] Warning: Can't load content from {filePath}: {ex.Message}");
                    continue;
                }
                var indexItems = ExtractItem(cq, relativePath);
                foreach (var item in indexItems)
                {
                    indexData[item.Href] = item;
                }
            }
            JsonUtility.Serialize(indexDataFilePath, indexData, Formatting.Indented);
            
            return manifest;
        }
        
        private static readonly Regex AllWords = new Regex(@"\w+", RegexOptions.Compiled);

        private IEnumerable<SearchIndexItem> ExtractItem(CQ cq, string relativePath)
        {
            var classHeader = cq["h1"].FirstElement().TextContent.Trim();
            
            var headings = cq["h4"];
            foreach (var heading in headings)
            {
                var nameSig = heading.TextContent.Trim(); // Unregister(AsyncEventHandler)
                var id = heading.Id; // DSharpPlus_AsyncEvent_Unregister_DSharpPlus_AsyncEventHandler_
                var uid = heading["data-uid"]; // DSharpPlus.AsyncEvent.Unregister(DSharpPlus.AsyncEventHandler)

                string summary = null;
                {
                    var summaryElement = heading.NextElementSibling;
                    if (summaryElement.Classes.Contains("summary"))
                    {
                        summary = summaryElement.TextContent.Trim();
                    }
                }

                string itemType = null;
                {
                    var sibling = heading.PreviousElementSibling;
                    while (sibling.NodeName.ToLowerInvariant() != "h3")
                    {
                        sibling = sibling.PreviousElementSibling;
                    }

                    switch (sibling.Id)
                    {
                        case "classes": // continue the outer loop, classes are handled by the existing indexer?
                        case "structs": // ^ same as above?
                        case "enums": // ^ same as above?
                        case "interfaces": // ^ same as above?
                        case "delegates": // ^ same thing here i think?
                        case "aliases": // continue the outer loop, aliases are handled in the base item
                            continue;
                        case "operators":
                            itemType = "Operator";
                            break;
                        case "events":
                            itemType = "Event";
                            break;
                        case "properties":
                            itemType = "Property";
                            break;
                        case "fields":
                            itemType = "Field";
                            break;
                        case "methods":
                            itemType = "Method";
                            break;
                        case "constructors":
                            itemType = "Constructor";
                            break;
                        case "eii":
                            itemType = "Explicit Interface Implementation";
                            break;
                        default:
                            throw new InvalidOperationException($"Unknown item type {sibling.Id}");
                    }
                }


                // summary, conceptual, decalaration (sic), parameters, etc
                var keywords = new List<string>();
                string aliases = null;
                
                for (var sibling = heading.NextElementSibling;
                    sibling != null && sibling.NodeName.ToLowerInvariant() != "h4";
                    sibling = sibling.NextElementSibling)
                {
                    foreach (Match match in AllWords.Matches(sibling.TextContent.Trim()))
                    {
                        keywords.Add(match.Value);
                    }
                }

                {
                    var sibling = heading.NextElementSibling;
                    while (sibling != null && sibling.Id != id + "_aliases") sibling = sibling.NextElementSibling;
                    if (sibling != null)
                    {
                        var aliasesSection = sibling.NextElementSibling;
                        aliases = aliasesSection.TextContent.Trim();
                    }
                }
                
                var idSpaced = id.Replace('_', ' ').Trim();

                var title = itemType == "Constructor" 
                    ? $"Constructor {nameSig}"
                    : $"{itemType} {AllWords.Match(nameSig).Value} in {classHeader}"; // AllWords.Match to get 1st value
                if (aliases != null) title += $" (like {aliases})";

                yield return new SearchIndexItem
                {
                    Href = relativePath + "#" + id,
                    Title = title,
                    
                    Signature = uid,
                    Summary = summary,
                    
                    Keywords = $"{uid} {idSpaced} {nameSig} {string.Join(" ", keywords)}"
                };
            }
        }
    }
}