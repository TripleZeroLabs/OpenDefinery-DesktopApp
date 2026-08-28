namespace OpenDefinery
{
    /// <summary>Outcome of loading a single shared parameter into a Revit document.</summary>
    public enum ParamLoadOutcome
    {
        /// <summary>Added to the family/project.</summary>
        Added,

        /// <summary>Already present, so it was skipped.</summary>
        AlreadyExists,

        /// <summary>Could not be added; see <see cref="ParamLoadResult.Message"/>.</summary>
        Failed
    }

    /// <summary>
    /// Per-parameter outcome from <see cref="RvtConnector.CreateParams"/>, so the UI can
    /// report exactly which parameters were added, skipped, or rejected.
    /// </summary>
    public class ParamLoadResult
    {
        public string Name { get; set; }
        public string Guid { get; set; }
        public ParamLoadOutcome Outcome { get; set; }
        public string Message { get; set; }
    }
}
