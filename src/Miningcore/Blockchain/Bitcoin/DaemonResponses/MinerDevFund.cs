using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Miningcore.Blockchain.Bitcoin.DaemonResponses
{
    public class MinerDevFundTemplateExtra
    {
        public string[] Addresses { get; set; }
        public ulong MinimumValue { get; set; }
    }
}
