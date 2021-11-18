using CsQuery.ExtensionMethods.Xml;
using Newtonsoft.Json;

namespace DisCatSharp.DocFx.CustomMemberIndexer
{
    public class SearchIndexItem
    {
        [JsonProperty("href")]
        public string Href { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("keywords")]
        public string Keywords { get; set; }

        [JsonProperty("ct_sig")]
        public string Signature { get; set; }

        [JsonProperty("ct_sum")]
        public string Summary { get; set; }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as SearchIndexItem);
        }

        public bool Equals(SearchIndexItem other)
        {
            if (other == null)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return string.Equals(this.Title, other.Title) && string.Equals(this.Href, other.Href) && string.Equals(this.Keywords, other.Keywords);
        }

        public override int GetHashCode()
        {
            return Title.GetHashCode() ^ Href.GetHashCode() ^ Keywords.GetHashCode();
        }
    }
}