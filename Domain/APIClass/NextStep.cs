using System.Collections.Concurrent;

namespace Domain.APIClass
{
    public class NextStep
    {
        public string nextStepName { get; set; } = "";
        public List<string>? options { get; set; }

        public NextStep(string nextStepName, List<string>? options = null)
        {
            this.nextStepName = nextStepName;
            this.options = options;
        }

    }
}
