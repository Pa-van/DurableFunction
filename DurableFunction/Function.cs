using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Net.Http;
using System.Threading;
using System.Collections.Generic;
using DurableTask.Core.Query;
using System.Linq;

namespace DurableFunction
{
    public static class Function
    {
        [FunctionName("TerminateAllPending")]
        public static async Task Run(
     [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
     [DurableClient] IDurableOrchestrationClient client,
     ILogger log)
        {
            log.LogInformation("Started working on the Taskhub : {TaskHub}", client.TaskHubName);
            try
            {
                var queryFilter = new OrchestrationStatusQueryCondition
                {
                    RuntimeStatus = new[]
            {
            OrchestrationRuntimeStatus.Running,
        },
                    //CreatedTimeFrom = DateTime.UtcNow.Subtract(TimeSpan.FromDays(365)),
                    //CreatedTimeTo = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1)),
                    // default paze size
                    PageSize = 100
                };

                string token;
                var queryList = new List<DurableOrchestrationStatus>();
                do
                {
                    var result = await client.ListInstancesAsync(
                    queryFilter,
                    CancellationToken.None);
                    queryList.AddRange(result.DurableOrchestrationState);
                    token = result.ContinuationToken;
                    queryFilter.ContinuationToken = token;
                }
                while (token != null);

                var instanceIds = queryList.Select(x => x.InstanceId);
                var terminationBucket = new List<Task>();
                foreach (var instanceId in instanceIds)
                {
                    var terminationTask = client.TerminateAsync(instanceId, "Running too long, terminated through Automation");
                    terminationBucket.Add(terminationTask);
                }

                log.LogInformation("Total of {instanceCount} found in pending status", terminationBucket.Count);
                await Task.WhenAll(terminationBucket);
                log.LogInformation("All instance have been terminated");
            }
            catch (Exception ex)
            {
                log.LogError("Exception Occured with error : {message}",ex.Message);
                throw;
            }
        }
    }
}
