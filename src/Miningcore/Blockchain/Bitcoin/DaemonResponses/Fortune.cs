using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Miningcore.Blockchain.Bitcoin.DaemonResponses
{
    public class Fortune
    {
        public string Payee { get; set; }
        public string Script { get; set; }
        public long Amount { get; set; }
    }

    public class FortuneBlockTemplateExtra
    {
        public JToken Fortune { get; set; }

        [JsonProperty("fortune_payments_started")]
        public bool FortunePaymentsStarted { get; set; }
    }
}
