using System.Collections.Concurrent;

namespace Domain.APIClass
{
    public class NextStep
    {
        public string nextStepName { get; set; } = "";
        public List<string> options { get; set; } = null!;

        public NextStep(string nextStepName, List<string> options)
        {
            this.nextStepName = nextStepName;
            this.options = options;
        }

    }
}
