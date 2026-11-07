using System;
using System.Collections.Generic;

namespace FastIoC.Core
{
    [Serializable]
    public class StringListWrapper
    {
        public string[] items = new string[0];

        public StringListWrapper()
        {
        }

        public StringListWrapper(List<string> list)
        {
            if (list != null && list.Count > 0)
                items = list.ToArray();
        }

        public List<string> ToList()
        {
            List<string> result = new();
            if (items != null)
                result.AddRange(items);
            return result;
        }
    }

    [Serializable]
    public class GenerationStatus
    {
        public enum Status
        {
            Success,
            Failed,
            Unknown
        }

        public Status status = Status.Unknown;
        public string lastUpdateTime = "";
        public string errorReason = "";
        public StringListWrapper errorDetails = new();
        /// <summary>Problems that don't fail generation, such as `internal` fields whose service type has no binding.</summary>
        public StringListWrapper warnings = new();

        public GenerationStatus()
        {
        }

        public GenerationStatus(Status status, string errorReason = "", List<string> errorDetails = null)
        {
            this.status = status;
            this.lastUpdateTime = DateTime.UtcNow.ToString("O");
            this.errorReason = errorReason;
            this.errorDetails = new StringListWrapper(errorDetails);
        }

        public override string ToString()
            => $"GenerationStatus(status={status}, time={lastUpdateTime}, error={errorReason})";
    }
}

