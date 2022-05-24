using Newtonsoft.Json;

namespace DisCatSharp.DocFx.CustomMemberIndexer
{
    /// <summary>
    /// The search index item.
    /// </summary>
    public class SearchIndexItem
    {
        /// <summary>
        /// Gets or sets the href.
        /// </summary>
        [JsonProperty("href")]
        public string Href { get; set; }

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        [JsonProperty("title")]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the keywords.
        /// </summary>
        [JsonProperty("keywords")]
        public string Keywords { get; set; }

        /// <summary>
        /// Gets or sets the signature.
        /// </summary>
        [JsonProperty("ct_sig")]
        public string Signature { get; set; }

        /// <summary>
        /// Gets or sets the summary.
        /// </summary>
        [JsonProperty("ct_sum")]
        public string Summary { get; set; }

        /// <summary>
        /// Whether an object is equal as <see cref="SearchIndexItem"/>.
        /// </summary>
        /// <param name="obj">The object.</param>
        public override bool Equals(object obj)
            => Equals(obj as SearchIndexItem);

        /// <summary>
        /// Whether an <see cref="SearchIndexItem"/> is equal to this <see cref="SearchIndexItem"/>.
        /// </summary>
        /// <param name="other">The other <see cref="SearchIndexItem"/>.</param>
        public bool Equals(SearchIndexItem other)
        {
            if (other == null)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return string.Equals(Title, other.Title) && string.Equals(Href, other.Href) && string.Equals(Keywords, other.Keywords);
        }

        /// <summary>
        /// Gets the hash code.
        /// </summary>
        public override int GetHashCode()
            => Title.GetHashCode() ^ Href.GetHashCode() ^ Keywords.GetHashCode();
    }
}