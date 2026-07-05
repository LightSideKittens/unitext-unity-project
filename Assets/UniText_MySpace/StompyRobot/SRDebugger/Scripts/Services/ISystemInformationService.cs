namespace SRDebugger
{
    using System;
    using Services;
    using SRF;

    public sealed class InfoEntry
    {
        public string Title { get; set; }

        public object Value
        {
            get
            {
                try
                {
                    return _valueGetter();
                }
                catch (Exception e)
                {
                    return "Error ({0})".Fmt(e.GetType().Name);
                }
            }
        }

        public bool IsPrivate { get; private set; }

        private Func<object> _valueGetter;

                /// <param name="name">Name to display to the user.</param>
        /// <param name="getter">Getter method to acquire the latest value.</param>
        /// <param name="isPrivate">If true, will be excluded from the bug reporter system.</param>
        /// <returns>The created <see cref="InfoEntry"/> object.</returns>
        public static InfoEntry Create(string name, Func<object> getter, bool isPrivate = false)
        {
            return new InfoEntry
            {
                Title = name,
                _valueGetter = getter,
                IsPrivate = isPrivate
            };
        }

                /// <param name="name">Name to display to the user.</param>
        /// <param name="value">The value of the entry.</param>
        /// <param name="isPrivate">If true, will be excluded from the bug reporter system.</param>
        /// <returns>The created <see cref="InfoEntry"/> object.</returns>
        public static InfoEntry Create(string name, object value, bool isPrivate = false)
        {
            return new InfoEntry
            {
                Title = name,
                _valueGetter = () => value,
                IsPrivate = isPrivate
            };
        }
    }
}

namespace SRDebugger.Services
{
    using System.Collections.Generic;

    public interface ISystemInformationService
    {
                IEnumerable<string> GetCategories();

                /// <param name="category">Category name to fetch (get a list of these from GetCategories())</param>
        /// <returns></returns>
        IList<InfoEntry> GetInfo(string category);

                /// <param name="category"></param>
        /// <param name="info"></param>
        void Add(InfoEntry info, string category = "Default");

                /// <param name="includePrivate">Set to true to include identifying private information (usually you don't want this)</param>
        /// <returns>The generated report</returns>
        Dictionary<string, Dictionary<string, object>> CreateReport(bool includePrivate = false);
    }
}
