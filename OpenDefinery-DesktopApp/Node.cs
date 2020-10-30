using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenDefinery
{
    /// <summary>
    /// The Node class is a generic class for deserializing Drupal responses when modifying content
    /// </summary>
    class Node
    {
        [JsonProperty("nid")]
        public Nid[] Nid { get; set; }

        [JsonProperty("uuid")]
        public Uuid[] Uuid { get; set; }

        [JsonProperty("vid")]
        public Nid[] Vid { get; set; }

        [JsonProperty("langcode")]
        public Langcode[] Langcode { get; set; }

        [JsonProperty("type")]
        public TypeElement[] Type { get; set; }

        [JsonProperty("revision_timestamp")]
        public RevisionTimestamp[] RevisionTimestamp { get; set; }

        [JsonProperty("status")]
        public DefaultLangcode[] Status { get; set; }

        [JsonProperty("title")]
        public Langcode[] Title { get; set; }

        [JsonProperty("created")]
        public Changed[] Created { get; set; }

        [JsonProperty("changed")]
        public Changed[] Changed { get; set; }

        [JsonProperty("promote")]
        public DefaultLangcode[] Promote { get; set; }

        [JsonProperty("sticky")]
        public DefaultLangcode[] Sticky { get; set; }

        [JsonProperty("default_langcode")]
        public DefaultLangcode[] DefaultLangcode { get; set; }

        [JsonProperty("revision_translation_affected")]
        public DefaultLangcode[] RevisionTranslationAffected { get; set; }

        [JsonProperty("path")]
        public Path[] Path { get; set; }

        [JsonProperty("field_guid")]
        public FieldGuid[] FieldGuid { get; set; }
    }

    public partial class FieldGuid
    {
        [JsonProperty("value")]
        public Guid Value { get; set; }
    }

    public partial class Changed
    {
        [JsonProperty("value")]
        public DateTimeOffset Value { get; set; }

        [JsonProperty("lang")]
        public string Lang { get; set; }

        [JsonProperty("format")]
        public string Format { get; set; }
    }

    public partial class DefaultLangcode
    {
        [JsonProperty("value")]
        public bool Value { get; set; }

        [JsonProperty("lang")]
        public string Lang { get; set; }
    }

    public partial class Uuid
    {
        [JsonProperty("value")]
        public Guid Value { get; set; }
    }

    public partial class Langcode
    {
        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("lang")]
        public string Lang { get; set; }
    }

    public partial class Nid
    {
        [JsonProperty("value")]
        public int Value { get; set; }
    }

    public partial class Path
    {
        [JsonProperty("alias")]
        public object Alias { get; set; }

        [JsonProperty("pid")]
        public object Pid { get; set; }

        [JsonProperty("langcode")]
        public string Langcode { get; set; }

        [JsonProperty("lang")]
        public string Lang { get; set; }
    }

    public partial class RevisionTimestamp
    {
        [JsonProperty("value")]
        public DateTimeOffset Value { get; set; }

        [JsonProperty("format")]
        public string Format { get; set; }
    }

    public partial class TypeElement
    {
        [JsonProperty("target_id")]
        public string TargetId { get; set; }
    }
}
