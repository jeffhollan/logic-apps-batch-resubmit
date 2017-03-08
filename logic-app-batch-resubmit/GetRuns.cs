using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using logic_app_batch_resubmit.Models;

namespace logic_app_batch_resubmit
{
    
    [Cmdlet(VerbsCommon.Get, "LogicAppRuns", SupportsShouldProcess = true)]
    public class GetRuns : Cmdlet
    {
        private int _MaxRunsToResubmit = 200;
        private List<RunInfo> runIds = new List<RunInfo>();
        [Parameter]
        public string LogicAppName { get; set; }
        [Parameter]
        public string ResourceGroup { get; set; }
        [Parameter]
        public string SubscriptionId { get; set; }
        [Parameter]
        public DateTime StartTimeSpan { get; set; }
        [Parameter]
        public DateTime EndTimeSpan { get; set; }
        [Parameter]
        public string AccessToken { get; set; }
        [Parameter]
        public int MaxRunsToResubmit
        {
            get
            {
                return _MaxRunsToResubmit;
            }
            set
            {
                _MaxRunsToResubmit = value;
            }
        }


        protected override async void ProcessRecord()
        {
            WriteVerbose("Getting failed runs....");
            bool haveRunsInTimespan = false;
            string url = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroup}/providers/Microsoft.Logic/workflows/{LogicAppName}/runs?api-version=2016-06-01&$filter=status eq 'Failed'";
            using (var client = new HttpClient())
            {
                while (url != null)
                {
                    var runs = JObject.Parse(client.GetAsync(url).Result.Content.ReadAsStringAsync().Result);
                    url = IterateRuns(runs);
                }
                if(ShouldProcess($"Resubmit on {runIds.Count} runs"))
                {
                    ResubmitRuns(runIds, LogicAppName, ResourceGroup, SubscriptionId, AccessToken);
                }
            }
        }

        private string IterateRuns(JObject runs)
        {
            bool getMorePages = false;
            foreach(JObject value in (JArray)runs["value"])
            {
                if(value["properties"]["startTime"] != null && DateTime.Parse((string)value["properties"]["startTime"]) < StartTimeSpan)
                {
                    getMorePages = false;
                }
                else if (value["properties"]["startTime"] != null && DateTime.Parse((string)value["properties"]["startTime"]) < EndTimeSpan)
                {
                    if (value["properties"]["name"] != null)
                    {
                        runIds.Add(new RunInfo { name = (string)value["properties"]["name"], triggerName = (string)value["properties"]["trigger"]["name"]});
                        getMorePages = true;
                    }
                }
            }
            if(getMorePages && runs["nextLink"] != null && runs.Count < MaxRunsToResubmit)
            {
                return (string)runs["nextLink"];
            }
            else
            {
                if(runs.Count > MaxRunsToResubmit)
                {
                    runIds = runIds.Take(MaxRunsToResubmit).ToList();
                }
                return null;
            }
        }
    }
}
