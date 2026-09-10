using Xunit;

namespace Broodline.Sim.Tests
{
    public class CorpusTests
    {
        [Fact]
        public void RunScenario_IsStableAcrossTwoCalls()
        {
            Assert.Equal(Corpus.RunScenario(0), Corpus.RunScenario(0));
        }

        // Serves two purposes with one suite: in an ordinary run BROODLINE_CORPUS_OUT
        // is unset, so this returns immediately and asserts nothing (a deliberate,
        // trivial pass — see Task 7 Step 2). Set that variable to a path and this
        // test emits the 500-scenario corpus for the cross-runtime diff instead.
        [Fact]
        public void EmitCorpusHashes()
        {
            var path = System.Environment.GetEnvironmentVariable("BROODLINE_CORPUS_OUT");
            if (string.IsNullOrEmpty(path)) return;   // ordinary runs skip this

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 500; i++)
                sb.AppendLine(i + "," + Corpus.RunScenario(i));
            System.IO.File.WriteAllText(path, sb.ToString());
        }
    }
}
