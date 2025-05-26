using System;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.ServiceConfigs;
using Mailjet.Client;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Volo.Abp.Emailing;

namespace IIASA.WorldCereal.Rdm.Core;

public class EmailHelper
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<EmailHelper> _logger;
    private readonly UserDatasetConfig _userDatasetConfig;
    private MailjetClient _client;
    private const string Sender = "no-reply@worldcereal-rdm.iiasa.ac.at";

    public EmailHelper(IEmailSender emailSender, ILogger<EmailHelper> logger, UserDatasetConfig userDatasetConfig)
    {
        _emailSender = emailSender;
        _logger = logger;
        _userDatasetConfig = userDatasetConfig;

        // Create a Mailjet client
        _client = new MailjetClient(userDatasetConfig.MailjetAPIKey, userDatasetConfig.MailjetAPISecret)
        {
            Version = ApiVersion.V3_1,
        };
    }

    public async Task SendDatasetShared(string datasetId, string title, string userEmail)
    {
        var body = $"A new Dataset was shared by user {userEmail}: {title} {datasetId}";
        var sub = $"{title} was shared";
        _logger.LogInformation($"Sending mail to - {_userDatasetConfig.RdmEmail} , sub- {sub} body-{body}");
        Console.WriteLine($"Sending mail to - {_userDatasetConfig.RdmEmail} , sub- {sub} body-{body}");
        /* await _emailSender.QueueAsync(emails, sub, body);*/

        await SendEmail(Sender, "Worldcereal RDM", _userDatasetConfig.RdmEmail,
            "Moderators", sub, body);
    }

    public async Task SendDatasetSharedVerified(string datasetId, string title, string userEmail)
    {
        var body = $"Name:{title} ID:{datasetId} Dataset Changed to verified by {userEmail}";
        var sub = $"{title} was verified";
        _logger.LogInformation($"Sending mail to - {_userDatasetConfig.RdmEmail} , sub- {sub} body-{body}");
        Console.WriteLine($"Sending mail to - {_userDatasetConfig.RdmEmail} , sub- {sub} body-{body}");
        /*await _emailSender.QueueAsync(emails, sub, body);*/
        await SendEmail(Sender, "Worldcereal RDM", _userDatasetConfig.RdmEmail,
            "Moderators", sub, body);
    }

    public async Task SendEmail(string from, string fromName, string to, string toName, string sub, string content)
    {
        // Define the email content
        var request = new MailjetRequest
            {
                Resource = Mailjet.Client.Resources.Send.Resource,
            }
            .Property(Mailjet.Client.Resources.Send.Messages, new JArray
            {
                new JObject
                {
                    {
                        "From", new JObject
                        {
                            { "Email", from }, // Sender email
                            { "Name", fromName } // Sender name
                        }
                    },
                    {
                        "To", new JArray
                        {
                            new JObject
                            {
                                { "Email", to }, // Recipient email
                                { "Name", toName } // Recipient name
                            },
                            new JObject
                            {
                                { "Email", "karanam@iiasa.ac.at" }, // Recipient email
                                { "Name", "santosh" } // Recipient name
                            },
                            new JObject
                            {
                                { "Email", "karanamiiasa@gmail.com" }, // Recipient email
                                { "Name", "santoshiiasa" } // Recipient name
                            }
                        }
                    },
                    { "Subject", sub }, // Email subject
                    { "TextPart", content }, // Plain text content
                    { "HtmlPart", $"<h3>{content}</h3>" } // HTML content
                }
            });

        try
        {
            // Send the email
            var response = await _client.PostAsync(request);
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Email sent successfully!");
            }
            else
            {
                Console.WriteLine($"Error: {response.StatusCode}");
                Console.WriteLine($"Response: {response.GetErrorInfo()}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }
    }
}