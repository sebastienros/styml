// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using Xunit;

namespace System.Text.Yaml.Tests
{
    public class GrammarModeTests
    {
        /// <summary>The reader reads YAML. Its JSON grammar and the switch that chose it were deleted in M7a (decision D8):
        /// setting the switch does nothing.</summary>
        [Fact]
        public void ReaderReadsYaml_TheDeletedSwitchDoesNothing()
        {
            GrammarModeInitializer.AssertReadsYaml();
            AppContext.SetSwitch(GrammarModeInitializer.DeletedReaderSwitchName, true);
            try
            {
                GrammarModeInitializer.AssertReadsYaml();
            }
            finally
            {
                AppContext.SetSwitch(GrammarModeInitializer.DeletedReaderSwitchName, false);
            }
        }
    }
}
