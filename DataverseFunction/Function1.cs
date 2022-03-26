using Azure;
using Azure.AI.FormRecognizer;
using Azure.Messaging.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace DataverseFunction
{
    public class Function1
    {
        [FunctionName("Function1")]
        public static async Task Run([EventHubTrigger("forms", Connection = "EventHubConnString")] EventData[] events, ILogger log)
        {
            var exceptions = new List<Exception>();
            var context = new ApplicationDbContext();
            var forms = await context.Forms.ToListAsync();
            foreach (EventData eventData in events)
            {
                try
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.ToArray());
                    string primaryEntityId = JsonConvert.DeserializeObject<dynamic>(messageBody).PrimaryEntityId;

                    // Dataverse table
                    var environmentId = "<...>";
                    var prefix = "<...>";
                    var tableName = "forms";
                    var detailsTableName = "formdetails";
                    var imageColumnName = "image";
                    var resource = $"https://{environmentId}.api.crm4.dynamics.com";
                    var apiVersion = "9.2";

                    // Azure app
                    var tenantId = "<...>";
                    var clientId = "<...>";
                    var clientSecret = "<...>";

                    // Form recognizer
                    var formRecognizerName = "powerapp-form-recognizer";
                    var formRecognizerKey = "<...>";

                    var authContext = new AuthenticationContext("https://login.microsoftonline.com/" + tenantId);
                    var credential = new ClientCredential(clientId, clientSecret);
                    var authResult = await authContext.AcquireTokenAsync(resource, credential);
                    var accessToken = authResult.AccessToken;

                    // Get the uploaded image from Dataverse.
                    var client = new HttpClient
                    {
                        BaseAddress = new Uri($"{resource}/api/data/v{apiVersion}/")
                    };
                    var headers = client.DefaultRequestHeaders;
                    headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    headers.Add("OData-MaxVersion", "4.0");
                    headers.Add("OData-Version", "4.0");
                    headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var response = await client.GetAsync($"{prefix}_{tableName}({primaryEntityId})?$select={prefix}_{imageColumnName},{prefix}_name");
                    var content = await response.Content.ReadAsStringAsync();
                    string image = JsonConvert.DeserializeObject<dynamic>(content)[$"{prefix}_{imageColumnName}"];
                    var bytes = Convert.FromBase64String(image);

                    // Send the image to Form Recognizer.
                    var endpoint = $"https://{formRecognizerName}.cognitiveservices.azure.com";
                    var recognizer = new FormRecognizerClient(new Uri(endpoint), new AzureKeyCredential(formRecognizerKey));
                    using var stream = new MemoryStream(bytes);
                    //var operation = await recognizer.StartRecognizeContentAsync(stream);
                    var operation = await recognizer.StartRecognizeInvoicesAsync(stream);
                    var result = await operation.WaitForCompletionAsync();

                    // Loop through the results and send them back to Dataverse.

                    // Use the result of recognize content.
                    //foreach (var value in result.Value)
                    //{
                    //    foreach (var line in value.Lines)
                    //    {
                    //        var postResult = await client.PostAsJsonAsync($"{prefix}_{detailsTableName}", new List<object>
                    //        {
                    //            new Dictionary<string, object>
                    //            {
                    //                { $"{prefix}_name", line.Text },
                    //                { $"{prefix}_textdata", line.Text },
                    //                { $"{prefix}_Form@odata.bind", $"/{prefix}_{tableName}({primaryEntityId})" }
                    //            }
                    //        });
                    //        var resultContext = await postResult.Content.ReadAsStringAsync();
                    //        postResult.EnsureSuccessStatusCode();
                    //    }
                    //}

                    // Use the result of recognize invoice.
                    foreach (var value in result.Value)
                    {
                        foreach (var field in value.Fields)
                        {
                            var postResult = await client.PostAsJsonAsync($"{prefix}_{detailsTableName}", new List<object>
                            {
                                new Dictionary<string, object>
                                {
                                    { $"{prefix}_name", field.Key },
                                    { $"{prefix}_textdata", field.Value.ValueData?.Text },
                                    { $"{prefix}_Form@odata.bind", $"/{prefix}_{tableName}({primaryEntityId})" }
                                }
                            });
                            var resultContext = await postResult.Content.ReadAsStringAsync();
                            postResult.EnsureSuccessStatusCode();
                        }
                    }

                    // Write to Azure SQL.
                    string name = JsonConvert.DeserializeObject<dynamic>(content)[$"{prefix}_name"];
                    var form = new Form
                    {
                        Base64Image = image,
                        Name = name,
                        FormDetails = result.Value.SelectMany(v => v.Fields.Select(f => new FormDetail
                        {
                            Name = f.Key,
                            TextData = f.Value.ValueData?.Text
                        })).ToList()
                    };
                    context.Forms.Add(form);
                    await context.SaveChangesAsync();

                    log.LogInformation($"C# Event Hub trigger function processed a message: {messageBody}");
                }
                catch (Exception e)
                {
                    // We need to keep processing the rest of the batch - capture this exception and continue.
                    // Also, consider capturing details of the message that failed processing so it can be processed again later.
                    exceptions.Add(e);
                }
            }

            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.
            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }
    }
}
