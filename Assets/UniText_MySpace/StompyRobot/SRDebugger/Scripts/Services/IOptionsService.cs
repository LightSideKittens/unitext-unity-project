namespace SRDebugger.Services
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using Internal;

    public interface IOptionsService
    {
                event EventHandler OptionsUpdated;

                event EventHandler<PropertyChangedEventArgs> OptionsValueUpdated;

        ICollection<OptionDefinition> Options { get; }

                /// <param name="obj">Object to scan for options</param>
        [Obsolete("Use IOptionsService.AddContainer instead.")]
        void Scan(object obj);

                void AddContainer(object obj);

                void RemoveContainer(object obj);
    }
}
