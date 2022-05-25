using CsQuery;

using Microsoft.DocAsCode.Common;
using Microsoft.DocAsCode.Plugins;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DisCatSharp.DocFx.CustomMemberIndexer
{
    /// <summary>
    /// The custom member indexer.
    /// </summary>
    [Export(nameof(CustomMemberIndexer), typeof(IPostProcessor))]
    public class CustomMemberIndexer : IPostProcessor
    {
        /// <summary>
        /// The index file name.
        /// </summary>
        public const string IndexFileName = "index.json";

        /// <summary>
        /// Prepares the metadata.
        /// </summary>
        /// <param name="metadata">The metadata.</param>
        /// <returns>An <see cref="ImmutableDictionary"/>.</returns>
        public ImmutableDictionary<string, object> PrepareMetadata(ImmutableDictionary<string, object> metadata) 
            => metadata;

        /// <summary>
        /// Processes the manifest.
        /// </summary>
        /// <param name="manifest">The manifest.</param>
        /// <param name="outputFolder">The output folder.</param>
        /// <returns>A <see cref="Manifest"/>.</returns>
        public Manifest Process(Manifest manifest, string outputFolder)
        {
            var indexDataFilePath = Path.Combine(outputFolder, IndexFileName);
            
            var indexData = JsonUtility.Deserialize<Dictionary<string, SearchIndexItem>>(indexDataFilePath);
            
            foreach (var kvp in indexData)
            {
                var item = kvp.Value;
                var filePath = Path.Combine(outputFolder,item.Href);
                
                CQ cq;
                
                try
                {
                    using var stream = EnvironmentContext.FileAbstractLayer.OpenRead(filePath);
                    cq = CQ.Create(stream, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Can't load content from existing item {filePath}", ex);
                }

                var summaryElement = cq["h1 + div.summary"].FirstOrDefault();
                if (summaryElement != null)
                    item.Summary = summaryElement.TextContent;
            }
            
            var htmlFiles = (
                from item in manifest.Files ?? Enumerable.Empty<ManifestItem>()
                from output in item.OutputFiles
                where item.DocumentType == "ManagedReference" 
                where output.Key.Equals(".html", StringComparison.OrdinalIgnoreCase)
                select output.Value.RelativePath
            ).ToArray();
            
            if (htmlFiles.Length == 0)
                return manifest;

            Logger.LogInfo($"[DC#] Extracting method index data from {htmlFiles.Length} html files");


            foreach (var relativePath in htmlFiles)
            {
                var filePath = Path.Combine(outputFolder, relativePath);

                CQ cq;
                
                if (!EnvironmentContext.FileAbstractLayer.Exists(filePath)) continue;
                
                try
                {
                    using var stream = EnvironmentContext.FileAbstractLayer.OpenRead(filePath);
                    cq = CQ.Create(stream, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[DC#] Warning: Can't load content from {filePath}: {ex.Message}");
                    continue;
                }
                var indexItems = ExtractItem(cq, relativePath);
                foreach (var item in indexItems)
                    indexData[item.Href] = item;
            }
            JsonUtility.Serialize(indexDataFilePath, indexData, Formatting.Indented);
            
            return manifest;
        }
        
        private static readonly Regex AllWords = new Regex(@"\w+", RegexOptions.Compiled);

        /// <summary>
        /// Extracts the item.
        /// </summary>
        /// <param name="cq">The cq.</param>
        /// <param name="relativePath">The relative path.</param>
        /// <returns>A list of <see cref="SearchIndexItems"/>.</returns>
        private IEnumerable<SearchIndexItem> ExtractItem(CQ cq, string relativePath)
        {
            var classHeader = cq["h1"].FirstElement().TextContent.Trim();
            
            var headings = cq["h4"];
            foreach (var heading in headings)
            {
                var nameSig = heading.TextContent.Trim();
                var id = heading.Id;
                var uid = heading["data-uid"];

                string summary = null;
                {
                    var summaryElement = heading.NextElementSibling;
                    if (summaryElement.Classes.Contains("summary"))
                        summary = summaryElement.TextContent.Trim();
                }

                string itemType = null;
                {
                    var sibling = heading.PreviousElementSibling;
                    while (sibling.NodeName.ToLowerInvariant() != "h3")
                        sibling = sibling.PreviousElementSibling;

                    switch (sibling.Id)
                    {
                        case "classes":
                        case "structs":
                        case "enums":
                        case "interfaces":
                        case "delegates":
                        case "aliases":
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

                var keywords = new List<string>();
                string aliases = null;
                
                for (var sibling = heading.NextElementSibling;
                    sibling != null && sibling.NodeName.ToLowerInvariant() != "h4";
                    sibling = sibling.NextElementSibling)
                    foreach (Match match in AllWords.Matches(sibling.TextContent.Trim()))
                        keywords.Add(match.Value);

                {
                    var sibling = heading.NextElementSibling;
                    while (sibling != null && sibling.Id != id + "_aliases") 
                        sibling = sibling.NextElementSibling;
                    if (sibling != null)
                        aliases = sibling.NextElementSibling.TextContent.Trim();
                }
                
                var idSpaced = id.Replace('_', ' ').Trim();

                var title = itemType == "Constructor" 
                    ? $"Constructor {nameSig}"
                    : $"{itemType} {AllWords.Match(nameSig).Value} in {classHeader}";
                if (aliases != null) 
                    title += $" (like {aliases})";

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